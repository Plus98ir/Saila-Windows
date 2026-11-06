using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using v2rayN.Base;
using v2rayN.Manager;

namespace v2rayN.Views;

public partial class MainWindow
{
    private static Config _config;
    private readonly SingleReplaceableDisposable _layoutBindingsDisposable = new();

    public MainWindow()
    {
        InitializeComponent();
        // Saila: resizable; below WideThreshold it becomes the one-page layout.
        MinHeight = Math.Min(MinHeight, SystemParameters.WorkArea.Height);
        dialogRoot.SizeChanged += (_, _) => RoundCorners();
        SizeChanged += MainWindow_SizeChanged;
        StateChanged += (_, _) =>
        {
            // Aero Snap can maximize it, but there is no restore button on the strip.
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
        };
        _saveSizeTimer.Tick += (_, _) =>
        {
            _saveSizeTimer.Stop();
            _ = ConfigHandler.SaveConfig(_config);
        };

        _config = AppManager.Instance.Config;
        ThreadPool.RegisterWaitForSingleObject(App.ProgramStarted, OnProgramStarted, null, -1, false);

        App.Current.SessionEnding += Current_SessionEnding;
        Closing += MainWindow_Closing;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        menuSettingsSetUWP.Click += MenuSettingsSetUWP_Click;
        menuPromotion.Click += MenuPromotion_Click;
        menuClose.Click += MenuClose_Click;
        menuCheckUpdate.Click += MenuCheckUpdate_Click;
        btnNewUpdate.Click += MenuCheckUpdate_Click;
        btnBackToHome.Click += (s, e) => ShowHomeView(true);
        menuBackupAndRestore.Click += MenuBackupAndRestore_Click;

        pbTheme.Content ??= new ThemeSettingView();

        this.WhenActivated(disposables =>
        {
            //servers
            this.BindCommand(ViewModel, vm => vm.AddVmessServerCmd, v => v.menuAddVmessServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddVlessServerCmd, v => v.menuAddVlessServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddShadowsocksServerCmd, v => v.menuAddShadowsocksServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddSocksServerCmd, v => v.menuAddSocksServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddHttpServerCmd, v => v.menuAddHttpServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddTrojanServerCmd, v => v.menuAddTrojanServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddHysteria2ServerCmd, v => v.menuAddHysteria2Server).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddTuicServerCmd, v => v.menuAddTuicServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddWireguardServerCmd, v => v.menuAddWireguardServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddAnytlsServerCmd, v => v.menuAddAnytlsServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddNaiveServerCmd, v => v.menuAddNaiveServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddMasqueServerCmd, v => v.menuAddMasqueServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddCustomServerCmd, v => v.menuAddCustomServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddCustomOutboundServerCmd, v => v.menuAddCustomOutboundServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddPolicyGroupServerCmd, v => v.menuAddPolicyGroupServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddProxyChainServerCmd, v => v.menuAddProxyChainServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddServerViaClipboardCmd, v => v.menuAddServerViaClipboard).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddServerViaScanCmd, v => v.menuAddServerViaScan).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddServerViaImageCmd, v => v.menuAddServerViaImage).DisposeWith(disposables);

