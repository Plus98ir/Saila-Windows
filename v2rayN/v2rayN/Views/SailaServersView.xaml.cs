using System.Windows;
using System.Windows.Automation;
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
using ServiceLib.Services;
using ServiceLib.ViewModels;

namespace v2rayN.Views;

public partial class SailaServersView : UserControl
{
    private static Config _config => AppManager.Instance.Config;
    private Border borderBack => SailaTemplate.Part<Border>(btnBack, "borderBack");
    private MaterialDesignThemes.Wpf.PackIcon iconBack => SailaTemplate.Part<MaterialDesignThemes.Wpf.PackIcon>(btnBack, "iconBack");
    private Border borderTestAll => SailaTemplate.Part<Border>(btnTestAll, "borderTestAll");
    private Border borderUpdateSubs => SailaTemplate.Part<Border>(btnUpdateSubs, "borderUpdateSubs");
    private Border borderColumns => SailaTemplate.Part<Border>(btnColumns, "borderColumns");
    private MaterialDesignThemes.Wpf.PackIcon iconColumns => SailaTemplate.Part<MaterialDesignThemes.Wpf.PackIcon>(btnColumns, "iconColumns");
    private string _selectedSubId = string.Empty;
    private List<ServerDisplayItem> _allItems = [];
    private IDisposable? _reloadSubscription;
    private SpeedtestService? _speedtest;
    private bool _sortedByPing;
    private bool _wide;
    private int _appliedColumns;

