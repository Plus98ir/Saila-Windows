using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

public partial class SailaSettingsView : UserControl
{
    private static Config _config => AppManager.Instance.Config;
    private Border borderBack => SailaTemplate.Part<Border>(btnBack, "borderBack");
    private MaterialDesignThemes.Wpf.PackIcon iconBack => SailaTemplate.Part<MaterialDesignThemes.Wpf.PackIcon>(btnBack, "iconBack");
    private bool _initializing = true;

    private static readonly (string name, string hex, Color color)[] AccentList =
    [
        ("Blue", "#7AB0FF", Color.FromRgb(0x7A, 0xB0, 0xFF)),
        ("Purple", "#B79CFF", Color.FromRgb(0xB7, 0x9C, 0xFF)),
        ("Pink", "#FF8CC0", Color.FromRgb(0xFF, 0x8C, 0xC0)),
        ("Red", "#FF8A80", Color.FromRgb(0xFF, 0x8A, 0x80)),
        ("Orange", "#FFB074", Color.FromRgb(0xFF, 0xB0, 0x74)),
        ("Yellow", "#FFD54F", Color.FromRgb(0xFF, 0xD5, 0x4F)),
        ("Green", "#6EE7A0", Color.FromRgb(0x6E, 0xE7, 0xA0))
    ];

    public SailaSettingsView()
    {
        InitializeComponent();

        Loaded += SailaSettingsView_Loaded;
        IsVisibleChanged += (s, e) =>
        {
            if (IsVisible)
            {
                ApplyThemeStyle();
                _ = LoadSettingsUIAsync();
            }
        };
    }

