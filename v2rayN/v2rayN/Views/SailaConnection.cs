using ServiceLib.Enums;
using ServiceLib.Handler;
using ServiceLib.Manager;
using ServiceLib.Models.Configs;
using ServiceLib.ViewModels;

namespace v2rayN.Views;

/// <summary>
/// Saila's connection model. The mode (system proxy or TUN) is a saved choice; the local port is always open.
/// Connect / Disconnect turn that mode on and off without changing it. Home, Settings and the tray use this.
/// </summary>
public static class SailaConnection
{
    public const string ModeProxy = "Proxy";
    public const string ModeTun = "Tun";

    private static Config _config => AppManager.Instance.Config;

    public static event Action? Changed;

    // A mode saved by an older build ("Port") falls back to system proxy.
    public static string Mode => _config.UiItem.SailaMode == ModeTun ? ModeTun : ModeProxy;

    public static bool IsConnected =>
        _config.SystemProxyItem.SysProxyType is ESysProxyType.ForcedChange or ESysProxyType.Pac
        || StatusBarViewModel.Instance.EnableTun;

    public static async Task ConnectAsync()
    {
        var mode = Mode;
        if (mode != ModeTun && StatusBarViewModel.Instance.EnableTun)
        {
            StatusBarViewModel.Instance.EnableTun = false;
        }
        await SetSystemProxyAsync(mode == ModeProxy ? ESysProxyType.ForcedChange : ESysProxyType.ForcedClear);
        if (mode == ModeTun && !StatusBarViewModel.Instance.EnableTun)
        {
            StatusBarViewModel.Instance.EnableTun = true;
        }
        Changed?.Invoke();
    }

    public static async Task DisconnectAsync()
    {
        if (StatusBarViewModel.Instance.EnableTun)
        {
            StatusBarViewModel.Instance.EnableTun = false;
        }
        await SetSystemProxyAsync(ESysProxyType.ForcedClear);
        Changed?.Invoke();
    }

    /// <summary>Saves the mode; when connected, switches the live connection to it.</summary>
    public static async Task SetModeAsync(string mode)
    {
        _config.UiItem.SailaMode = mode;
        await ConfigHandler.SaveConfig(_config);
        if (IsConnected)
        {
            await ConnectAsync();
        }
        else
        {
            Changed?.Invoke();
        }
    }

    private static async Task SetSystemProxyAsync(ESysProxyType type)
    {
        _config.SystemProxyItem.SysProxyType = type;
        await StatusBarViewModel.Instance.ChangeSystemProxyAsync(type, true);
        await ConfigHandler.SaveConfig(_config);
    }
}