            //sub
            this.BindCommand(ViewModel, vm => vm.SubSettingCmd, v => v.menuSubSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubUpdateCmd, v => v.menuSubUpdate).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubUpdateViaProxyCmd, v => v.menuSubUpdateViaProxy).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubGroupUpdateCmd, v => v.menuSubGroupUpdate).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubGroupUpdateViaProxyCmd, v => v.menuSubGroupUpdateViaProxy).DisposeWith(disposables);

            //setting
            this.BindCommand(ViewModel, vm => vm.OptionSettingCmd, v => v.menuOptionSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingSettingCmd, v => v.menuRoutingSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.DNSSettingCmd, v => v.menuDNSSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.FullConfigTemplateCmd, v => v.menuFullConfigTemplate).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.GlobalHotkeySettingCmd, v => v.menuGlobalHotkeySetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RebootAsAdminCmd, v => v.menuRebootAsAdmin).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ClearServerStatisticsCmd, v => v.menuClearServerStatistics).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenTheFileLocationCmd, v => v.menuOpenTheFileLocation).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RegionalPresetDefaultCmd, v => v.menuRegionalPresetsDefault).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RegionalPresetRussiaCmd, v => v.menuRegionalPresetsRussia).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RegionalPresetIranCmd, v => v.menuRegionalPresetsIran).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.ReloadCmd, v => v.menuReload).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.BlReloadEnabled, v => v.menuReload.IsEnabled).DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.BlNewUpdate, v => v.btnNewUpdate.Visibility).DisposeWith(disposables);

            _layoutBindingsDisposable.DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.MainGirdOrientation)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(UpdateLayout)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.StatusBarViewModel)
                .Subscribe(vm => ViewHost.Show(contentStatusBarView, vm))
                .DisposeWith(disposables);

            ViewModel.ReadTextFromClipboardInteraction.RegisterHandler(interaction =>
            {
                var clipboardData = WindowsUtils.GetClipboardData();
                interaction.SetOutput(clipboardData);
            }).DisposeWith(disposables);

            ViewModel.ScanScreenInteraction.RegisterHandler(interaction =>
            {
                ShowHideWindow(false);
                if (Application.Current?.MainWindow is { } window)
                {
                    var bytes = QRCodeWindowsUtils.CaptureScreen(window);
                    interaction.SetOutput(bytes);
                }
                ShowHideWindow(true);
            }).DisposeWith(disposables);

            ViewModel.BrowseImageFileInteraction.RegisterHandler(interaction =>
            {
                if (UI.OpenFileDialog(out var fileName, "PNG|*.png|All|*.*") != true)
                {
                    interaction.SetOutput(null);
                    return;
                }
                interaction.SetOutput(fileName);
            }).DisposeWith(disposables);

            ViewModel.ShowHideWindowInteraction.RegisterHandler(interaction =>
            {
                ShowHideWindow(interaction.Input);
                interaction.SetOutput(RxVoid.Default);
            }).DisposeWith(disposables);

            AppEvents.SendSnackMsgRequested
              .AsObservable()
              .ObserveOn(RxSchedulers.MainThreadScheduler)
              .Subscribe(async content => await DelegateSnackMsg(content))
              .DisposeWith(disposables);

            AppEvents.AppExitRequested
              .AsObservable()
              .ObserveOn(RxSchedulers.MainThreadScheduler)
              .Subscribe(_ => StorageUI())
              .DisposeWith(disposables);

            AppEvents.ShutdownRequested
             .AsObservable()
             .ObserveOn(RxSchedulers.MainThreadScheduler)
             .Subscribe(Shutdown)
             .DisposeWith(disposables);
        });

        Title = $"{Utils.GetVersion()} - {(Utils.IsAdministrator() ? ResUI.RunAsAdmin : ResUI.NotRunAsAdmin)}";
        if (_config.UiItem.AutoHideStartup)
        {
            WindowState = WindowState.Minimized;
        }

        if (!_config.GuiItem.EnableHWA)
        {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        }

        AddHelpMenuItem();
        WindowsManager.Instance.RegisterGlobalHotkey(_config, OnHotkeyHandler, null);
    }

    #region Event

    private void OnProgramStarted(object state, bool timeout)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ShowHideWindow(true);
        });
    }

    private async Task DelegateSnackMsg(string content)
    {
        MainSnackbar.MessageQueue?.Enqueue(content);
        await Task.CompletedTask;
    }

    private void OnHotkeyHandler(EGlobalHotkey e)
    {
        switch (e)
        {
            case EGlobalHotkey.ShowForm:
                ShowHideWindow(null);
                break;

            case EGlobalHotkey.SystemProxyClear:
            case EGlobalHotkey.SystemProxySet:
            case EGlobalHotkey.SystemProxyUnchanged:
            case EGlobalHotkey.SystemProxyPac:
                AppEvents.SysProxyChangeRequested.Publish((ESysProxyType)((int)e - 1));
                break;
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        ShowHideWindow(false);
    }

    private async void Current_SessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        Logging.SaveLog("Current_SessionEnding");
        StorageUI();
        await AppManager.Instance.AppExitAsync(false);
    }

    private void Shutdown(bool obj)
    {
        Application.Current.Shutdown();
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            switch (e.Key)
            {
                case Key.V:
                    if (Keyboard.FocusedElement is TextBox)
                    {
                        return;
                    }
                    AddServerViaClipboardAsync().ContinueWith(_ => { });

                    break;

                case Key.S:
                    ScanScreenTaskAsync().ContinueWith(_ => { });
                    break;
            }
        }
        else
        {
            if (e.Key == Key.F5)
            {
                ViewModel?.Reload();
            }
        }
    }

    // Saila: "Close" quits, like the ✕ (hiding next to the clock has its own button).
    private void MenuClose_Click(object sender, RoutedEventArgs e)
    {
        StorageUI();
        BtnWinClose_Click(sender, e);
    }

    private void MenuPromotion_Click(object sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart($"{Utils.Base64Decode(Global.PromotionUrl)}?t={DateTime.Now.Ticks}");
    }

    private void MenuSettingsSetUWP_Click(object sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart(Utils.GetBinPath("EnableLoopback.exe"));
    }

    public async Task AddServerViaClipboardAsync()
    {
        var clipboardData = WindowsUtils.GetClipboardData();
        if (clipboardData.IsNotEmpty() && ViewModel != null)
        {
            await ViewModel.AddServerViaClipboardAsync(clipboardData);
        }
    }

    public async Task ScanScreenTaskAsync()
    {
        ShowHideWindow(false);

        if (Application.Current?.MainWindow is Window window)
        {
            var bytes = QRCodeWindowsUtils.CaptureScreen(window);
            await ViewModel?.ScanScreenResult(bytes);
        }

        ShowHideWindow(true);
    }

    public void MenuCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        // Saila: its own dialog (Saila, Xray core, geo files) instead of v2rayN's core list.
        if (ViewModel?.CheckUpdateViewModel is { } vm)
        {
            DialogHost.Show(new SailaUpdateView(vm), "RootDialog");
        }

        AppEvents.HasUpdateNotified.Publish(false);
    }

    public void MenuBackupAndRestore_Click(object sender, RoutedEventArgs e)
    {
        // Saila: its own local-file dialog instead of v2rayN's (which also offered WebDAV).
        if (ViewModel?.BackupAndRestoreViewModel is { } vm)
        {
            DialogHost.Show(new SailaBackupView(vm), "RootDialog");
        }
    }

    private void BtnNavHome_Click(object sender, RoutedEventArgs e) => NavigateTo(SailaPage.Home);
    private void BtnNavServers_Click(object sender, RoutedEventArgs e) => NavigateTo(SailaPage.Servers);
    private void BtnNavAdd_Click(object sender, RoutedEventArgs e) => NavigateTo(SailaPage.Add);
    private void BtnNavSettings_Click(object sender, RoutedEventArgs e) => NavigateTo(SailaPage.Settings);
    private void BtnNavMenu_Click(object sender, RoutedEventArgs e)
    {
        if (btnNavMenu.ContextMenu != null)
        {
            btnNavMenu.ContextMenu.PlacementTarget = btnNavMenu;
            btnNavMenu.ContextMenu.IsOpen = true;
        }
    }
    private void BtnTabMore_Click(object sender, RoutedEventArgs e)
    {
        if (btnNavMenu.ContextMenu != null)
        {
            btnNavMenu.ContextMenu.PlacementTarget = btnTabMore;
            btnNavMenu.ContextMenu.IsOpen = true;
        }
    }
    private void MenuReload_Click(object sender, RoutedEventArgs e) => ViewModel?.Reload();
    #endregion Event

    #region UI

    private SailaPage _currentPage = SailaPage.Home;

    public SailaHomeView sailaHomeView => viewHome;

    private const double WideThreshold = 800;
    private readonly System.Windows.Threading.DispatcherTimer _saveSizeTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _isWide;
    private bool _sizeRestored;
    private bool _sizing;
    private ISailaEditPage? _editPage;
    private TaskCompletionSource<bool>? _editResult;
    private SailaPage _pageBeforeEdit = SailaPage.Servers;

    public bool IsWide => _isWide;

    public void NavigateTo(SailaPage page)
    {
        // Wide: home is always on the left, so "go home" keeps whatever is open on the right.
        if (_isWide && page == SailaPage.Home)
        {
            viewHome.RefreshState();
            ApplyThemeStyle();
            return;
        }
        if (page != SailaPage.Edit)
        {
            CloseEditPage(false);
        }

        _currentPage = page;
        var showHome = _isWide || page == SailaPage.Home;
        viewHome.Visibility = showHome ? Visibility.Visible : Visibility.Collapsed;
        panelRight.Visibility = page != SailaPage.Home ? Visibility.Visible : Visibility.Collapsed;
        colHome.Width = _isWide ? new GridLength(430) : showHome ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        colPane.Width = showHome && !_isWide ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        viewServers.Visibility = page == SailaPage.Servers ? Visibility.Visible : Visibility.Collapsed;
        viewAdd.Visibility = page == SailaPage.Add ? Visibility.Visible : Visibility.Collapsed;
        viewSettings.Visibility = page == SailaPage.Settings ? Visibility.Visible : Visibility.Collapsed;
        hostEdit.Visibility = page == SailaPage.Edit ? Visibility.Visible : Visibility.Collapsed;

        ApplyThemeStyle();

        switch (page)
        {
            case SailaPage.Home:
                viewHome.RefreshState();
                break;
            case SailaPage.Servers:
                _ = viewServers.LoadServersAsync();
                break;
            case SailaPage.Add:
                break;
            case SailaPage.Settings:
                viewSettings.ApplyThemeStyle();
                break;
        }

        // Wide: home stays on screen, so a new first server or a changed subscription must reach it from here.
        if (_isWide)
        {
            viewHome.RefreshState();
        }
    }

    // Saila: AddServerWindow / AddServer2Window as a page of this window; the result is the dialog's.
    public Task<bool> ShowEditPageAsync(object vm)
    {
        CloseEditPage(false);
        ISailaEditPage page = vm switch
        {
            AddServerViewModel server => new SailaEditView { ViewModel = server },
            AddServer2ViewModel custom => new SailaEditCustomView { ViewModel = custom },
            SubEditViewModel sub => new SailaEditSubView { ViewModel = sub },
            _ => throw new ArgumentException(vm.GetType().Name),
        };
        if (_currentPage != SailaPage.Edit)
        {
            _pageBeforeEdit = _currentPage;
        }
        _editPage = page;
        _editResult = new TaskCompletionSource<bool>();
        var result = _editResult.Task;
        page.SetWide(_isWide);
        page.Cancelled += () => LeaveEditPage(false);
        if (vm is ServiceLib.Base.ICloseable closeable)
        {
            closeable.RequestClose += (_, _) => Application.Current.Dispatcher.Invoke(() => LeaveEditPage(true));
        }
        hostEdit.Children.Add((UIElement)page);
        ShowHideWindow(true);
        NavigateTo(SailaPage.Edit);
        return result;
    }

    private void LeaveEditPage(bool saved)
    {
        CloseEditPage(saved);
        NavigateTo(_pageBeforeEdit == SailaPage.Edit ? SailaPage.Servers : _pageBeforeEdit);
    }

    private void CloseEditPage(bool saved)
    {
        if (_editPage == null)
        {
            return;
        }
        hostEdit.Children.Clear();
        hostEdit.Visibility = Visibility.Collapsed;
        _editPage = null;
        var result = _editResult;
        _editResult = null;
        result?.TrySetResult(saved);
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_sizeRestored)
        {
            return;
        }
        ApplyLayout(e.NewSize.Width >= WideThreshold);
        if (_sizing || WindowState != WindowState.Normal)
        {
            return;
        }
        var ui = _config.UiItem;
        if (e.NewSize.Width >= WideThreshold)
        {
            ui.SailaWideWidth = (int)e.NewSize.Width;
            ui.SailaWideHeight = (int)e.NewSize.Height;
        }
        else
        {
            ui.SailaNarrowWidth = (int)e.NewSize.Width;
            ui.SailaNarrowHeight = (int)e.NewSize.Height;
        }
        _saveSizeTimer.Stop();
        _saveSizeTimer.Start();
    }

    private void ApplyLayout(bool wide, bool force = false)
    {
        if (wide == _isWide && !force)
        {
            return;
        }
        _isWide = wide;
        _config.UiItem.SailaPaneOpen = wide;
        iconWinPane.Kind = wide ? PackIconKind.ArrowCollapseHorizontal : PackIconKind.ArrowExpandHorizontal;
        btnWinPane.ToolTip = wide ? "Close the side panel" : "Open the side panel";
        cardNav.Visibility = wide ? Visibility.Collapsed : Visibility.Visible;
        cardTabs.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
        paneDivider.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
        viewHome.SetWide(wide);
        viewServers.SetWide(wide);
        viewAdd.SetWide(wide);
        viewSettings.SetWide(wide);
        _editPage?.SetWide(wide);

        // Closing the panel leaves the power button; opening it shows the servers. An open form stays.
        var page = _editPage != null ? SailaPage.Edit : wide ? (_currentPage == SailaPage.Home ? SailaPage.Servers : _currentPage) : SailaPage.Home;
        NavigateTo(page);
    }

    private void BtnWinPane_Click(object sender, RoutedEventArgs e)
    {
        SizeWindow(!_isWide, false);
        _ = ConfigHandler.SaveConfig(_config);
    }

    // Each layout keeps its own size. Stays on the screen when the panel opens near the right edge.
    private void SizeWindow(bool wide, bool center)
    {
        var ui = _config.UiItem;
        var area = SystemParameters.WorkArea;
        _sizing = true;
        var width = wide ? Math.Max(ui.SailaWideWidth, WideThreshold) : Math.Min(ui.SailaNarrowWidth, WideThreshold - 1);
        Width = Math.Min(width, area.Width);
        Height = Math.Min(wide ? ui.SailaWideHeight : ui.SailaNarrowHeight, area.Height);
        if (center)
        {
            Left = area.Left + (area.Width - Width) / 2;
            Top = area.Top + (area.Height - Height) / 2;
        }
        else if (Left >= area.Left && Left < area.Right)
        {
            Left = Math.Max(area.Left, Math.Min(Left, area.Right - Width));
            Top = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
        }
        Dispatcher.BeginInvoke(() => _sizing = false, System.Windows.Threading.DispatcherPriority.Background);
    }

    // Saila: 20 px rounded window, like the cards inside. The window is transparent and the content is
    // clipped here; DWM's own rounding is a fixed 8 px and Windows 11 only.
    private void RoundCorners()
    {
        dialogRoot.Clip = new RectangleGeometry(new Rect(0, 0, dialogRoot.ActualWidth, dialogRoot.ActualHeight), 20, 20);
    }

    private void StripTitle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void BtnWinMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    // Saila: three clear choices — minimize to the taskbar, hide next to the clock, or quit.
    private void BtnWinTray_Click(object sender, RoutedEventArgs e) => ShowHideWindow(false);

    private async void BtnWinClose_Click(object sender, RoutedEventArgs e)
    {
        if (contentStatusBarView.Content is StatusBarView statusBar)
        {
            await statusBar.ExitAppAsync();
        }
        else
        {
            await AppManager.Instance.AppExitAsync(true);
        }
    }

    public void ApplyThemeStyle()
    {
        string look = _config.UiItem.SailaLook ?? "Nexus";
        string accentHex = _config.UiItem.SailaAccent ?? "#7AB0FF";
        Color accentColor = SailaHomeView.ParseColor(accentHex);
        var accentBrush = new SolidColorBrush(accentColor);
        accentBrush.Freeze();

        backdropControl.Look = look;
        backdropControl.AccentColor = accentColor;
        backdropControl.InvalidateVisual();

        Brush navBg;
        Brush navBorder;
        Brush inactiveBrush;

        switch (look)
        {
            case "Glass":
                navBg = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                navBorder = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255));
                inactiveBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
                break;
            case "Black":
                navBg = new SolidColorBrush(Color.FromRgb(0x14, 0x15, 0x19));
                navBorder = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x33));
                inactiveBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                break;
            case "White":
                navBg = Brushes.White;
                navBorder = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xF0));
                inactiveBrush = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                break;
            case "Nexus":
            default:
                navBg = new SolidColorBrush(Color.FromArgb(200, 0x11, 0x1A, 0x33));
                navBorder = new SolidColorBrush(Color.FromArgb(53, 0x82, 0xA0, 0xFF));
                inactiveBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
                break;
        }
        navBg.Freeze();
        navBorder.Freeze();
        inactiveBrush.Freeze();

        cardNav.Background = navBg;
        cardNav.BorderBrush = navBorder;
        iconWinMinimize.Foreground = inactiveBrush;
        iconWinClose.Foreground = inactiveBrush;
        iconWinTray.Foreground = inactiveBrush;
        iconWinPane.Foreground = inactiveBrush;
        cardTabs.Background = navBg;
        cardTabs.BorderBrush = navBorder;
        paneDivider.Background = navBorder;
        Resources["SailaScrollThumb"] = new SolidColorBrush(look == "White" ? Color.FromArgb(0x55, 0x0F, 0x17, 0x2A) : Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));

        SetNavButton(btnNavHome, _currentPage == SailaPage.Home, accentBrush, inactiveBrush);
        SetNavButton(btnNavServers, _currentPage == SailaPage.Servers, accentBrush, inactiveBrush);
        SetNavButton(btnNavAdd, _currentPage == SailaPage.Add, accentBrush, inactiveBrush);
        SetNavButton(btnNavSettings, _currentPage == SailaPage.Settings, accentBrush, inactiveBrush);
        SetNavButton(btnNavMenu, false, accentBrush, inactiveBrush);
        // The edit form belongs to the servers.
        var onServers = _currentPage is SailaPage.Servers or SailaPage.Edit;
        SetNavButton(btnNavServers, onServers, accentBrush, inactiveBrush);
        SetNavButton(btnTabServers, onServers, accentBrush, inactiveBrush);
        SetNavButton(btnTabAdd, _currentPage == SailaPage.Add, accentBrush, inactiveBrush);
        SetNavButton(btnTabSettings, _currentPage == SailaPage.Settings, accentBrush, inactiveBrush);
        SetNavButton(btnTabMore, false, accentBrush, inactiveBrush);
        if (_editPage is FrameworkElement editPage)
        {
            SailaEditTheme.Apply(editPage);
        }

        viewHome?.ApplyThemeStyle();
        viewServers?.ApplyThemeStyle();
        viewAdd?.ApplyThemeStyle();
        viewSettings?.ApplyThemeStyle();
    }

    private static void SetNavButton(Button btn, bool isActive, Brush accentBrush, Brush inactiveBrush)
    {
        if (isActive)
        {
            btn.Background = accentBrush;
            btn.Foreground = Brushes.White;
        }
        else
        {
            btn.Background = Brushes.Transparent;
            btn.Foreground = inactiveBrush;
        }
    }

    public void ShowHomeView(bool isHome)
    {
        if (isHome)
        {
            panelHome.Visibility = Visibility.Visible;
            panelAdvanced.Visibility = Visibility.Collapsed;
            contentStatusBarView.Height = 0;
            contentStatusBarView.Visibility = Visibility.Hidden;
            NavigateTo(_isWide ? _currentPage : SailaPage.Home);
        }
        else
        {
            panelHome.Visibility = Visibility.Collapsed;
            panelAdvanced.Visibility = Visibility.Visible;
            panelAdvanced.Margin = new Thickness(0, 0, 0, 36);
            contentStatusBarView.Height = double.NaN;
            contentStatusBarView.Visibility = Visibility.Visible;
            UpdateLayout(_config.UiItem.MainGirdOrientation);
        }
    }


    public void ShowHideWindow(bool? blShow)
    {
        var bl = blShow ?? !AppManager.Instance.ShowInTaskbar;
        if (bl)
        {
            this?.Show();
            if (this?.WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            this?.Activate();
            this?.Focus();
        }
        else
        {
            this?.Hide();
        }
        AppManager.Instance.ShowInTaskbar = bl;
    }

    protected override void OnLoaded(object? sender, RoutedEventArgs e)
    {
        base.OnLoaded(sender, e);
        if (_config.UiItem.AutoHideStartup)
        {
            ShowHideWindow(false);
        }
        RestoreUI();
        var wide = _config.UiItem.SailaPaneOpen;
        SizeWindow(wide, true);
        _isWide = wide;
        _currentPage = wide ? SailaPage.Servers : SailaPage.Home;
        ApplyLayout(wide, true);
        _sizeRestored = true;
        panelHome.Visibility = Visibility.Visible;
        panelAdvanced.Visibility = Visibility.Collapsed;
        ApplyThemeStyle();
    }

    private void RestoreUI()
    {
        if (_config.UiItem.MainGirdHeight1 > 0 && _config.UiItem.MainGirdHeight2 > 0)
        {
            if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Horizontal)
            {
                gridMain.ColumnDefinitions[0].Width = new GridLength(_config.UiItem.MainGirdHeight1, GridUnitType.Star);
                gridMain.ColumnDefinitions[2].Width = new GridLength(_config.UiItem.MainGirdHeight2, GridUnitType.Star);
            }
            else if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Vertical)
            {
                gridMain1.RowDefinitions[0].Height = new GridLength(_config.UiItem.MainGirdHeight1, GridUnitType.Star);
                gridMain1.RowDefinitions[2].Height = new GridLength(_config.UiItem.MainGirdHeight2, GridUnitType.Star);
            }
        }
    }

    private void StorageUI()
    {
        ConfigHandler.SaveWindowSizeItem(_config, GetType().Name, Width, Height);

        if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Horizontal)
        {
            ConfigHandler.SaveMainGirdHeight(_config, gridMain.ColumnDefinitions[0].ActualWidth, gridMain.ColumnDefinitions[2].ActualWidth);
        }
        else if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Vertical)
        {
            ConfigHandler.SaveMainGirdHeight(_config, gridMain1.RowDefinitions[0].ActualHeight, gridMain1.RowDefinitions[2].ActualHeight);
        }
    }

    private void UpdateLayout(EGirdOrientation orientation)
    {
        var currentLayoutDisposables = new MultipleDisposable();
        _layoutBindingsDisposable.Create(currentLayoutDisposables);

        gridMain.Visibility = orientation == EGirdOrientation.Horizontal ? Visibility.Visible : Visibility.Collapsed;
        gridMain1.Visibility = orientation == EGirdOrientation.Vertical ? Visibility.Visible : Visibility.Collapsed;
        gridMain2.Visibility = orientation == EGirdOrientation.Tab ? Visibility.Visible : Visibility.Collapsed;

        switch (orientation)
        {
            case EGirdOrientation.Horizontal:
                this.WhenAnyValue(v => v.ViewModel.ProfilesViewModel)
                    .Subscribe(vm => ViewHost.Show(tabProfiles, vm))
                    .DisposeWith(currentLayoutDisposables);
                this.WhenAnyValue(v => v.ViewModel.MsgViewModel)
                    .Subscribe(vm => ViewHost.Show(tabMsgView, vm))
                    .DisposeWith(currentLayoutDisposables);
                this.WhenAnyValue(v => v.ViewModel.ClashProxiesViewModel)
                    .Subscribe(vm => ViewHost.Show(tabClashProxies, vm))
                    .DisposeWith(currentLayoutDisposables);
                this.WhenAnyValue(v => v.ViewModel.ClashConnectionsViewModel)
                    .Subscribe(vm => ViewHost.Show(tabClashConnections, vm))
                    .DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabMsgView.Visibility).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies.Visibility).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections.Visibility).DisposeWith(currentLayoutDisposables);
                this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain.SelectedIndex).DisposeWith(currentLayoutDisposables);
                break;

            case EGirdOrientation.Vertical:
                this.WhenAnyValue(v => v.ViewModel.ProfilesViewModel)
                    .Subscribe(vm => ViewHost.Show(tabProfiles1, vm))
                    .DisposeWith(currentLayoutDisposables);
                this.WhenAnyValue(v => v.ViewModel.MsgViewModel)
                    .Subscribe(vm => ViewHost.Show(tabMsgView1, vm))
                    .DisposeWith(currentLayoutDisposables);
                this.WhenAnyValue(v => v.ViewModel.ClashProxiesViewModel)
                    .Subscribe(vm => ViewHost.Show(tabClashProxies1, vm))
                    .DisposeWith(currentLayoutDisposables);
                this.WhenAnyValue(v => v.ViewModel.ClashConnectionsViewModel)
                    .Subscribe(vm => ViewHost.Show(tabClashConnections1, vm))
                    .DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabMsgView1.Visibility).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies1.Visibility).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections1.Visibility).DisposeWith(currentLayoutDisposables);
                this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain1.SelectedIndex).DisposeWith(currentLayoutDisposables);
                break;

            case EGirdOrientation.Tab:
            default:
                this.WhenAnyValue(v => v.ViewModel.ProfilesViewModel)
                    .Subscribe(vm => ViewHost.Show(tabProfiles2, vm))
                    .DisposeWith(currentLayoutDisposables);
                this.WhenAnyValue(v => v.ViewModel.MsgViewModel)
                    .Subscribe(vm => ViewHost.Show(tabMsgView2, vm))
                    .DisposeWith(currentLayoutDisposables);
                this.WhenAnyValue(v => v.ViewModel.ClashProxiesViewModel)
                    .Subscribe(vm => ViewHost.Show(tabClashProxies2, vm))
                    .DisposeWith(currentLayoutDisposables);
                this.WhenAnyValue(v => v.ViewModel.ClashConnectionsViewModel)
                    .Subscribe(vm => ViewHost.Show(tabClashConnections2, vm))
                    .DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies2.Visibility).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections2.Visibility).DisposeWith(currentLayoutDisposables);
                this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain2.SelectedIndex).DisposeWith(currentLayoutDisposables);
                break;
        }

        RestoreUI();
    }

    private void AddHelpMenuItem()
    {
        var coreInfo = CoreInfoManager.Instance.GetCoreInfo();
        foreach (var it in coreInfo
            .Where(t => t.CoreType is not ECoreType.v2fly
                        and not ECoreType.hysteria))
        {
            var item = new MenuItem()
            {
                Tag = it.Url.Replace(@"/releases", ""),
                Header = string.Format(ResUI.menuWebsiteItem, it.CoreType.ToString().Replace("_", " ")).UpperFirstChar()
            };
            item.Click += MenuItem_Click;
            menuHelp.Items.Add(item);
        }
    }

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item)
        {
            ProcUtils.ProcessStart(item.Tag.ToString());
        }
    }

    #endregion UI
}
