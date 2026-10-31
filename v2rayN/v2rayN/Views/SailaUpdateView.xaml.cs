using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Automation;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using ServiceLib.Common;
using ServiceLib.Enums;
using ServiceLib.Manager;
using ServiceLib.Models.Configs;
using ServiceLib.Models.Dto;
using ServiceLib.ViewModels;

namespace v2rayN.Views;

/// <summary>
/// Saila: update dialog for Saila itself, the Xray core and the geo files, driving v2rayN's
/// CheckUpdateViewModel (download, core stop and app upgrade stay in that one place).
/// </summary>
public partial class SailaUpdateView : UserControl
{
    private static Config _config => AppManager.Instance.Config;
    private readonly CheckUpdateViewModel _vm;
    private readonly DispatcherTimer _statusTimer;
    private readonly List<(CheckUpdateModel Model, ToggleButton Toggle, TextBlock Status, TextBlock Name)> _rows = [];

    public SailaUpdateView(CheckUpdateViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        var app = vm.CheckUpdateModels.FirstOrDefault(m => m.CoreType == ECoreType.v2rayN);
        togPreRelease.IsChecked = app?.IsCheckPreRelease ?? false;
        BuildRows();
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _statusTimer.Tick += (_, _) => RefreshStatus();
        ApplyLook();
    }

    // mihomo and sing-box are left out: Saila never runs them and its zip does not ship them.
    private static string? RowName(CheckUpdateModel m) => m switch
    {
        { CoreType: ECoreType.v2rayN } => $"Saila  ·  {Utils.GetVersionInfo().Split('+')[0]}",
        { CoreType: ECoreType.Xray } => "Xray core",
        { IsGeoFile: true } => "Geo files (geoip / geosite)",
        _ => null,
    };

    private void BuildRows()
    {
        panelRows.Children.Clear();
        _rows.Clear();
        foreach (var m in _vm.CheckUpdateModels)
        {
            var name = RowName(m);
            if (name == null)
            {
                continue;
            }
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var toggle = new ToggleButton
            {
                IsChecked = m.IsSelected != false,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)FindResource("MaterialDesignSwitchToggleButton"),
            };
            AutomationProperties.SetName(toggle, name);
            var text = new StackPanel();
            var nameBlock = new TextBlock { Text = name, FontSize = 13, FontWeight = FontWeights.SemiBold };
            var status = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            text.Children.Add(nameBlock);
            text.Children.Add(status);
            Grid.SetColumn(text, 1);
            grid.Children.Add(toggle);
            grid.Children.Add(text);
            panelRows.Children.Add(grid);
            _rows.Add((m, toggle, status, nameBlock));
        }
    }

    // Applies the dialog's choices to the shared models; cores Saila does not use are never checked.
    private void ApplySelection()
    {
        foreach (var m in _vm.CheckUpdateModels)
        {
            var row = _rows.FirstOrDefault(r => r.Model == m);
            m.IsSelected = row.Model != null && row.Toggle.IsChecked == true;
            m.Remarks = string.Empty;
            if (m.CoreType == ECoreType.v2rayN)
            {
                m.IsCheckPreRelease = togPreRelease.IsChecked == true;
            }
        }
    }

    // v2rayN's messages name the app "v2rayN"; Saila shows its own name.
    private void RefreshStatus()
    {
        foreach (var r in _rows)
        {
            r.Status.Text = (r.Model.Remarks ?? string.Empty).Replace("v2rayN", Global.AppName);
        }
    }

    private async void BtnCheckOnly_Click(object sender, RoutedEventArgs e) => await RunAsync(false);

    private async void BtnUpdate_Click(object sender, RoutedEventArgs e) => await RunAsync(true);

    private async Task RunAsync(bool update)
    {
        ApplySelection();
        btnCheckOnly.IsEnabled = btnUpdate.IsEnabled = false;
        _statusTimer.Start();
        try
        {
            if (update)
            {
                await RunToEnd(_vm.CheckUpdateCmd.Execute());
            }
            else
            {
                await RunToEnd(_vm.CheckOnlyCmd.Execute());
            }
        }
        finally
        {
            btnCheckOnly.IsEnabled = btnUpdate.IsEnabled = true;
            RefreshStatus();
        }
    }

    // ReactiveCommand.Execute is cold and this project has no System.Reactive to await it; completes when the command does.
    private static Task RunToEnd(IObservable<RxVoid> command)
    {
        var done = new TaskCompletionSource();
        command.Subscribe(new EndObserver(done));
        return done.Task;
    }

    private sealed class EndObserver(TaskCompletionSource done) : IObserver<RxVoid>
    {
        public void OnNext(RxVoid value) { }
        public void OnError(Exception error) => done.TrySetResult();
        public void OnCompleted() => done.TrySetResult();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        _statusTimer.Stop();
        DialogHost.CloseDialogCommand.Execute(null, this);
    }

    private void ApplyLook()
    {
        var look = _config.UiItem.SailaLook ?? "Nexus";
        var white = look == "White";
        var accentBrush = new SolidColorBrush(SailaHomeView.ParseColor(_config.UiItem.SailaAccent ?? "#7AB0FF"));
        var primary = new SolidColorBrush(white ? Color.FromRgb(0x0F, 0x17, 0x2A) : Colors.White);
        var secondary = new SolidColorBrush(white ? Color.FromRgb(0x47, 0x55, 0x69) : Color.FromRgb(0x94, 0xA3, 0xB8));
        card.Background = new SolidColorBrush(white ? Colors.White : Color.FromRgb(0x14, 0x15, 0x19));
        card.BorderBrush = new SolidColorBrush(white ? Color.FromRgb(0xE2, 0xE7, 0xF0) : Color.FromRgb(0x2A, 0x2C, 0x33));
        txtTitle.Foreground = primary;
        txtPreRelease.Foreground = primary;
        btnClose.Foreground = secondary;
        btnCheckOnly.Foreground = accentBrush;
        btnCheckOnly.BorderBrush = accentBrush;
        btnUpdate.Background = accentBrush;
        btnUpdate.BorderBrush = accentBrush;
        btnUpdate.Foreground = new SolidColorBrush(white ? Colors.White : Color.FromRgb(0x0F, 0x17, 0x2A));
        togPreRelease.Background = accentBrush;
        foreach (var r in _rows)
        {
            r.Toggle.Background = accentBrush;
            r.Name.Foreground = primary;
            r.Status.Foreground = secondary;
        }
    }
}
