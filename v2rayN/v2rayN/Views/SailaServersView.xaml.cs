using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ReactiveUI;
using ServiceLib.Common;
using ServiceLib.Enums;
using ServiceLib.Handler;
using ServiceLib.Manager;
using ServiceLib.Models.Configs;
using ServiceLib.Models.Dto;
using ServiceLib.Resx;
using ServiceLib.ViewModels;

namespace v2rayN.Views;

public partial class SailaServersView : UserControl
{
    private static Config _config => AppManager.Instance.Config;
    private string _selectedSubId = string.Empty;
    private List<ServerDisplayItem> _allItems = [];
    private IDisposable? _reloadSubscription;

    public SailaServersView()
    {
        InitializeComponent();

        Loaded += SailaServersView_Loaded;
        Unloaded += SailaServersView_Unloaded;
        IsVisibleChanged += SailaServersView_IsVisibleChanged;
    }

    private void SailaServersView_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyThemeStyle();
        _ = LoadServersAsync();

        _reloadSubscription?.Dispose();
        _reloadSubscription = AppEvents.DispatcherStatisticsRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ =>
            {
                UpdateDelayChips();
            });
    }

    private void SailaServersView_Unloaded(object sender, RoutedEventArgs e)
    {
        _reloadSubscription?.Dispose();
    }

    private void SailaServersView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            ApplyThemeStyle();
            _ = LoadServersAsync();
        }
    }

    public async Task LoadServersAsync()
    {
        try
        {
            await LoadChipsAsync();

            var rawModels = await AppManager.Instance.ProfileModels(_selectedSubId, txtSearch.Text.Trim());
            var items = new List<ServerDisplayItem>();

            string activeId = _config.IndexId;

            string look = _config.UiItem.SailaLook ?? "Nexus";
            string accentHex = _config.UiItem.SailaAccent ?? "#7AB0FF";
            Color accentColor = SailaHomeView.ParseColor(accentHex);
            var accentBrush = new SolidColorBrush(accentColor);
            accentBrush.Freeze();

            foreach (var m in rawModels)
            {
                var remarks = m.Remarks.IsNullOrEmpty() ? m.Address : m.Remarks;
                var (cc, initials, cleanRemarks) = SailaFlagHelper.ParseName(remarks);

                string net = m.Network.IsNullOrEmpty() ? "tcp" : m.Network;
                string sec = m.StreamSecurity.IsNullOrEmpty() ? string.Empty : $" · {m.StreamSecurity}";
                string subLine = $"{m.ConfigType} · {net}{sec}";

                bool isActive = m.IndexId == activeId;

                var displayItem = new ServerDisplayItem
                {
                    Model = m,
                    CountryCode = cc,
                    AvatarText = initials,
                    CleanRemarks = cleanRemarks,
                    SubtitleLine = subLine,
                    IsActive = isActive
                };

                displayItem.UpdateStyling(look, accentBrush);
                items.Add(displayItem);
            }

            _allItems = items;
            listServers.ItemsSource = _allItems;

            bool isEmpty = _allItems.Count == 0;
            panelEmpty.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
            scrollServers.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;

            // Load flag images asynchronously in the background
            _ = Task.Run(async () =>
            {
                foreach (var it in items)
                {
                    if (!string.IsNullOrEmpty(it.CountryCode))
                    {
                        var flag = await SailaFlagHelper.GetFlagAsync(it.CountryCode);
                        if (flag != null)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                it.FlagImage = flag;
                                it.RaisePropertyChanged(nameof(it.FlagImage));
                                it.RaisePropertyChanged(nameof(it.FlagVisibility));
                                it.RaisePropertyChanged(nameof(it.AvatarTextVisibility));
                            });
                        }
                    }
                }
            });
        }
        catch
        {
        }
    }

    private void UpdateDelayChips()
    {
        if (_allItems == null || _allItems.Count == 0) return;
        foreach (var it in _allItems)
        {
            it.UpdatePing();
        }
    }

    private async Task LoadChipsAsync()
    {
        panelChips.Children.Clear();

        string look = _config.UiItem.SailaLook ?? "Nexus";
        string accentHex = _config.UiItem.SailaAccent ?? "#7AB0FF";
        Color accentColor = SailaHomeView.ParseColor(accentHex);
        var accentBrush = new SolidColorBrush(accentColor);
        accentBrush.Freeze();

        // 1. "All" Chip
        var btnAll = CreateChip(ResUI.saila_all_servers, string.Empty, _selectedSubId.IsNullOrEmpty(), accentBrush, look);
        panelChips.Children.Add(btnAll);

        // 2. Subscription Chips - distinct by URL or ID (no duplicates)
        var subs = await AppManager.Instance.SubItems();
        if (subs != null)
        {
            var distinctSubs = subs
                .Where(s => s.Enabled)
                .GroupBy(s => string.IsNullOrWhiteSpace(s.Url) ? s.Id : s.Url.Trim().ToLowerInvariant())
                .Select(g => g.First())
                .ToList();

            foreach (var sub in distinctSubs)
            {
                string title = sub.Remarks.IsNullOrEmpty() ? sub.Url : sub.Remarks;
                bool isSel = sub.Id == _selectedSubId;
                var btn = CreateChip(title, sub.Id, isSel, accentBrush, look);
                panelChips.Children.Add(btn);
            }
        }
    }

    private Button CreateChip(string text, string subId, bool isSelected, Brush accentBrush, string look)
    {
        var btn = new Button
        {
            Margin = new Thickness(0, 0, 8, 0),
            Height = 30,
            Padding = new Thickness(14, 0, 14, 0),
            Cursor = Cursors.Hand,
            Tag = subId,
            Style = (Style)FindResource("MaterialDesignFlatButton")
        };

        Brush bg;
        Brush borderBrush;
        Brush fg;

        if (isSelected)
        {
            bg = accentBrush;
            borderBrush = Brushes.Transparent;
            fg = Brushes.White;
        }
        else
        {
            if (look == "White")
            {
                bg = Brushes.White;
                borderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xF0));
                fg = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
            }
            else if (look == "Black")
            {
                bg = new SolidColorBrush(Color.FromRgb(0x1F, 0x22, 0x2A));
                borderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x33));
                fg = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
            }
            else
            {
                bg = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                borderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                fg = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
            }
        }

        var border = new Border
        {
            CornerRadius = new CornerRadius(15),
            Padding = new Thickness(14, 0, 14, 0),
            Background = bg,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1)
        };

        var tb = new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = fg,
            VerticalAlignment = VerticalAlignment.Center
        };

        border.Child = tb;

        var template = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(ContentPresenter));
        template.VisualTree = factory;
        btn.Template = template;
        btn.Content = border;

        btn.Click += async (s, e) =>
        {
            _selectedSubId = subId;
            await LoadServersAsync();
        };

        return btn;
    }

    private async void ServerRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ServerDisplayItem item)
        {
            if (Application.Current.MainWindow is MainWindow mw && mw.ViewModel?.ProfilesViewModel != null)
            {
                mw.ViewModel.ProfilesViewModel.SelectedProfile = item.Model;
                _config.IndexId = item.Model.IndexId;
                await mw.ViewModel.ProfilesViewModel.SetDefaultServer(item.Model.IndexId);
                mw.NavigateTo(SailaPage.Home);
                await mw.viewHome.RefreshServerInfoAsync(item.Model);
            }
        }
    }

    private async void MenuEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is ServerDisplayItem item)
        {
            if (Application.Current.MainWindow is MainWindow mw && mw.ViewModel?.ProfilesViewModel != null)
            {
                mw.ViewModel.ProfilesViewModel.SelectedProfile = item.Model;
                await mw.ViewModel.ProfilesViewModel.EditServerAsync();
            }
        }
    }

    private async void MenuShare_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is ServerDisplayItem item)
        {
            if (Application.Current.MainWindow is MainWindow mw && mw.ViewModel?.ProfilesViewModel != null)
            {
                mw.ViewModel.ProfilesViewModel.SelectedProfile = item.Model;
                await mw.ViewModel.ProfilesViewModel.ShareServerAsync();
            }
        }
    }

    private async void MenuDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is ServerDisplayItem item)
        {
            if (Application.Current.MainWindow is MainWindow mw && mw.ViewModel?.ProfilesViewModel != null)
            {
                mw.ViewModel.ProfilesViewModel.SelectedProfile = item.Model;
                await mw.ViewModel.ProfilesViewModel.RemoveServerAsync();
                await LoadServersAsync();
            }
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        (Application.Current.MainWindow as MainWindow)?.NavigateTo(SailaPage.Home);
    }

    private void BtnEmptyAdd_Click(object sender, RoutedEventArgs e)
    {
        (Application.Current.MainWindow as MainWindow)?.NavigateTo(SailaPage.Add);
    }

    private async void BtnTestAll_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mw && mw.ViewModel?.ProfilesViewModel != null)
        {
            await mw.ViewModel.ProfilesViewModel.ServerSpeedtest(ESpeedActionType.Realping);
        }
    }

    private void BtnUpdateSubs_Click(object sender, RoutedEventArgs e)
    {
        StatusBarViewModel.Instance.SubUpdateCmd.Execute().Subscribe();
        _ = LoadServersAsync();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _ = LoadServersAsync();
    }

    public void ApplyThemeStyle()
    {
        string look = _config.UiItem.SailaLook ?? "Nexus";
        string accentHex = _config.UiItem.SailaAccent ?? "#7AB0FF";
        Color accentColor = SailaHomeView.ParseColor(accentHex);
        var accentBrush = new SolidColorBrush(accentColor);
        accentBrush.Freeze();

        Brush textPrimary;
        Brush textSecondary;

        switch (look)
        {
            case "White":
                textPrimary = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
                textSecondary = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));

                borderBack.Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                borderBack.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xF0));
                iconBack.Foreground = textPrimary;
                txtTitle.Foreground = textPrimary;

                borderTestAll.Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                borderTestAll.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xF0));
                borderUpdateSubs.Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                borderUpdateSubs.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xF0));

                borderSearch.Background = Brushes.White;
                borderSearch.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xF0));
                iconSearch.Foreground = textSecondary;
                txtSearch.Foreground = textPrimary;

                borderEmpty.Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                iconEmpty.Foreground = textSecondary;
                txtEmpty.Foreground = textSecondary;
                break;

            case "Glass":
                textPrimary = Brushes.White;
                textSecondary = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));

                borderBack.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                borderBack.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                iconBack.Foreground = Brushes.White;
                txtTitle.Foreground = Brushes.White;

                borderTestAll.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                borderTestAll.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                borderUpdateSubs.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                borderUpdateSubs.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));

                borderSearch.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                borderSearch.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                iconSearch.Foreground = textSecondary;
                txtSearch.Foreground = Brushes.White;

                borderEmpty.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                iconEmpty.Foreground = textSecondary;
                txtEmpty.Foreground = textSecondary;
                break;

            case "Black":
                textPrimary = Brushes.White;
                textSecondary = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

                borderBack.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x22, 0x2A));
                borderBack.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x33));
                iconBack.Foreground = Brushes.White;
                txtTitle.Foreground = Brushes.White;

                borderTestAll.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x22, 0x2A));
                borderTestAll.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x33));
                borderUpdateSubs.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x22, 0x2A));
                borderUpdateSubs.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x33));

                borderSearch.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x22, 0x2A));
                borderSearch.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x33));
                iconSearch.Foreground = textSecondary;
                txtSearch.Foreground = Brushes.White;

                borderEmpty.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x22, 0x2A));
                iconEmpty.Foreground = textSecondary;
                txtEmpty.Foreground = textSecondary;
                break;

            case "Nexus":
            default:
                textPrimary = Brushes.White;
                textSecondary = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

                borderBack.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                borderBack.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                iconBack.Foreground = Brushes.White;
                txtTitle.Foreground = Brushes.White;

                borderTestAll.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                borderTestAll.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                borderUpdateSubs.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                borderUpdateSubs.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));

                borderSearch.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                borderSearch.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                iconSearch.Foreground = textSecondary;
                txtSearch.Foreground = Brushes.White;

                borderEmpty.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                iconEmpty.Foreground = textSecondary;
                txtEmpty.Foreground = textSecondary;
                break;
        }

        btnTestAll.Foreground = accentBrush;
        btnUpdateSubs.Foreground = accentBrush;
        btnEmptyAdd.Tag = accentBrush;

        if (_allItems != null)
        {
            foreach (var it in _allItems)
            {
                it.UpdateStyling(look, accentBrush);
            }
        }
    }
}

