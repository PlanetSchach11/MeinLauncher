using Avalonia.Controls;
using Avalonia.Interactivity;
using MeinLauncher.ViewModels;

namespace MeinLauncher.Views.Settings;

public partial class DesignSettingsView : UserControl
{
    public DesignSettingsView()
    {
        InitializeComponent();
    }

    private SettingsViewModel? Vm => DataContext as SettingsViewModel;

    private void OnAccentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AccentItem item } && Vm is not null)
            Vm.SelectedAccentItem = item;
    }
}