    private void SailaSettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyThemeStyle();
        _ = LoadSettingsUIAsync();
    }

    private async Task LoadSettingsUIAsync()
    {
        _initializing = true;

        // RTL check
        bool isFa = (_config.UiItem.CurrentLanguage ?? string.Empty).StartsWith("fa");
        FlowDirection = isFa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        // 1. Connection Mode
        bool isTun = StatusBarViewModel.Instance.EnableTun;
        UpdateModeButtons(isTun);

        // Routing: select the item that is actually active in config/DB
        var routings = await AppManager.Instance.RoutingItems();
        if (routings != null && routings.Count > 0)
        {
            cmbRouting.ItemsSource = routings;
            var active = routings.FirstOrDefault(t => t.IsActive == true)
                      ?? routings.FirstOrDefault(t => t.Remarks.Contains("Iran direct"))
                      ?? routings.FirstOrDefault();
            if (active != null)
            {
                cmbRouting.SelectedItem = routings.FirstOrDefault(t => t.Id == active.Id);
                StatusBarViewModel.Instance.SelectedRouting = active;
            }
        }

        // Auto routing
        togAutoRouting.IsChecked = _config.UiItem.SailaAutoRouting;
        UpdateAutoRoutingStatus();

        // Auto Run
        togAutoRun.IsChecked = _config.GuiItem.AutoRun;

        // Local Port
        int port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
        txtLocalPort.Text = $"{port} ({EInboundProtocol.mixed})";

        // 2. Appearance
        UpdateLookButtons(_config.UiItem.SailaLook ?? "Nexus");
        BuildAccentCircles();

        // Language
        cmbLanguage.ItemsSource = Global.LanguageOptions;
        cmbLanguage.SelectedValue = _config.UiItem.CurrentLanguage;

        // 3. User-Agent
        string ua = _config.CoreBasicItem.DefUserAgent ?? string.Empty;
        if (ua.StartsWith("Saila", StringComparison.OrdinalIgnoreCase) || ua.IsNullOrEmpty())
        {
            cmbUserAgent.SelectedIndex = 0;
            txtCustomUA.Visibility = Visibility.Collapsed;
        }
        else if (ua.StartsWith("v2rayN", StringComparison.OrdinalIgnoreCase))
        {
            cmbUserAgent.SelectedIndex = 1;
            txtCustomUA.Visibility = Visibility.Collapsed;
        }
        else if (ua.IndexOf("Hiddify", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            cmbUserAgent.SelectedIndex = 2;
            txtCustomUA.Visibility = Visibility.Collapsed;
        }
        else
        {
            cmbUserAgent.SelectedIndex = 3;
            txtCustomUA.Text = ua;
            txtCustomUA.Visibility = Visibility.Visible;
        }

        // 4. About
        txtAbout.Text = string.Format(ResUI.saila_about_text, Utils.GetVersionInfo());

        _initializing = false;
    }

    private void UpdateModeButtons(bool isTun)
    {
        txtModeSub.Text = isTun ? ResUI.saila_mode_tun : ResUI.saila_mode_proxy;

        string look = _config.UiItem.SailaLook ?? "Nexus";
        string accentHex = _config.UiItem.SailaAccent ?? "#7AB0FF";
        Color accentColor = SailaHomeView.ParseColor(accentHex);
        var accentBrush = new SolidColorBrush(accentColor);
        accentBrush.Freeze();

        Brush inactiveText = look == "White"
            ? new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69))
            : new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));

        // The pill parts live inside each button's template, so look them up there.
        SetModePill(btnModeTun, "borderModeTun", "txtModeTun", isTun, accentBrush, inactiveText);
        SetModePill(btnModeProxy, "borderModeProxy", "txtModeProxy", !isTun, accentBrush, inactiveText);
    }

    private static void SetModePill(Button button, string borderName, string textName, bool active, Brush accent, Brush inactiveText)
    {
        button.ApplyTemplate();
        if (button.Template?.FindName(borderName, button) is Border border)
        {
            border.Background = active ? accent : Brushes.Transparent;
        }
        if (button.Template?.FindName(textName, button) is TextBlock text)
        {
            text.Foreground = active ? Brushes.White : inactiveText;
        }
    }

    private void BtnModeProxy_Click(object sender, RoutedEventArgs e)
    {
        StatusBarViewModel.Instance.EnableTun = false;
        UpdateModeButtons(false);
    }

    private void BtnModeTun_Click(object sender, RoutedEventArgs e)
    {
        StatusBarViewModel.Instance.EnableTun = true;
        UpdateModeButtons(true);
    }

    private async void CmbRouting_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (cmbRouting.SelectedItem is RoutingItem item)
        {
            StatusBarViewModel.Instance.SelectedRouting = item;
            await ConfigHandler.SetDefaultRouting(_config, item);
        }
    }

    private void UpdateAutoRoutingStatus()
    {
        var country = SailaAutoRouting.CountryName(_config);
        txtAutoRoutingStatus.Text = _config.UiItem.SailaAutoRouting && country != null
            ? string.Format(ResUI.saila_auto_routing_country, country)
            : ResUI.saila_auto_routing_summary;
    }

    private async void TogAutoRouting_Click(object sender, RoutedEventArgs e)
    {
        bool on = togAutoRouting.IsChecked == true;
        _config.UiItem.SailaAutoRouting = on;
        // Forget the last country so turning it on re-applies the routing now.
        _config.UiItem.SailaRoutingCountry = null;
        await ConfigHandler.SaveConfig(_config);
        UpdateAutoRoutingStatus();
        if (!on)
        {
            return;
        }

        var target = await SailaAutoRouting.PickAsync(_config);
        if (target != null && cmbRouting.ItemsSource is IEnumerable<RoutingItem> routings)
        {
            // Selecting it in the combo goes through CmbRouting_SelectionChanged, which activates and reloads.
            cmbRouting.SelectedItem = routings.FirstOrDefault(t => t.Id == target.Id);
        }
        UpdateAutoRoutingStatus();
    }

    private async void TogAutoRun_Click(object sender, RoutedEventArgs e)
    {
        bool isRun = togAutoRun.IsChecked == true;
        _config.GuiItem.AutoRun = isRun;
        await AutoStartupHandler.UpdateTask(_config);
        await ConfigHandler.SaveConfig(_config);
    }

    private void UpdateLookButtons(string activeLook)
    {
        string look = _config.UiItem.SailaLook ?? "Nexus";
        string accentHex = _config.UiItem.SailaAccent ?? "#7AB0FF";
        Color accentColor = SailaHomeView.ParseColor(accentHex);
        var accentBrush = new SolidColorBrush(accentColor);
        accentBrush.Freeze();

        Brush inactiveText = look == "White"
            ? new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69))
            : new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));

        void StyleBtn(Button btn, string lookName)
        {
            bool sel = string.Equals(activeLook, lookName, StringComparison.OrdinalIgnoreCase);
            btn.Background = sel ? accentBrush : Brushes.Transparent;
            btn.Foreground = sel ? Brushes.White : inactiveText;
        }

        StyleBtn(btnLookNexus, "Nexus");
        StyleBtn(btnLookGlass, "Glass");
        StyleBtn(btnLookBlack, "Black");
        StyleBtn(btnLookWhite, "White");
    }

    private async void BtnLook_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string look)
        {
            _config.UiItem.SailaLook = look;
            await ConfigHandler.SaveConfig(_config);
            UpdateLookButtons(look);
            (Application.Current.MainWindow as MainWindow)?.ApplyThemeStyle();
        }
    }

    private void BuildAccentCircles()
    {
        panelAccents.Children.Clear();
        string currentHex = _config.UiItem.SailaAccent ?? "#7AB0FF";

        foreach (var (name, hex, col) in AccentList)
        {
            bool isSelected = string.Equals(currentHex, hex, StringComparison.OrdinalIgnoreCase);

            var border = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(col),
                Margin = new Thickness(6, 0, 6, 0),
                Cursor = Cursors.Hand,
                ToolTip = name
            };

            if (isSelected)
            {
                border.BorderBrush = Brushes.White;
                border.BorderThickness = new Thickness(2.5);
                border.Child = new MaterialDesignThemes.Wpf.PackIcon
                {
                    Kind = MaterialDesignThemes.Wpf.PackIconKind.Check,
                    Width = 16,
                    Height = 16,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else
            {
                border.BorderThickness = new Thickness(0);
            }

            border.MouseLeftButtonUp += async (s, e) =>
            {
                _config.UiItem.SailaAccent = hex;
                await ConfigHandler.SaveConfig(_config);
                BuildAccentCircles();
                (Application.Current.MainWindow as MainWindow)?.ApplyThemeStyle();
            };

            panelAccents.Children.Add(border);
        }
    }

    private async void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (cmbLanguage.SelectedValue is string lang)
        {
            _config.UiItem.CurrentLanguage = lang;
            await ConfigHandler.SaveConfig(_config);

            bool isFa = lang.StartsWith("fa");
            FlowDirection = isFa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }
    }

    private async void CmbUserAgent_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (cmbUserAgent.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            switch (tag)
            {
                case "Saila":
                    _config.CoreBasicItem.DefUserAgent = $"Saila/{Utils.GetVersion(false)}";
                    txtCustomUA.Visibility = Visibility.Collapsed;
                    break;
                case "v2rayN":
                    _config.CoreBasicItem.DefUserAgent = $"v2rayN/{Utils.GetVersion(false)}";
                    txtCustomUA.Visibility = Visibility.Collapsed;
                    break;
                case "Hiddify":
                    _config.CoreBasicItem.DefUserAgent = "HiddifyNext/Android";
                    txtCustomUA.Visibility = Visibility.Collapsed;
                    break;
                case "Custom":
                    txtCustomUA.Visibility = Visibility.Visible;
                    break;
            }
            await ConfigHandler.SaveConfig(_config);
        }
    }

    private async void TxtCustomUA_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing) return;
        _config.CoreBasicItem.DefUserAgent = txtCustomUA.Text.Trim();
        await ConfigHandler.SaveConfig(_config);
    }

    private void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        (Application.Current.MainWindow as MainWindow)?.MenuCheckUpdate_Click(null, null);
    }

    private void BtnBackupRestore_Click(object sender, RoutedEventArgs e)
    {
        (Application.Current.MainWindow as MainWindow)?.MenuBackupAndRestore_Click(null, null);
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        (Application.Current.MainWindow as MainWindow)?.NavigateTo(SailaPage.Home);
    }

    public void ApplyThemeStyle()
    {
        string look = _config.UiItem.SailaLook ?? "Nexus";
        string accentHex = _config.UiItem.SailaAccent ?? "#7AB0FF";
        Color accentColor = SailaHomeView.ParseColor(accentHex);
        var accentBrush = new SolidColorBrush(accentColor);
        accentBrush.Freeze();

        txtLocalPort.Foreground = accentBrush;
        UpdateLookButtons(look);
        BuildAccentCircles();
        UpdateModeButtons(StatusBarViewModel.Instance.EnableTun);

        Brush cardBg;
        Brush cardBorder;
        Brush textPrimary;
        Brush textSecondary;
        Brush sepBrush;
        Brush containerBg;

        switch (look)
        {
            case "White":
                cardBg = Brushes.White;
                cardBorder = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xF0));
                textPrimary = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
                textSecondary = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
                sepBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xF0));
                containerBg = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));

                borderBack.Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                borderBack.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xF0));
                iconBack.Foreground = textPrimary;
                txtTitle.Foreground = textPrimary;
                break;

            case "Glass":
                cardBg = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                cardBorder = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255));
                textPrimary = Brushes.White;
                textSecondary = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
                sepBrush = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                containerBg = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));

                borderBack.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                borderBack.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                iconBack.Foreground = Brushes.White;
                txtTitle.Foreground = Brushes.White;
                break;

            case "Black":
                cardBg = new SolidColorBrush(Color.FromRgb(0x14, 0x15, 0x19));
                cardBorder = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x33));
                textPrimary = Brushes.White;
                textSecondary = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                sepBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x33));
                containerBg = new SolidColorBrush(Color.FromRgb(0x1F, 0x22, 0x2A));

                borderBack.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x22, 0x2A));
                borderBack.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x33));
                iconBack.Foreground = Brushes.White;
                txtTitle.Foreground = Brushes.White;
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
                sepBrush = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                containerBg = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));

                borderBack.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                borderBack.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                iconBack.Foreground = Brushes.White;
                txtTitle.Foreground = Brushes.White;
                break;
        }

        cardBg.Freeze();
        cardBorder.Freeze();

        cardConnection.Background = cardBg;
        cardConnection.BorderBrush = cardBorder;
        cardAppearance.Background = cardBg;
        cardAppearance.BorderBrush = cardBorder;
        cardSubscription.Background = cardBg;
        cardSubscription.BorderBrush = cardBorder;
        cardMore.Background = cardBg;
        cardMore.BorderBrush = cardBorder;

        txtConnectionTitle.Foreground = textPrimary;
        txtAppearanceTitle.Foreground = textPrimary;
        txtSubscriptionTitle.Foreground = textPrimary;
        txtMoreTitle.Foreground = textPrimary;

        txtModeLabel.Foreground = textPrimary;
        txtRoutingLabel.Foreground = textPrimary;
        txtAutoRunLabel.Foreground = textPrimary;
        txtAutoRoutingLabel.Foreground = textPrimary;
        txtAutoRoutingStatus.Foreground = textSecondary;
        txtLocalPortLabel.Foreground = textPrimary;

        txtLookTitle.Foreground = textSecondary;
        txtAccentTitle.Foreground = textSecondary;
        txtLangLabel.Foreground = textPrimary;
        txtUALabel.Foreground = textPrimary;

        borderModeContainer.Background = containerBg;
        borderLookContainer.Background = containerBg;

        cmbRouting.Foreground = textPrimary;
        cmbLanguage.Foreground = textPrimary;
        cmbUserAgent.Foreground = textPrimary;

        txtCustomUA.Foreground = textPrimary;
        if (look == "White")
        {
            txtCustomUA.Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
        }
        else
        {
            txtCustomUA.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
        }

        txtCheckUpdate.Foreground = textPrimary;
        txtBackupRestore.Foreground = textPrimary;
        iconChev1.Foreground = textSecondary;
        iconChev2.Foreground = textSecondary;

        sep1.Background = sepBrush;
        sep2.Background = sepBrush;
        sepAutoRouting.Background = sepBrush;
        sep3.Background = sepBrush;
        sep4.Background = sepBrush;
        sep5.Background = sepBrush;
        sep7.Background = sepBrush;

        txtAbout.Foreground = textSecondary;
    }
}
