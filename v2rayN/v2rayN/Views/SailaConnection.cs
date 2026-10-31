using ServiceLib.Enums;
using ServiceLib.Handler;
using ServiceLib.Manager;
using ServiceLib.Models.Configs;
using ServiceLib.ViewModels;

namespace v2rayN.Views;

/// <summary>
/// Saila's connection model. The mode (system proxy, TUN, or the local port only) is a saved choice;
/// Connect / Disconnect turn that mode on and off without changing it. Home, Settings and the tray use this.
/// </summary>
public static class SailaConnection
{
    public const string ModeProxy = "Proxy";
    public const string ModeTun = "Tun";
    public const string ModePort = "Port";

    private static Config _config => AppManager.Instance.Config;

    // Port-only has no system-wide footprint to read back, so its "on" state lives here (one app run).
    private static bool _portOnlyOn;

    public static event Action? Changed;

    public static string Mode => _config.UiItem.SailaMode is ModeTun or ModePort ? _config.UiItem.SailaMode : ModeProxy;

    public static bool IsConnected =>
        _config.SystemProxyItem.SysProxyType is ESysProxyType.ForcedChange or ESysProxyType.Pac
        || StatusBarViewModel.Instance.EnableTun
        || _portOnlyOn;

    public static async Task ConnectAsync()
    {
        var mode = Mode;
        if (mode != ModeTun && StatusBarViewModel.Instance.EnableTun)
        {
            StatusBarViewModel.Instance.EnableTun = false;
        }
        await SetSystemProxyAsync(mode == ModeProxy ? ESysProxyType.ForcedChange : ESysProxyType.ForcedClear);
        _portOnlyOn = mode == ModePort;
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
        _portOnlyOn = false;
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
