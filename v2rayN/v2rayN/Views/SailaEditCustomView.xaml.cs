namespace v2rayN.Views;

public partial class SailaEditCustomView : ISailaEditPage
{
    public event Action? Cancelled;

    private DateTime _noticeUntil;

    public void SetWide(bool wide)
    {
        rootEdit.Margin = wide ? new Thickness(20, 8, 20, 16) : new Thickness(16, 4, 16, 84);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => Cancelled?.Invoke();

    public SailaEditCustomView()
    {
        InitializeComponent();

        Loaded += Window_Loaded;
        SailaEditTheme.Apply(this);
        btnSave.Click += (_, _) => _noticeUntil = DateTime.UtcNow.AddSeconds(5);

        cmbCoreType.ItemsSource = Utils.GetEnumNames<ECoreType>().Where(t => t != nameof(ECoreType.v2rayN)).ToList().AppendEmpty();

        this.WhenActivated(disposables =>
        {
            AppEvents.SendSnackMsgRequested
                .AsObservable()
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Where(_ => DateTime.UtcNow < _noticeUntil)
                .Subscribe(msg => txtNotice.Text = msg)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.SelectedSource)
                .KeepNotNull()
                .Subscribe(InitializeData)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Address, v => v.txtAddress.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CoreType, v => v.cmbCoreType.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.DisplayLog, v => v.togDisplayLog.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.PreSocksPort, v => v.txtPreSocksPort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.IsSingboxEndpoint, v => v.togSingBoxEndpoint.IsChecked).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.BrowseServerCmd, v => v.btnBrowse).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.EditServerCmd, v => v.btnEdit).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SaveServerCmd, v => v.btnSave).DisposeWith(disposables);

            ViewModel.BrowseConfigFileInteraction.RegisterHandler(interaction =>
            {
                if (UI.OpenFileDialog(out var fileName, "Config|*.json|YAML|*.yaml;*.yml|All|*.*") != true)
                {
                    interaction.SetOutput(null);
                    return;
                }
                interaction.SetOutput(fileName);
            }).DisposeWith(disposables);
        });
    }

    private void InitializeData(ProfileItem profileItem)
    {
        if (profileItem.ConfigType is EConfigType.Custom)
        {
            txtType.Text = "Custom";
            cmbCoreType.ItemsSource = Utils.GetEnumNames<ECoreType>().Where(t => t != nameof(ECoreType.v2rayN)).ToList();
            gridCustomServer.Visibility = Visibility.Visible;
            gridCustomOutbound.Visibility = Visibility.Collapsed;
        }
        else if (profileItem.ConfigType is EConfigType.Outbound)
        {
            txtType.Text = "Outbound";
            cmbCoreType.ItemsSource = Global.CoreTypes;
            gridCustomServer.Visibility = Visibility.Collapsed;
            gridCustomOutbound.Visibility = Visibility.Visible;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        txtRemarks.Focus();
    }
}
