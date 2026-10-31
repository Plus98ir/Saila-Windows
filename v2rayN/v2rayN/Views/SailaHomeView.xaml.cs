using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignColors.ColorManipulation;
using ReactiveUI;
using ServiceLib.Common;
using ServiceLib.Enums;
using ServiceLib.Handler;
using ServiceLib.Manager;
using ServiceLib.Models.Configs;
using ServiceLib.Models.Dto;
using ServiceLib.Models.Entities;
using ServiceLib.Resx;
using ServiceLib.ViewModels;

namespace v2rayN.Views;

public partial class SailaHomeView : UserControl
{
    private static Config _config => AppManager.Instance.Config;
    private readonly DispatcherTimer _timer;
    private DateTime? _connectedSince;
    private long _sessionBytes;
    private IDisposable? _statsSubscription;
    private IDisposable? _proxyChangeSubscription;
    private bool _isConnected;
    // Text colors of the current look; the connection state reuses them instead of fixed white.
    private Brush _textPrimary = Brushes.White;
    private Brush? _navDefaultBackground;
    private Brush _textSecondary = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));

    public SailaHomeView()
    {
        InitializeComponent();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;

        Loaded += SailaHomeView_Loaded;
        Unloaded += SailaHomeView_Unloaded;
        IsVisibleChanged += SailaHomeView_IsVisibleChanged;
    }

    private void SailaHomeView_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyThemeStyle();
        RefreshState();

        _statsSubscription?.Dispose();
        _statsSubscription = AppEvents.DispatcherStatisticsRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(UpdateSpeedStatistics);

        _proxyChangeSubscription?.Dispose();
        _proxyChangeSubscription = AppEvents.SysProxyChangeRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => RefreshState());

        _timer.Start();
    }

    private void SailaHomeView_Unloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _statsSubscription?.Dispose();
        _proxyChangeSubscription?.Dispose();
    }

    private void SailaHomeView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            ApplyThemeStyle();
            RefreshState();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    public void RefreshState()
    {
        CheckConnectionState();
        _ = RefreshServerInfoAsync();
    }

    private void CheckConnectionState()
    {
        bool wasConnected = _isConnected;
        _isConnected = _config.SystemProxyItem.SysProxyType == ESysProxyType.ForcedChange
                       || _config.SystemProxyItem.SysProxyType == ESysProxyType.Pac
                       || StatusBarViewModel.Instance.EnableTun;

        orbControl.IsConnected = _isConnected;

        if (_isConnected)
        {
            if (_connectedSince == null)
            {
                _connectedSince = DateTime.Now;
            }

            txtConnectionStatus.Text = ResUI.saila_connected;
            txtConnectionStatus.Foreground = _textPrimary;
            iconShield.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)); // HealthGood green

            iconPower.Visibility = Visibility.Collapsed;
            txtTimer.Visibility = Visibility.Visible;

            UpdatePillButton(true);
        }
        else
        {
            _connectedSince = null;
            txtConnectionStatus.Text = ResUI.saila_not_connected;
            txtConnectionStatus.Foreground = _textSecondary;
            iconShield.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

            iconPower.Visibility = Visibility.Visible;
            txtTimer.Visibility = Visibility.Collapsed;

            UpdatePillButton(false);
        }
    }

    private void UpdatePillButton(bool isConnected)
    {
        var accentHex = _config.UiItem.SailaAccent ?? "#7AB0FF";
        var accentColor = ParseColor(accentHex);
        var deeperColor = Deeper(accentColor, 0.25f);

        if (isConnected)
        {
            btnPillConnect.Background = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255));
            btnPillConnect.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            btnPillConnect.BorderThickness = new Thickness(1);
            if (btnPillConnect.Template?.FindName("txtPillLabel", btnPillConnect) is TextBlock tb)
            {
                tb.Text = ResUI.saila_disconnect;
            }
        }
        else
        {
            var grad = new LinearGradientBrush(accentColor, deeperColor, new Point(0, 0), new Point(1, 0));
            grad.Freeze();
            btnPillConnect.Background = grad;
            btnPillConnect.BorderThickness = new Thickness(0);
            if (btnPillConnect.Template?.FindName("txtPillLabel", btnPillConnect) is TextBlock tb)
            {
                tb.Text = ResUI.saila_connect;
            }
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_isConnected && _connectedSince != null)
        {
            var diff = DateTime.Now - _connectedSince.Value;
            txtTimer.Text = string.Format("{0:D2}:{1:D2}:{2:D2}", (int)diff.TotalHours, diff.Minutes, diff.Seconds);
        }
    }

    private async Task RefreshServerInfoAsync()
    {
        try
        {
            var defaultServer = await ConfigHandler.GetDefaultServer(_config);
            if (defaultServer != null)
            {
                var remarks = defaultServer.Remarks.IsNullOrEmpty() ? defaultServer.Address : defaultServer.Remarks;
                var (avatar, isFlag, cleanRemarks) = ParseServerAvatar(remarks);

                txtAvatar.Text = avatar;
                txtAvatar.FontSize = isFlag ? 22 : 16;
                txtServerRemarks.Text = cleanRemarks;

                // Subtitle
                string subtitle = defaultServer.Address;
                if (!defaultServer.Subid.IsNullOrEmpty())
                {
                    var subs = await AppManager.Instance.SubItems();
                    var sub = subs.FirstOrDefault(s => s.Id == defaultServer.Subid);
                    if (sub != null && !sub.Remarks.IsNullOrEmpty())
                    {
                        subtitle = sub.Remarks;
                    }
                    UpdateUsageStrip(sub);
                }
                else
                {
                    UpdateUsageStrip(null);
                }

                txtServerSubtitle.Text = subtitle;
            }
            else
            {
                txtAvatar.Text = "SL";
                txtAvatar.FontSize = 16;
                txtServerRemarks.Text = ResUI.saila_no_server;
                txtServerSubtitle.Text = ResUI.saila_add_first;
                UpdateUsageStrip(null);
            }
        }
        catch
        {
        }
    }

    private void UpdateUsageStrip(SubItem? sub)
    {
        if (sub != null)
        {
            var (remaining, days, hasInfo) = GetSubscriptionUsage(sub);
            if (hasInfo)
            {
                colRemaining.Width = new GridLength(1, GridUnitType.Star);
                colDays.Width = new GridLength(1, GridUnitType.Star);
                panelRemaining.Visibility = Visibility.Visible;
                panelDays.Visibility = Visibility.Visible;
                txtRemainingVal.Text = remaining;
                txtDaysVal.Text = days;
                return;
            }
        }

        // No subscription info -> show only session usage across the strip
        colRemaining.Width = new GridLength(0);
        colDays.Width = new GridLength(0);
        panelRemaining.Visibility = Visibility.Collapsed;
        panelDays.Visibility = Visibility.Collapsed;
    }

    private void UpdateSpeedStatistics(ServerSpeedItem item)
    {
        if (item == null) return;

        txtSpeedUpload.Text = Utils.HumanFy(item.ProxyUp) + "/s";
        txtSpeedDownload.Text = Utils.HumanFy(item.ProxyDown) + "/s";

        if (_isConnected)
        {
            _sessionBytes += Math.Max(0, item.ProxyUp + item.ProxyDown);
            txtSessionVal.Text = Utils.HumanFy(_sessionBytes);
        }
    }

    private async void BtnToggleConnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isConnected)
            {
                // Disconnect
                AppEvents.SysProxyChangeRequested.Publish(ESysProxyType.ForcedClear);
                await StatusBarViewModel.Instance.ChangeSystemProxyAsync(ESysProxyType.ForcedClear, true);
                _config.SystemProxyItem.SysProxyType = ESysProxyType.ForcedClear;
                if (StatusBarViewModel.Instance.EnableTun)
                {
                    StatusBarViewModel.Instance.EnableTun = false;
                }
                await ConfigHandler.SaveConfig(_config);
            }
            else
            {
                // Connect
                AppEvents.SysProxyChangeRequested.Publish(ESysProxyType.ForcedChange);
                await StatusBarViewModel.Instance.ChangeSystemProxyAsync(ESysProxyType.ForcedChange, true);
                _config.SystemProxyItem.SysProxyType = ESysProxyType.ForcedChange;
                await ConfigHandler.SaveConfig(_config);
            }

            CheckConnectionState();
        }
        catch
        {
        }
    }

    private void RowServerHeader_Click(object sender, MouseButtonEventArgs e)
    {
        // Chevron opens Advanced view
        if (Application.Current.MainWindow is MainWindow mw)
        {
            mw.ShowHomeView(false);
        }
    }

    private void BtnNavHome_Click(object sender, RoutedEventArgs e)
    {
        // Already on home
    }

    private void BtnNavServers_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mw)
        {
            mw.ShowHomeView(false);
        }
    }

    private void BtnNavSettings_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mw && mw.ViewModel != null)
        {
            mw.ViewModel.OptionSettingCmd.Execute().Subscribe();
        }
    }

    private void BtnNavMenu_Click(object sender, RoutedEventArgs e)
    {
        btnNavMenu.ContextMenu.PlacementTarget = btnNavMenu;
        btnNavMenu.ContextMenu.IsOpen = true;
    }

    private void BtnAddServer_Click(object sender, RoutedEventArgs e)
    {
        btnAddServer.ContextMenu.PlacementTarget = btnAddServer;
        btnAddServer.ContextMenu.IsOpen = true;
    }

    private async void ItemClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mw)
        {
            await mw.AddServerViaClipboardAsync();
            RefreshState();
        }
    }

    private void ItemScan_Click(object sender, RoutedEventArgs e)
    {
        StatusBarViewModel.Instance.AddServerViaScanCmd.Execute().Subscribe();
        RefreshState();
    }

    private void ItemSubSetting_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mw && mw.ViewModel != null)
        {
            mw.ViewModel.SubSettingCmd.Execute().Subscribe();
        }
    }

    private void ItemSubUpdate_Click(object sender, RoutedEventArgs e)
    {
        StatusBarViewModel.Instance.SubUpdateCmd.Execute().Subscribe();
        RefreshState();
    }

    private void MenuCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mw)
        {
            mw.MenuCheckUpdate_Click(sender, e);
        }
    }

    private void MenuBackupRestore_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mw)
        {
            mw.MenuBackupAndRestore_Click(sender, e);
        }
    }

    private void MenuReload_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mw && mw.ViewModel != null)
        {
            mw.ViewModel.ReloadCmd.Execute().Subscribe();
            RefreshState();
        }
    }

    private void MenuClose_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mw)
        {
            mw.ShowHideWindow(false);
        }
    }

    private async void Look_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string look)
        {
            _config.UiItem.SailaLook = look;
            await ConfigHandler.SaveConfig(_config);
            ApplyThemeStyle();
        }
    }

    private async void Accent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string hex)
        {
            _config.UiItem.SailaAccent = hex;
            await ConfigHandler.SaveConfig(_config);
            ApplyThemeStyle();
        }
    }

    public void ApplyThemeStyle()
    {
        string look = _config.UiItem.SailaLook ?? "Nexus";
        string accentHex = _config.UiItem.SailaAccent ?? "#7AB0FF";
        Color accentColor = ParseColor(accentHex);
        Color deeperAccent = Deeper(accentColor, 0.25f);

        backdropControl.Look = look;
        backdropControl.AccentColor = accentColor;
        backdropControl.InvalidateVisual();

        orbControl.AccentColor = accentColor;
        orbControl.InvalidateVisual();

        // Accent brushes
        var accentBrush = new SolidColorBrush(accentColor);
        accentBrush.Freeze();
        var deeperBrush = new SolidColorBrush(deeperAccent);
        deeperBrush.Freeze();
        var accentGrad = new LinearGradientBrush(accentColor, deeperAccent, new Point(0, 0), new Point(1, 1));
        accentGrad.Freeze();

        // + Add button background
        btnAddServer.Tag = accentGrad;

        // Big Connect Button gradient
        btnMainConnect.Background = accentGrad;

        // Speed card icons
        iconUpload.Foreground = accentBrush;
        iconDownload.Foreground = accentBrush;

        // Info strip numbers
        txtSessionVal.Foreground = accentBrush;
        txtRemainingVal.Foreground = accentBrush;
        txtDaysVal.Foreground = accentBrush;

        // Nav active button
        if (btnNavHome.Template?.FindName("borderNavHome", btnNavHome) is Border bNavHome)
        {
            bNavHome.Background = accentBrush;
        }

        // Apply Look card styling
        Brush cardBg;
        Brush cardBorder;
        Brush textPrimary;
        Brush textSecondary;

        switch (look)
        {
            case "Glass":
                cardBg = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                cardBorder = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255));
                textPrimary = Brushes.White;
                textSecondary = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
                break;

            case "Black":
                cardBg = new SolidColorBrush(Color.FromRgb(0x14, 0x15, 0x19));
                cardBorder = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x33));
                textPrimary = Brushes.White;
                textSecondary = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                break;

            case "White":
                cardBg = Brushes.White;
                cardBorder = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xF0));
                textPrimary = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
                textSecondary = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
                break;

            case "Nexus":
            default:
                var nexusGrad = new LinearGradientBrush(
                    Color.FromArgb(192, 0x16, 0x20, 0x3E),
                    Color.FromArgb(176, 0x0A, 0x10, 0x22),
                    new Point(0, 0), new Point(0, 1));
                nexusGrad.Freeze();
                cardBg = nexusGrad;
                cardBorder = new SolidColorBrush(Color.FromArgb(53, 0x82, 0xA0, 0xFF));
                textPrimary = Brushes.White;
                textSecondary = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                break;
        }

        cardBg.Freeze();
        cardBorder.Freeze();

        cardServer.Background = cardBg;
        cardServer.BorderBrush = cardBorder;

        cardUpload.Background = cardBg;
        cardUpload.BorderBrush = cardBorder;

        cardDownload.Background = cardBg;
        cardDownload.BorderBrush = cardBorder;

        cardInfoStrip.Background = cardBg;
        cardInfoStrip.BorderBrush = cardBorder;

        cardNav.BorderBrush = cardBorder;

        txtServerRemarks.Foreground = textPrimary;
        txtSpeedUpload.Foreground = textPrimary;
        txtSpeedDownload.Foreground = textPrimary;

        _textPrimary = textPrimary;
        _textSecondary = textSecondary;
        txtConnectionStatus.Foreground = _isConnected ? textPrimary : textSecondary;
        borderAvatar.Background = new SolidColorBrush(Color.FromArgb(look == "White" ? (byte)40 : (byte)60, accentColor.R, accentColor.G, accentColor.B));
        txtAvatar.Foreground = look == "White" ? deeperBrush : Brushes.White;
        _navDefaultBackground ??= cardNav.Background;
        cardNav.Background = look == "White" ? cardBg : _navDefaultBackground;
        txtStatusLabel.Foreground = textSecondary;
        txtServerSubtitle.Foreground = textSecondary;

        UpdatePillButton(_isConnected);
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            if (hex.StartsWith("#"))
            {
                hex = hex.Substring(1);
            }
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                return Color.FromRgb(r, g, b);
            }
        }
        catch
        {
        }
        return Color.FromRgb(0x7A, 0xB0, 0xFF);
    }

    private static Color Deeper(Color c, float factor)
    {
        byte r = (byte)Math.Max(0, c.R * (1 - factor));
        byte g = (byte)Math.Max(0, c.G * (1 - factor));
        byte b = (byte)Math.Max(0, c.B * (1 - factor));
        return Color.FromRgb(r, g, b);
    }

    public static (string avatarText, bool isFlag, string cleanRemarks) ParseServerAvatar(string remarks)
    {
        if (string.IsNullOrWhiteSpace(remarks))
        {
            return ("SL", false, ResUI.saila_no_server);
        }

        // Check for country flag emoji: pair of regional indicator symbols
        var match = Regex.Match(remarks, @"\uD83C[\uDDE6-\uDDFF]\uD83C[\uDDE6-\uDDFF]");
        if (match.Success)
        {
            string flag = match.Value;
            string clean = remarks.Replace(flag, "").Trim();
            return (flag, true, clean.Length > 0 ? clean : remarks);
        }

        var parts = remarks.Split(new[] { ' ', '-', '_', '|', '@', ':', '.' }, StringSplitOptions.RemoveEmptyEntries);
        string initials = "SL";
        if (parts.Length >= 2 && parts[0].Length > 0 && parts[1].Length > 0)
        {
            initials = $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[1][0])}";
        }
        else if (remarks.Length >= 2)
        {
            initials = remarks.Substring(0, 2).ToUpper();
        }
        else
        {
            initials = remarks.ToUpper();
        }
        return (initials, false, remarks);
    }

    public static (string remaining, string daysLeft, bool hasInfo) GetSubscriptionUsage(SubItem? sub)
    {
        if (sub == null) return (string.Empty, string.Empty, false);

        string raw = $"{sub.Memo} {sub.RequestHeaders}";
        if (string.IsNullOrWhiteSpace(raw)) return (string.Empty, string.Empty, false);

        long upload = 0, download = 0, total = 0, expire = 0;
        foreach (var part in raw.Split(new[] { ';', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=');
            if (kv.Length == 2)
            {
                var key = kv[0].Trim().ToLowerInvariant();
                if (long.TryParse(kv[1].Trim(), out var val))
                {
                    if (key == "upload") upload = val;
                    else if (key == "download") download = val;
                    else if (key == "total") total = val;
                    else if (key == "expire") expire = val;
                }
            }
        }

        if (total <= 0 && expire <= 0) return (string.Empty, string.Empty, false);

        string remaining = total > 0 ? Utils.HumanFy(Math.Max(0, total - (upload + download))) : ResUI.saila_unlimited;
        string days = ResUI.saila_unlimited;
        if (expire > 0)
        {
            var expireTime = DateTimeOffset.FromUnixTimeSeconds(expire).LocalDateTime;
            var diff = expireTime - DateTime.Now;
            if (diff.TotalSeconds <= 0)
                days = ResUI.saila_expired;
            else
                days = Math.Ceiling(diff.TotalDays).ToString("0");
        }
        return (remaining, days, true);
    }
}

/// <summary>
/// Dynamic backdrop rendering for Nexus, Glass, Black, White.
/// </summary>
public class SailaBackdropControl : FrameworkElement
{
    public string Look { get; set; } = "Nexus";
    public Color AccentColor { get; set; } = Color.FromRgb(0x7A, 0xB0, 0xFF);

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double maxDim = Math.Max(w, h);

        switch (Look)
        {
            case "Glass":
                // Dark glass base with vivid glows
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x08, 0x0B, 0x16)), null, new Rect(0, 0, w, h));
                DrawRadialGlow(dc, AccentColor, 110, new Point(0, 0), maxDim * 0.72, w, h);
                DrawRadialGlow(dc, Deeper(AccentColor, 0.35f), 90, new Point(w * 1.05, h * 0.40), maxDim * 0.62, w, h);
                DrawRadialGlow(dc, Lighter(AccentColor, 0.25f), 50, new Point(w * 0.15, h * 0.72), maxDim * 0.45, w, h);
                DrawRadialGlow(dc, Deeper(AccentColor, 0.20f), 70, new Point(w * 0.95, h * 1.02), maxDim * 0.62, w, h);
                break;

            case "Black":
                // Pure dark with soft top glow
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0, 0, 0)), null, new Rect(0, 0, w, h));
                DrawRadialGlow(dc, AccentColor, 30, new Point(w * 0.5, 0), maxDim * 0.6, w, h);
                break;

            case "White":
                // Clean white with soft glow
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xF5, 0xF7, 0xFB)), null, new Rect(0, 0, w, h));
                DrawRadialGlow(dc, AccentColor, 25, new Point(w * 0.5, 0), maxDim * 0.6, w, h);
                break;

            case "Nexus":
            default:
                // Deep navy + 32px grid + accent glow top-right + violet glow bottom-left
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x06, 0x0A, 0x17)), null, new Rect(0, 0, w, h));
                DrawRadialGlow(dc, AccentColor, 90, new Point(w * 0.9, h * 0.05), maxDim * 0.75, w, h);
                DrawRadialGlow(dc, Color.FromRgb(0x7C, 0x3A, 0xED), 70, new Point(w * 0.1, h * 0.95), maxDim * 0.65, w, h);

                // 32px faint grid
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(14, 0x78, 0x96, 0xFF)), 1.0);
                pen.Freeze();
                for (double x = 0; x < w; x += 32.0)
                {
                    dc.DrawLine(pen, new Point(x, 0), new Point(x, h));
                }
                for (double y = 0; y < h; y += 32.0)
                {
                    dc.DrawLine(pen, new Point(0, y), new Point(w, y));
                }
                break;
        }
    }

    private static void DrawRadialGlow(DrawingContext dc, Color color, byte alpha, Point center, double radius, double w, double h)
    {
        if (radius <= 0) return;
        var grad = new RadialGradientBrush(
            Color.FromArgb(alpha, color.R, color.G, color.B),
            Color.FromArgb(0, color.R, color.G, color.B))
        {
            Center = center,
            GradientOrigin = center,
            RadiusX = radius,
            RadiusY = radius,
            MappingMode = BrushMappingMode.Absolute
        };
        grad.Freeze();
        dc.DrawRectangle(grad, null, new Rect(0, 0, w, h));
    }

    private static Color Deeper(Color c, float factor)
    {
        byte r = (byte)Math.Max(0, c.R * (1 - factor));
        byte g = (byte)Math.Max(0, c.G * (1 - factor));
        byte b = (byte)Math.Max(0, c.B * (1 - factor));
        return Color.FromRgb(r, g, b);
    }

    private static Color Lighter(Color c, float factor)
    {
        byte r = (byte)Math.Min(255, c.R + (255 - c.R) * factor);
        byte g = (byte)Math.Min(255, c.G + (255 - c.G) * factor);
        byte b = (byte)Math.Min(255, c.B + (255 - c.B) * factor);
        return Color.FromRgb(r, g, b);
    }
}