    public SailaServersView()
    {
        InitializeComponent();
#if SAILA_TEST
        AddTestEditButton();
#endif

        Loaded += SailaServersView_Loaded;
        Unloaded += SailaServersView_Unloaded;
        IsVisibleChanged += SailaServersView_IsVisibleChanged;
        SizeChanged += (_, _) =>
        {
            if (_wide)
            {
                ApplyColumns();
            }
        };
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

    public string SelectedSubId => _selectedSubId;

    public void SetWide(bool wide)
    {
        _wide = wide;
        rootPage.Margin = wide ? new Thickness(20, 0, 20, 12) : new Thickness(16, 14, 16, 104);
        btnBack.Visibility = wide ? Visibility.Collapsed : Visibility.Visible;
        btnColumns.Visibility = wide ? Visibility.Collapsed : Visibility.Visible;
        txtTitle.Margin = wide ? new Thickness(0) : new Thickness(12, 0, 0, 0);
        ApplyColumns();
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

            // ProfileModels carries no delay; take the last test results so pings survive a page change.
            var delays = (await ProfileExManager.Instance.GetProfileExs())
                .GroupBy(x => x.IndexId)
                .ToDictionary(g => g.Key, g => g.First().Delay);

            foreach (var m in rawModels)
            {
                if (delays.TryGetValue(m.IndexId, out var delay))
                {
                    m.Delay = delay;
                }
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

            _allItems = _sortedByPing ? items.OrderBy(PingKey).ToList() : items;
            listServers.ItemsSource = _allItems;

            bool isEmpty = _allItems.Count == 0;
            panelEmpty.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
            scrollServers.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;

            // Load flag images asynchronously in the background
            _ = Task.Run(async () =>
            {
                await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = 6 }, async (it, _) =>
                {
                    it.CountryCode = await SailaFlagHelper.ResolveCountryAsync(it.CountryCode, it.Model.Address);
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
                });
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
        // Read the subs before touching the panel: two overlapping calls used to both Clear() and then both add.
        var subs = await AppManager.Instance.SubItems();
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
        if (subs != null)
        {
            var distinctSubs = subs
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

        // Add a subscription, or a group without a link for single configs; edit the selected one.
        panelChips.Children.Add(CreateChip($"+ {ResUI.saila_subscription}", string.Empty, false, accentBrush, look, () => EditSubAsync(new SubItem { Enabled = true }), "btnSubAdd"));
        if (subs?.FirstOrDefault(t => t.Id == _selectedSubId) is { } selected)
        {
            panelChips.Children.Add(CreateChip(ResUI.menuSubEdit, string.Empty, false, accentBrush, look, () => EditSubAsync(selected), "btnSubEdit"));
        }
    }

    private async Task EditSubAsync(SubItem item)
    {
        if (Application.Current.MainWindow is not MainWindow mw)
        {
            return;
        }
        var isNew = item.Id.IsNullOrEmpty();
        var oldUrl = item.Url;
        var vm = new SubEditViewModel(item);
        var saved = await mw.ShowEditPageAsync(vm);
        if (saved)
        {
            _selectedSubId = vm.SelectedSource.Id;
            if (vm.SelectedSource.Url.IsNotEmpty() && (isNew || vm.SelectedSource.Url != oldUrl) && mw.ViewModel != null)
            {
                await mw.ViewModel.UpdateSubscriptionProcess(vm.SelectedSource.Id, false);
            }
        }
        await AfterSubsChangedAsync(mw);
    }

    private async Task AfterSubsChangedAsync(MainWindow mw)
    {
        var subs = await AppManager.Instance.SubItems();
        if (_selectedSubId.IsNotEmpty() && subs?.Any(t => t.Id == _selectedSubId) != true)
        {
            _selectedSubId = string.Empty; // it was deleted
        }
        // The active server went with a deleted subscription: pick another and restart the core on it.
        if (_config.IndexId.IsNotEmpty() && await AppManager.Instance.GetProfileItem(_config.IndexId) == null)
        {
            await ConfigHandler.GetDefaultServer(_config);
            mw.ViewModel?.Reload();
        }
        await LoadServersAsync();
        await mw.viewHome.RefreshServerInfoAsync();
    }

    private async void MenuMove_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menu || menu.DataContext is not ServerDisplayItem item)
        {
            return;
        }
        var subs = await AppManager.Instance.SubItems();
        menu.Items.Clear();
        menu.Items.Add(MoveItem(ResUI.saila_no_group, string.Empty, item));
        foreach (var sub in subs ?? [])
        {
            menu.Items.Add(MoveItem(sub.Remarks.IsNullOrEmpty() ? sub.Url : sub.Remarks, sub.Id, item));
        }
    }

    private MenuItem MoveItem(string title, string subId, ServerDisplayItem item)
    {
        var entry = new MenuItem { Header = title, IsChecked = (item.Model.Subid ?? string.Empty) == subId };
        entry.Click += async (_, _) => await MoveAsync(item, subId);
        return entry;
    }

    private async Task MoveAsync(ServerDisplayItem item, string subId)
    {
        var profile = await AppManager.Instance.GetProfileItem(item.Model.IndexId);
        if (profile == null)
        {
            return;
        }
        // Not a subscription's own row any more: updating the target subscription must not delete it.
        profile.IsSub = false;
        await ConfigHandler.MoveToGroup(_config, [profile], subId);
        await LoadServersAsync();
    }

    private Button CreateChip(string text, string subId, bool isSelected, Brush accentBrush, string look, Func<Task>? action = null, string? name = null)
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

        AutomationProperties.SetName(btn, text);
        if (name != null)
        {
            btn.Name = name;
        }
        btn.Click += async (s, e) =>
        {
            if (action != null)
            {
                await action();
                return;
            }
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
                await mw.viewHome.RefreshServerInfoAsync();
                if (mw.IsWide)
                {
                    // The list stays open beside home: move the highlight.
                    foreach (var it in _allItems)
                    {
                        it.IsActive = it.Model.IndexId == item.Model.IndexId;
                    }
                    ApplyThemeStyle();
                }
            }
        }
    }

    private async void MenuEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is ServerDisplayItem item)
        {
            await EditAsync(item);
        }
    }

    private async Task EditAsync(ServerDisplayItem item)
    {
        if (Application.Current.MainWindow is MainWindow mw && mw.ViewModel?.ProfilesViewModel != null)
        {
            mw.ViewModel.ProfilesViewModel.SelectedProfile = item.Model;
            await mw.ViewModel.ProfilesViewModel.EditServerAsync();
            await LoadServersAsync();
            await mw.viewHome.RefreshServerInfoAsync();
        }
    }

#if SAILA_TEST
    // tmp\win-paneltest.ps1: synthetic right-clicks don't reach WPF, so tests edit the first row through this.
    private void AddTestEditButton()
    {
        var button = new Button { Name = "btnTestEdit", Width = 1, Height = 1, Opacity = 0, HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += async (_, _) =>
        {
            if (_allItems.Count > 0)
            {
                await EditAsync(_allItems[0]);
            }
        };
        rootPage.Children.Add(button);
        var custom = new Button { Name = "btnTestCustom", Width = 1, Height = 1, Opacity = 0, HorizontalAlignment = HorizontalAlignment.Right };
        custom.Click += async (_, _) =>
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                await mw.ShowEditPageAsync(new AddServer2ViewModel(new ProfileItem { ConfigType = EConfigType.Custom, Remarks = "AT Custom", Address = "custom.json" }));
            }
        };
        rootPage.Children.Add(custom);
        var move = new Button { Name = "btnTestMove", Width = 1, Height = 1, Opacity = 0, HorizontalAlignment = HorizontalAlignment.Center };
        move.Click += async (_, _) =>
        {
            var subs = await AppManager.Instance.SubItems();
            if (_allItems.Count > 0 && subs?.FirstOrDefault(t => t.Remarks == "Panel Sub") is { } target)
            {
                await MoveAsync(_allItems[0], target.Id);
            }
        };
        rootPage.Children.Add(move);
    }
