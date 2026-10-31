using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using ServiceLib.Manager;
using ServiceLib.Models.Configs;
using ServiceLib.ViewModels;

namespace v2rayN.Views;

/// <summary>
/// Saila: backup to / restore from a local file in Saila's style, using v2rayN's BackupAndRestoreViewModel.
/// </summary>
public partial class SailaBackupView : UserControl
{
    private static Config _config => AppManager.Instance.Config;
    private readonly BackupAndRestoreViewModel _vm;

    public SailaBackupView(BackupAndRestoreViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        ApplyLook();
    }

    private async void BtnBackup_Click(object sender, RoutedEventArgs e)
    {
        if (UI.SaveFileDialog(out var fileName, "Zip|*.zip") != true)
        {
            return;
        }
        txtStatus.Text = "…";
        await _vm.LocalBackup(fileName);
        txtStatus.Text = _vm.OperationMsg;
    }

    private async void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        if (UI.OpenFileDialog(out var fileName, "Zip|*.zip|All|*.*") != true)
        {
            return;
        }
        txtStatus.Text = "…";
        await _vm.LocalRestore(fileName);
        txtStatus.Text = _vm.OperationMsg;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => DialogHost.CloseDialogCommand.Execute(null, this);

    private void ApplyLook()
    {
        var white = (_config.UiItem.SailaLook ?? "Nexus") == "White";
        var accentBrush = new SolidColorBrush(SailaHomeView.ParseColor(_config.UiItem.SailaAccent ?? "#7AB0FF"));
        var primary = new SolidColorBrush(white ? Color.FromRgb(0x0F, 0x17, 0x2A) : Colors.White);
        var secondary = new SolidColorBrush(white ? Color.FromRgb(0x47, 0x55, 0x69) : Color.FromRgb(0x94, 0xA3, 0xB8));
        card.Background = new SolidColorBrush(white ? Colors.White : Color.FromRgb(0x14, 0x15, 0x19));
        card.BorderBrush = new SolidColorBrush(white ? Color.FromRgb(0xE2, 0xE7, 0xF0) : Color.FromRgb(0x2A, 0x2C, 0x33));
        txtTitle.Foreground = primary;
        txtHint.Foreground = secondary;
        txtStatus.Foreground = secondary;
        btnClose.Foreground = secondary;
        btnRestore.Foreground = accentBrush;
        btnRestore.BorderBrush = accentBrush;
        btnBackup.Background = accentBrush;
        btnBackup.BorderBrush = accentBrush;
        btnBackup.Foreground = new SolidColorBrush(white ? Colors.White : Color.FromRgb(0x0F, 0x17, 0x2A));
    }
}
