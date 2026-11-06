namespace v2rayN.Views;

public partial class SailaEditSubView : ISailaEditPage
{
    public event Action? Cancelled;

    private DateTime _noticeUntil;
    private bool _deleteArmed;

    public void SetWide(bool wide)
    {
        rootEdit.Margin = wide ? new Thickness(20, 8, 20, 16) : new Thickness(16, 4, 16, 84);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => Cancelled?.Invoke();

    // The first press only warns: deleting takes the subscription's servers with it.
    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (!_deleteArmed)
        {
            _deleteArmed = true;
            txtNotice.Text = ResUI.saila_sub_delete_confirm;
            return;
        }
        await ConfigHandler.DeleteSubItem(AppManager.Instance.Config, ViewModel.SelectedSource.Id);
        Cancelled?.Invoke();
    }

    public SailaEditSubView()
    {
        InitializeComponent();

        Loaded += Window_Loaded;
        SailaEditTheme.Apply(this);
        btnSave.Click += (_, _) => _noticeUntil = DateTime.UtcNow.AddSeconds(5);

        cmbConvertTarget.ItemsSource = Global.SubConvertTargets;
        cmbCustomCoreType.ItemsSource = Utils.GetEnumNames<ECoreType>().Where(t => t != nameof(ECoreType.v2rayN)).ToList().AppendEmpty();

        this.WhenActivated(disposables =>
        {
            AppEvents.SendSnackMsgRequested
                .AsObservable()
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Where(_ => DateTime.UtcNow < _noticeUntil)
                .Subscribe(msg => txtNotice.Text = msg)
                .DisposeWith(disposables);

            var isNew = ViewModel.SelectedSource.Id.IsNullOrEmpty();
            txtType.Text = isNew ? ResUI.menuSubAdd : ViewModel.SelectedSource.Remarks;
            btnDelete.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;

            this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Url, v => v.txtUrl.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.MoreUrl, v => v.txtMoreUrl.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Enabled, v => v.togEnable.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.AutoUpdateInterval, v => v.txtAutoUpdateInterval.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.UserAgent, v => v.txtUserAgent.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.RequestHeaders, v => v.txtRequestHeaders.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Sort, v => v.txtSort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Filter, v => v.txtFilter.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.ConvertTarget, v => v.cmbConvertTarget.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.PrevProfile, v => v.txtPrevProfile.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.NextProfile, v => v.txtNextProfile.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.PreSocksPort, v => v.txtPreSocksPort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Memo, v => v.txtMemo.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CustomCoreType, v => v.cmbCustomCoreType.Text).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SelectPrevProfileCmd, v => v.btnSelectPrevProfile).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SelectNextProfileCmd, v => v.btnSelectNextProfile).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        txtRemarks.Focus();
    }
}