/// <summary>
/// Smooth dot ring particle orb animation around the connect button.
/// Runs at <= 30 fps (33ms timer) with 0 memory allocation during ticks.
/// </summary>
public class SailaOrbControl : FrameworkElement
{
    private readonly DispatcherTimer _timer;
    private double _phase;
    private SolidColorBrush[]? _cachedBrushes;
    private Color _lastAccentColor;

    public bool IsConnected { get; set; }
    public Color AccentColor { get; set; } = Color.FromRgb(0x7A, 0xB0, 0xFF);

    public SailaOrbControl()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 fps
        };
        _timer.Tick += (s, e) =>
        {
            _phase += IsConnected ? 0.055 : 0.018;
            InvalidateVisual();
        };

        Loaded += (s, e) => _timer.Start();
        Unloaded += (s, e) => _timer.Stop();
        IsVisibleChanged += (s, e) =>
        {
            if (IsVisible) _timer.Start();
            else _timer.Stop();
        };
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 10 || h <= 10) return;

        double cx = w / 2.0;
        double cy = h / 2.0;
        double maxR = Math.Min(w, h) / 2.0;

        int rings = 14;
        int dots = 72;

        if (_cachedBrushes == null || _lastAccentColor != AccentColor)
        {
            _lastAccentColor = AccentColor;
            _cachedBrushes = new SolidColorBrush[rings];
            for (int i = 0; i < rings; i++)
            {
                double t = (double)i / (rings - 1.0);
                byte a = (byte)Math.Max(20, Math.Min(240, (1.0 - t * 0.55) * 240));
                var b = new SolidColorBrush(Color.FromArgb(a, AccentColor.R, AccentColor.G, AccentColor.B));
                b.Freeze();
                _cachedBrushes[i] = b;
            }
        }

        for (int i = 0; i < rings; i++)
        {
            double t = (double)i / (rings - 1.0);
            double baseR = maxR * (0.50 + 0.46 * t);
            double dotRadius = 1.5 + 1.1 * (1.0 - t);
            Brush brush = _cachedBrushes[i];

            for (int j = 0; j < dots; j++)
            {
                double a = 2.0 * Math.PI * j / dots;
                double wave = 1.0 +
                    0.07 * Math.Sin(3.0 * a + _phase + i * 0.35) +
                    0.04 * Math.Sin(5.0 * a - _phase * 1.4 + i * 0.2);
                double r = baseR * wave;

                double x = cx + r * Math.Cos(a);
                double y = cy + r * Math.Sin(a);

                dc.DrawEllipse(brush, null, new Point(x, y), dotRadius, dotRadius);
            }
        }
    }
}