#endif

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

    // Saila: tests the servers shown on this page. ProfilesViewModel.ServerSpeedtest only tests the rows
    // selected in v2rayN's hidden grid, so from here it tested nothing.
    private async void BtnTestAll_Click(object sender, RoutedEventArgs e)
    {
        if (_allItems.Count == 0) return;
        var byId = _allItems.ToDictionary(it => it.Model.IndexId);
        var profiles = await AppManager.Instance.GetProfileItemsOrderedByIndexIds(byId.Keys);
        if (profiles.Count == 0) return;

        foreach (var it in _allItems)
        {
            it.Model.Delay = 0;
            it.UpdatePing();
        }
        btnTestAll.IsEnabled = false;
        try
        {
            _speedtest ??= new SpeedtestService(_config, result =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (result.IndexId is { } id && byId.TryGetValue(id, out var item) && int.TryParse(result.Delay, out var ms))
                    {
                        item.Model.Delay = ms;
                        item.UpdatePing();
                    }
                });
                return Task.CompletedTask;
            });
            await _speedtest.RunLoop(ESpeedActionType.Realping, profiles);
            SortByPing();
        }
        finally
        {
            btnTestAll.IsEnabled = true;
        }
    }

    private void BtnSortPing_Click(object sender, RoutedEventArgs e) => SortByPing();

    // Fastest first; untested next, failed last. Display order only (the subscription's order is kept);
    // it stays on while Saila runs.
    private void SortByPing()
    {
        _sortedByPing = true;
        _allItems = _allItems.OrderBy(PingKey).ToList();
        listServers.ItemsSource = _allItems;
    }

    private static int PingKey(ServerDisplayItem it) => it.Model.Delay switch
    {
        > 0 and var d => d,
        0 => int.MaxValue - 1,
        _ => int.MaxValue,
    };

    // Saila: waits for the update to finish before reloading; reloading right away showed the old list.
    private async void BtnUpdateSubs_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is not MainWindow { ViewModel: { } vm }) return;
        btnUpdateSubs.IsEnabled = false;
        try
        {
            await vm.UpdateSubscriptionProcess(string.Empty, false);
            await LoadServersAsync();
        }
        finally
        {
            btnUpdateSubs.IsEnabled = true;
        }
    }

    private async void BtnColumns_Click(object sender, RoutedEventArgs e)
    {
        _config.UiItem.SailaServerColumns = _config.UiItem.SailaServerColumns == 2 ? 1 : 2;
        ApplyColumns();
        await ConfigHandler.SaveConfig(_config);
    }

    // One or two columns of server cards; the icon shows the layout a click switches to.
    private void ApplyColumns()
    {
        // Wide window: as many columns as fit. Narrow: the user's choice.
        var columns = _wide ? Math.Max(1, (int)(ActualWidth / 300)) : _config.UiItem.SailaServerColumns == 2 ? 2 : 1;
        if (columns != _appliedColumns)
        {
            _appliedColumns = columns;
            var panel = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.UniformGrid));
            panel.SetValue(System.Windows.Controls.Primitives.UniformGrid.ColumnsProperty, columns);
            panel.SetValue(VerticalAlignmentProperty, VerticalAlignment.Top);
            listServers.ItemsPanel = new ItemsPanelTemplate(panel);
        }
        if (iconColumns != null)
        {
            iconColumns.Kind = columns == 2 ? MaterialDesignThemes.Wpf.PackIconKind.ViewAgendaOutline : MaterialDesignThemes.Wpf.PackIconKind.ViewGridOutline;
        }
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
        btnColumns.Foreground = accentBrush;
        btnSortPing.Foreground = accentBrush;
        SailaTemplate.Part<Border>(btnSortPing, "borderSortPing").Background = borderUpdateSubs.Background;
        borderColumns.Background = borderUpdateSubs.Background;
        ApplyColumns();
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
