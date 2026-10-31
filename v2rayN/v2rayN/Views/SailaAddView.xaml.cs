using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ServiceLib.Common;
using ServiceLib.Handler;
using ServiceLib.Manager;
using ServiceLib.Models.Configs;
using ServiceLib.Models.Entities;
using ServiceLib.Resx;
using ServiceLib.ViewModels;

namespace v2rayN.Views;

public partial class SailaAddView : UserControl
{
    private static Config _config => AppManager.Instance.Config;
    private Border borderBack => SailaTemplate.Part<Border>(btnBack, "borderBack");
    private MaterialDesignThemes.Wpf.PackIcon iconBack => SailaTemplate.Part<MaterialDesignThemes.Wpf.PackIcon>(btnBack, "iconBack");
    private Border borderPaste => SailaTemplate.Part<Border>(btnPaste, "borderPaste");
    private MaterialDesignThemes.Wpf.PackIcon iconPaste => SailaTemplate.Part<MaterialDesignThemes.Wpf.PackIcon>(btnPaste, "iconPaste");
    private TextBlock txtPaste => SailaTemplate.Part<TextBlock>(btnPaste, "txtPaste");
    private Border borderScan => SailaTemplate.Part<Border>(btnScan, "borderScan");
    private MaterialDesignThemes.Wpf.PackIcon iconScan => SailaTemplate.Part<MaterialDesignThemes.Wpf.PackIcon>(btnScan, "iconScan");
    private TextBlock txtScan => SailaTemplate.Part<TextBlock>(btnScan, "txtScan");

    public SailaAddView()
    {
        InitializeComponent();

        Loaded += (s, e) => ApplyThemeStyle();
        IsVisibleChanged += (s, e) =>
        {
            if (IsVisible)
            {
                ApplyThemeStyle();
                txtFeedback.Text = string.Empty;
            }
        };
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        (Application.Current.MainWindow as MainWindow)?.NavigateTo(SailaPage.Home);
    }

    private void BtnPaste_Click(object sender, RoutedEventArgs e)
    {
        var clip = WindowsUtils.GetClipboardData();
        if (!string.IsNullOrWhiteSpace(clip))
        {
            txtInput.Text = clip.Trim();
        }
    }

