using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace ServiceLib.Handler;

/// <summary>
/// Saila: picks the routing from the user's own country. Iran → "Iran direct (Saila)", China → the
/// Chinese whitelist, elsewhere → global. Applied only when the detected country changes, so a manual
/// choice in between stays until the user is in another country.
/// </summary>
public static class SailaAutoRouting
{
    private static readonly string _tag = "SailaAutoRouting";

    /// <summary>Returns true when the active routing was changed.</summary>
    public static async Task<bool> CheckAsync(Config config)
    {
        if (!config.UiItem.SailaAutoRouting)
        {
            return false;
        }
        var target = await PickAsync(config);
        return target != null && await ConfigHandler.SetDefaultRouting(config, target) == 0;
    }

    /// <summary>
    /// Detects the country and returns the routing for it when the country changed since the last
    /// time (and records it), else null. The caller makes it active.
    /// </summary>
    public static async Task<RoutingItem?> PickAsync(Config config)
    {
        var cc = await DetectCountryAsync();
        if (cc.IsNullOrEmpty() || cc == config.UiItem.SailaRoutingCountry)
        {
            return null;
        }

        var items = await AppManager.Instance.RoutingItems();
        var target = cc switch
        {
            "ir" => items?.FirstOrDefault(t => t.Remarks.EndsWith("Iran direct (Saila)")),
            "cn" => items?.FirstOrDefault(t => t.Remarks.Contains("(Whitelist)")),
            _ => items?.FirstOrDefault(t => t.Remarks.Contains("(Global)")),
        };
        if (target == null)
        {
            return null;
        }

        config.UiItem.SailaRoutingCountry = cc;
        await ConfigHandler.SaveConfig(config);
        Logging.SaveLog($"{_tag}: country {cc} -> {target.Remarks}");
        return target;
    }

    /// <summary>Country name for the settings line, e.g. "Iran", or null when none was detected yet.</summary>
    public static string? CountryName(Config config)
    {
        try
        {
            if (config.UiItem.SailaRoutingCountry is not { Length: 2 } cc)
            {
                return null;
            }
            // DisplayName is in the Windows display language; use it only when the app is in Persian too.
            var region = new RegionInfo(cc);
            return (config.UiItem.CurrentLanguage ?? "").StartsWith("fa") ? region.DisplayName : region.EnglishName;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The user's own IP (bypassing the system proxy), else the Windows region.</summary>
    private static async Task<string?> DetectCountryAsync()
    {
        using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(6) };
        (string url, string field)[] sources =
        [
            ("https://ipwho.is/?fields=success,country_code", "country_code"),
            ("http://ip-api.com/json/?fields=countryCode", "countryCode"),
        ];
        foreach (var (url, field) in sources)
        {
            try
            {
                using var doc = JsonDocument.Parse(await http.GetStringAsync(url));
                if (doc.RootElement.TryGetProperty(field, out var v) && v.GetString() is { Length: 2 } code)
                {
                    return code.ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog($"{_tag}: lookup {new Uri(url).Host} failed", ex);
            }
        }
        try
        {
            return RegionInfo.CurrentRegion.TwoLetterISORegionName.ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }
}
