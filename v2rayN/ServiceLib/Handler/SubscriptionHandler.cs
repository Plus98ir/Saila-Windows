namespace ServiceLib.Handler;

public static class SubscriptionHandler
{
    public static async Task UpdateProcess(Config config, string subId, bool blProxy, Func<bool, string, Task> updateFunc)
    {
        await updateFunc?.Invoke(false, ResUI.MsgUpdateSubscriptionStart);
        var subItem = await AppManager.Instance.SubItems();

        if (subItem is not { Count: > 0 })
        {
            await updateFunc?.Invoke(false, ResUI.MsgNoValidSubscription);
            return;
        }

        var successCount = 0;
        foreach (var item in subItem)
        {
            try
            {
                if (!IsValidSubscription(item, subId))
                {
                    continue;
                }

                var hashCode = $"{item.Remarks}->";
                if (item.Enabled == false)
                {
                    await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgSkipSubscriptionUpdate}");
                    continue;
                }

                // Create download handler
                var downloadHandle = CreateDownloadHandler(item, hashCode, updateFunc);
                await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgStartGettingSubscriptions}");

                // Get all subscription content (main subscription + additional subscriptions)
                var result = await DownloadAllSubscriptions(config, item, blProxy, downloadHandle);

                if (!string.IsNullOrWhiteSpace(downloadHandle.ResponseProfileTitle))
                {
                    item.Remarks = downloadHandle.ResponseProfileTitle;
                    await SQLiteHelper.Instance.UpdateAsync(item);
                }
                if (!string.IsNullOrWhiteSpace(downloadHandle.ResponseSubscriptionUserInfo))
                {
                    item.Memo = downloadHandle.ResponseSubscriptionUserInfo;
                    await SQLiteHelper.Instance.UpdateAsync(item);
                }

                // Process download result
                if (await ProcessDownloadResult(config, item.Id, result, hashCode, updateFunc))
                {
                    successCount++;
                    await ImportJsonOnlyProtocols(config, item, blProxy, downloadHandle);
                }

                await updateFunc?.Invoke(false, "-------------------------------------------------------");
            }
            catch (Exception ex)
            {
                var hashCode = $"{item.Remarks}->";
                Logging.SaveLog("UpdateSubscription", ex);
                await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgFailedImportSubscription}: {ex.Message}");
                await updateFunc?.Invoke(false, "-------------------------------------------------------");
            }
        }

        await updateFunc?.Invoke(successCount > 0, $"{ResUI.MsgUpdateSubscriptionEnd}");
    }

    /// <summary>
    /// Saila: MASQUE and XDRIVE have no share link, so a 3X-UI panel offers them only in its JSON subscription.
    /// Its address comes from the panel's subscription page ("subJsonUrl", works with custom paths), else /json/
    /// in place of /sub/. As custom outbounds they run inside Saila's own Xray config, so the local port, routing,
    /// statistics, TUN and ping work as for any other server. A failure here leaves the rest of the update alone.
    /// </summary>
    private static async Task ImportJsonOnlyProtocols(Config config, SubItem item, bool blProxy, DownloadService downloadHandle)
    {
        try
        {
            var url = Utils.GetPunycode(item.Url.TrimEx());
            var page = await DownloadSubscriptionContent(downloadHandle, $"{url}{(url.Contains('?') ? '&' : '?')}html=1", blProxy, BrowserUserAgent);
            var jsonUrl = FindJsonSubUrl(page) ?? (url.Contains("/sub/") ? url.Replace("/sub/", "/json/") : null);
            if (jsonUrl == null)
            {
                return;
            }
            var text = await DownloadSubscriptionContent(downloadHandle, jsonUrl, blProxy, item.UserAgent);
            foreach (var (remarks, outbound) in SelectJsonOnlyOutbounds(text))
            {
                if (V2rayFmt.ResolveOutbound(outbound, item.Remarks) is not { } profile)
                {
                    continue;
                }
                profile.Remarks = remarks ?? profile.Remarks;
                profile.Subid = item.Id;
                profile.IsSub = true;
                await ConfigHandler.AddCustomOutboundServer(config, profile, true);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("ImportJsonOnlyProtocols", ex);
        }
    }

    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/130 Safari/537.36";

    public static string? FindJsonSubUrl(string? page)
    {
        var match = Regex.Match(page ?? string.Empty, @"""subJsonUrl"":""(https?://[^""]+)""");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>From a JSON subscription, the proxy outbound of each MASQUE or XDRIVE config, with the config's name.</summary>
    public static List<(string? Remarks, JsonObject Outbound)> SelectJsonOnlyOutbounds(string? json)
    {
        var result = new List<(string?, JsonObject)>();
        if (json.IsNullOrEmpty() || !json.TrimStart().StartsWith('[') || JsonUtils.ParseJson(json) is not JsonArray configs)
        {
            return result;
        }
        foreach (var config in configs.OfType<JsonObject>())
        {
            if (config["outbounds"] is not JsonArray outbounds)
            {
                continue;
            }
            var wanted = outbounds.OfType<JsonObject>().FirstOrDefault(o =>
                o["protocol"]?.ToString() == "masque" || o["streamSettings"]?["network"]?.ToString() == "xdrive");
            if (wanted != null)
            {
                result.Add((config["remarks"]?.ToString(), wanted));
            }
        }
        return result;
    }

    private static bool IsValidSubscription(SubItem item, string subId)
    {
        var id = item.Id.TrimEx();
        var url = item.Url.TrimEx();

        if (id.IsNullOrEmpty() || url.IsNullOrEmpty())
        {
            return false;
        }

        if (subId.IsNotEmpty() && item.Id != subId)
        {
            return false;
        }

        if (!url.StartsWith(Global.HttpsProtocol) && !url.StartsWith(Global.HttpProtocol))
        {
            return false;
        }

        return true;
    }

    private static DownloadService CreateDownloadHandler(SubItem item, string hashCode, Func<bool, string, Task> updateFunc)
    {
        if (!HttpRequestHeadersHelper.TryParse(item.RequestHeaders, out var requestHeaders))
        {
            throw new FormatException(ResUI.SubRequestHeadersInvalid);
        }

        var downloadHandle = new DownloadService
        {
            AcceptHeader = "*/*",
            RequestHeaders = requestHeaders
        };
        downloadHandle.Error += (sender2, args) =>
        {
            updateFunc?.Invoke(false, $"{hashCode}{args.GetException().Message}");
        };
        return downloadHandle;
    }

    private static async Task<string> DownloadSubscriptionContent(DownloadService downloadHandle, string url, bool blProxy, string userAgent)
    {
        var result = await downloadHandle.TryDownloadString(url, blProxy, userAgent);

        // If download with proxy fails, try direct connection
        if (blProxy && result.IsNullOrEmpty())
        {
            result = await downloadHandle.TryDownloadString(url, false, userAgent);
        }

        return result ?? string.Empty;
    }

    private static async Task<string> DownloadAllSubscriptions(Config config, SubItem item, bool blProxy, DownloadService downloadHandle)
    {
        // Download main subscription content
        var result = await DownloadMainSubscription(config, item, blProxy, downloadHandle);

        // Process additional subscription links (if any)
        if (item.ConvertTarget.IsNullOrEmpty() && item.MoreUrl.TrimEx().IsNotEmpty())
        {
            result = await DownloadAdditionalSubscriptions(item, result, blProxy, downloadHandle);
        }

        return result;
    }

    private static async Task<string> DownloadMainSubscription(Config config, SubItem item, bool blProxy, DownloadService downloadHandle)
    {
        // Prepare subscription URL and download directly
        var url = Utils.GetPunycode(item.Url.TrimEx());

        // If conversion is needed
        if (item.ConvertTarget.IsNotEmpty())
        {
            var subConvertUrl = config.ConstItem.SubConvertUrl.IsNullOrEmpty()
                ? Global.SubConvertUrls.FirstOrDefault()
                : config.ConstItem.SubConvertUrl;

            url = string.Format(subConvertUrl!, Utils.UrlEncode(url));

            if (!url.Contains("target="))
            {
                url += $"&target={item.ConvertTarget}";
            }

            if (!url.Contains("config="))
            {
                url += $"&config={Global.SubConvertConfig.FirstOrDefault()}";
            }
        }

        // Download and return result directly
        return await DownloadSubscriptionContent(downloadHandle, url, blProxy, item.UserAgent);
    }

    private static async Task<string> DownloadAdditionalSubscriptions(SubItem item, string mainResult, bool blProxy, DownloadService downloadHandle)
    {
        var result = mainResult;

        // If main subscription result is Base64 encoded, decode it first
        if (result.IsNotEmpty() && Utils.IsBase64String(result))
        {
            result = Utils.Base64Decode(result);
        }

        // Process additional URL list
        var lstUrl = item.MoreUrl.TrimEx().Split(",") ?? [];
        foreach (var it in lstUrl)
        {
            var url2 = Utils.GetPunycode(it);
            if (url2.IsNullOrEmpty())
            {
                continue;
            }

            var additionalResult = await DownloadSubscriptionContent(downloadHandle, url2, blProxy, item.UserAgent);

            if (additionalResult.IsNotEmpty())
            {
                // Process additional subscription results, add to main result
                if (Utils.IsBase64String(additionalResult))
                {
                    result += Environment.NewLine + Utils.Base64Decode(additionalResult);
                }
                else
                {
                    result += Environment.NewLine + additionalResult;
                }
            }
        }

        return result;
    }

    private static async Task<bool> ProcessDownloadResult(Config config, string id, string result, string hashCode, Func<bool, string, Task> updateFunc)
    {
        if (result.IsNullOrEmpty())
        {
            await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgSubscriptionDecodingFailed}");
            return false;
        }

        await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgGetSubscriptionSuccessfully}");

        // If result is too short, display content directly
        if (result.Length < 99)
        {
            await updateFunc?.Invoke(false, $"{hashCode}{result}");
        }

        await updateFunc?.Invoke(false, $"{hashCode}{ResUI.MsgStartParsingSubscription}");

        // Add servers to configuration
        var ret = await ConfigHandler.AddBatchServers(config, result, id, true);
        if (ret <= 0)
        {
            Logging.SaveLog("FailedImportSubscription");
            Logging.SaveLog(result);
        }

        // Update completion message
        await updateFunc?.Invoke(false, ret > 0
                ? $"{hashCode}{ResUI.MsgUpdateSubscriptionEnd}"
                : $"{hashCode}{ResUI.MsgFailedImportSubscription}");

        return ret > 0;
    }
}