    private async void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mw)
        {
            await mw.ScanScreenTaskAsync();
            mw.NavigateTo(SailaPage.Servers);
        }
    }

    private async void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var rawText = txtInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawText))
        {
            txtFeedback.Text = ResUI.saila_paste_hint;
            txtFeedback.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            return;
        }

        try
        {
            // If it's an HTTP/HTTPS URL -> add/update as subscription
            if (Uri.TryCreate(rawText, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                string cleanUrl = rawText.Trim();
                var existingSubs = await AppManager.Instance.SubItems();
                var existing = existingSubs?.FirstOrDefault(s => string.Equals((s.Url ?? "").Trim(), cleanUrl, StringComparison.OrdinalIgnoreCase));

                string targetSubId;
                if (existing != null)
                {
                    targetSubId = existing.Id;
                    txtFeedback.Text = "Already added — updated";
                    txtFeedback.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
                }
                else
                {
                    var subItem = new SubItem
                    {
                        Id = Utils.GetGuid(false),
                        Url = cleanUrl,
                        Remarks = uri.Host,
                        Enabled = true
                    };

                    await ConfigHandler.AddSubItem(_config, subItem);
                    targetSubId = subItem.Id;

                    txtFeedback.Text = ResUI.saila_add_success;
                    txtFeedback.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
                }

                // Trigger subscription update
                if (Application.Current.MainWindow is MainWindow mw && mw.ViewModel != null)
                {
                    await mw.ViewModel.UpdateSubscriptionProcess(targetSubId, false);
                }
                else
                {
                    StatusBarViewModel.Instance.SubUpdateCmd.Execute().Subscribe();
                }

                await Task.Delay(600);
                txtInput.Clear();
                txtFeedback.Text = string.Empty;
                (Application.Current.MainWindow as MainWindow)?.NavigateTo(SailaPage.Servers);
            }
            else
            {
                // Otherwise import as configs
                int count = await ConfigHandler.AddBatchServers(_config, rawText, "", true);
                if (count > 0)
                {
                    txtFeedback.Text = $"{ResUI.saila_add_success} ({count})";
                    txtFeedback.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));

                    await Task.Delay(600);
                    txtInput.Clear();
                    txtFeedback.Text = string.Empty;
                    (Application.Current.MainWindow as MainWindow)?.NavigateTo(SailaPage.Servers);
                }
                else
                {
                    txtFeedback.Text = ResUI.saila_add_failed;
                    txtFeedback.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                }
            }
        }
        catch (Exception ex)
        {
            txtFeedback.Text = $"{ResUI.saila_add_failed}: {ex.Message}";
            txtFeedback.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
        }
    }

    public void ApplyThemeStyle()
    {
        string look = _config.UiItem.SailaLook ?? "Nexus";
        string accentHex = _config.UiItem.SailaAccent ?? "#7AB0FF";
        Color accentColor = SailaHomeView.ParseColor(accentHex);
        Color deeperAccent = SailaHomeView.Deeper(accentColor, 0.25f);
        var accentGrad = new LinearGradientBrush(accentColor, deeperAccent, new Point(0, 0), new Point(1, 1));
        accentGrad.Freeze();

        btnAdd.Background = accentGrad;

        Brush cardBg;
        Brush cardBorder;
        Brush textPrimary;
        Brush textSecondary;
        Brush inputBg;
        Brush inputBorder;
        Brush secBtnBg;
        Brush secBtnBorder;

        switch (look)
        {
            case "White":
                cardBg = Brushes.White;
                cardBorder = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xF0));
                textPrimary = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
                textSecondary = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));

                borderBack.Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                borderBack.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xF0));
                iconBack.Foreground = textPrimary;
                txtTitle.Foreground = textPrimary;

                inputBg = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
                inputBorder = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xF0));

                secBtnBg = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                secBtnBorder = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xF0));
                break;

            case "Glass":
                cardBg = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                cardBorder = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255));
                textPrimary = Brushes.White;
                textSecondary = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));

                borderBack.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                borderBack.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                iconBack.Foreground = Brushes.White;
                txtTitle.Foreground = Brushes.White;

                inputBg = new SolidColorBrush(Color.FromArgb(37, 255, 255, 255));
                inputBorder = new SolidColorBrush(Color.FromArgb(53, 255, 255, 255));

                secBtnBg = new SolidColorBrush(Color.FromArgb(37, 255, 255, 255));
                secBtnBorder = new SolidColorBrush(Color.FromArgb(53, 255, 255, 255));
                break;

            case "Black":
                cardBg = new SolidColorBrush(Color.FromRgb(0x14, 0x15, 0x19));
                cardBorder = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x33));
                textPrimary = Brushes.White;
                textSecondary = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

                borderBack.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x22, 0x2A));
                borderBack.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x33));
                iconBack.Foreground = Brushes.White;
                txtTitle.Foreground = Brushes.White;

                inputBg = new SolidColorBrush(Color.FromRgb(0x1F, 0x22, 0x2A));
                inputBorder = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x33));

                secBtnBg = new SolidColorBrush(Color.FromRgb(0x1F, 0x22, 0x2A));
                secBtnBorder = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x33));
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

                borderBack.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                borderBack.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                iconBack.Foreground = Brushes.White;
                txtTitle.Foreground = Brushes.White;

                inputBg = new SolidColorBrush(Color.FromArgb(37, 255, 255, 255));
                inputBorder = new SolidColorBrush(Color.FromArgb(53, 255, 255, 255));

                secBtnBg = new SolidColorBrush(Color.FromArgb(37, 255, 255, 255));
                secBtnBorder = new SolidColorBrush(Color.FromArgb(53, 255, 255, 255));
                break;
        }

        cardBg.Freeze();
        cardBorder.Freeze();

        cardMain.Background = cardBg;
        cardMain.BorderBrush = cardBorder;

        borderInput.Background = inputBg;
        borderInput.BorderBrush = inputBorder;
        txtInput.Foreground = textPrimary;

        borderPaste.Background = secBtnBg;
        borderPaste.BorderBrush = secBtnBorder;
        iconPaste.Foreground = textSecondary;
        txtPaste.Foreground = textPrimary;

        borderScan.Background = secBtnBg;
        borderScan.BorderBrush = secBtnBorder;
        iconScan.Foreground = textSecondary;
        txtScan.Foreground = textPrimary;
    }
}
