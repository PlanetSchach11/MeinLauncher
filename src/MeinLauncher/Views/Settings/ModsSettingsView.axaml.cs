using Avalonia.Controls;
using Avalonia.Interactivity;
using MeinLauncher.ViewModels;

namespace MeinLauncher.Views.Settings;

public partial class ModsSettingsView : UserControl
{
    public ModsSettingsView()
    {
        InitializeComponent();
    }

    private SettingsViewModel? Vm => DataContext as SettingsViewModel;

    private void OnOpenModsFolderClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null)
            return;

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Vm.ModsFolderPath,
            UseShellExecute = true,
        });
    }
}