public class ServerDisplayItem : ReactiveObject
{
    public ProfileItemModel Model { get; set; } = new();
    public string? CountryCode { get; set; }
    public string AvatarText { get; set; } = "SL";
    public string CleanRemarks { get; set; } = string.Empty;
    public string SubtitleLine { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public ImageSource? FlagImage { get; set; }
    public Visibility FlagVisibility => FlagImage != null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AvatarTextVisibility => FlagImage == null ? Visibility.Visible : Visibility.Collapsed;

    public string PingText { get; set; } = "-";
    public Brush PingBgBrush { get; set; } = Brushes.Transparent;
    public Brush PingBorderBrush { get; set; } = Brushes.Transparent;
    public Brush PingFgBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

    public Brush CardBgBrush { get; set; } = Brushes.Transparent;
    public Brush CardBorderBrush { get; set; } = Brushes.Transparent;
    public Thickness CardBorderThickness { get; set; } = new Thickness(1);

    public Brush TextPrimaryBrush { get; set; } = Brushes.White;
    public Brush TextSecondaryBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
    public Brush AvatarBgBrush { get; set; } = new SolidColorBrush(Color.FromArgb(37, 255, 255, 255));
    public Brush AvatarBorderBrush { get; set; } = new SolidColorBrush(Color.FromArgb(53, 255, 255, 255));

    public void UpdatePing()
    {
        int delay = Model.Delay;
        if (delay > 0)
        {
            PingText = $"{delay} ms";
            PingFgBrush = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
            PingBgBrush = new SolidColorBrush(Color.FromArgb(36, 0x22, 0xC5, 0x5E));
            PingBorderBrush = new SolidColorBrush(Color.FromArgb(70, 0x22, 0xC5, 0x5E));
        }
        else if (delay < 0)
        {
            PingText = "✕";
            PingFgBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            PingBgBrush = new SolidColorBrush(Color.FromArgb(36, 0xEF, 0x44, 0x44));
            PingBorderBrush = new SolidColorBrush(Color.FromArgb(70, 0xEF, 0x44, 0x44));
        }
        else
        {
            PingText = "-";
            PingFgBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            PingBgBrush = Brushes.Transparent;
            PingBorderBrush = Brushes.Transparent;
        }

        this.RaisePropertyChanged(nameof(PingText));
        this.RaisePropertyChanged(nameof(PingFgBrush));
        this.RaisePropertyChanged(nameof(PingBgBrush));
        this.RaisePropertyChanged(nameof(PingBorderBrush));
    }

    public void UpdateStyling(string look, Brush accentBrush)
    {
        UpdatePing();

        switch (look)
        {
            case "White":
                TextPrimaryBrush = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
                TextSecondaryBrush = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
                AvatarBgBrush = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                AvatarBorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xF0));
                CardBgBrush = Brushes.White;
                CardBorderBrush = IsActive ? accentBrush : new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xF0));
                CardBorderThickness = new Thickness(IsActive ? 1.5 : 1);
                break;

            case "Glass":
                TextPrimaryBrush = Brushes.White;
                TextSecondaryBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
                AvatarBgBrush = new SolidColorBrush(Color.FromArgb(37, 255, 255, 255));
                AvatarBorderBrush = new SolidColorBrush(Color.FromArgb(53, 255, 255, 255));
                CardBgBrush = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                CardBorderBrush = IsActive ? accentBrush : new SolidColorBrush(Color.FromArgb(64, 255, 255, 255));
                CardBorderThickness = new Thickness(IsActive ? 1.5 : 1);
                break;

            case "Black":
                TextPrimaryBrush = Brushes.White;
                TextSecondaryBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                AvatarBgBrush = new SolidColorBrush(Color.FromRgb(0x1F, 0x22, 0x2A));
                AvatarBorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x33));
                CardBgBrush = new SolidColorBrush(Color.FromRgb(0x14, 0x15, 0x19));
                CardBorderBrush = IsActive ? accentBrush : new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x33));
                CardBorderThickness = new Thickness(IsActive ? 1.5 : 1);
                break;

            case "Nexus":
            default:
                TextPrimaryBrush = Brushes.White;
                TextSecondaryBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                AvatarBgBrush = new SolidColorBrush(Color.FromArgb(37, 255, 255, 255));
                AvatarBorderBrush = new SolidColorBrush(Color.FromArgb(53, 255, 255, 255));
                var nexusGrad = new LinearGradientBrush(
                    Color.FromArgb(192, 0x16, 0x20, 0x3E),
                    Color.FromArgb(176, 0x0A, 0x10, 0x22),
                    new Point(0, 0), new Point(0, 1));
                nexusGrad.Freeze();
                CardBgBrush = nexusGrad;
                CardBorderBrush = IsActive ? accentBrush : new SolidColorBrush(Color.FromArgb(53, 0x82, 0xA0, 0xFF));
                CardBorderThickness = new Thickness(IsActive ? 1.5 : 1);
                break;
        }

        this.RaisePropertyChanged(nameof(TextPrimaryBrush));
        this.RaisePropertyChanged(nameof(TextSecondaryBrush));
        this.RaisePropertyChanged(nameof(AvatarBgBrush));
        this.RaisePropertyChanged(nameof(AvatarBorderBrush));
        this.RaisePropertyChanged(nameof(CardBgBrush));
        this.RaisePropertyChanged(nameof(CardBorderBrush));
        this.RaisePropertyChanged(nameof(CardBorderThickness));
    }
}
