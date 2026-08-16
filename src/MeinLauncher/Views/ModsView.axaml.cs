using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Diagnostics;

namespace MeinLauncher.Views;

public partial class ModsView : UserControl
{
    public ModsView()
    {
        InitializeComponent();
    }

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.ModsViewModel vm)
            return;

        if (System.IO.Directory.Exists(vm.ModsDirectory))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = vm.ModsDirectory,
                UseShellExecute = true,
            });
        }
    }

    private void OnInstalledToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle &&
            toggle.DataContext is Models.InstalledModItem item &&
            DataContext is ViewModels.ModsViewModel vm)
        {
            vm.ToggleInstalledCommand.Execute(item);
        }
    }

    private void OnUpdateClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Models.InstalledModItem item } &&
            DataContext is ViewModels.ModsViewModel vm)
        {
            vm.UpdateModCommand.Execute(item);
        }
    }

    private void OnUninstallClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Models.InstalledModItem item } &&
            DataContext is ViewModels.ModsViewModel vm)
        {
            vm.UninstallModCommand.Execute(item);
        }
    }

    private void OnInstallClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Models.ModrinthModItem item } &&
            DataContext is ViewModels.ModsViewModel vm)
        {
            vm.InstallCommand.Execute(item);
        }
    }

    private void OnModrinthUpdateClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Models.ModrinthModItem item } &&
            DataContext is ViewModels.ModsViewModel vm)
        {
            vm.UpdateFromModrinthCommand.Execute(item);
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not ViewModels.ModsViewModel vm)
            return;

        if (ReferenceEquals(sender, ModrinthBox))
            vm.BrowseModrinthCommand.Execute(null);
    }
}
