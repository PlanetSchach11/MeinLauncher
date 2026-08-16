using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MeinLauncher.Models;
using MeinLauncher.ViewModels;

namespace MeinLauncher.Views.Settings;

public partial class MinecraftSettingsView : UserControl
{
    public MinecraftSettingsView()
    {
        InitializeComponent();
    }

    private SettingsViewModel? Vm => DataContext as SettingsViewModel;

    private async void OnBrowseJavaClick(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null || Vm is null)
            return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Java-Runtime auswählen (java.exe)",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Java Executable") { Patterns = ["*.exe"] },
                FilePickerFileTypes.All,
            ],
        });

        if (files.Count > 0)
            Vm.JavaPath = files[0].Path.LocalPath;
    }

    private async void OnBrowseGameDirClick(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null || Vm is null)
            return;

        var dirs = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Spieldaten-Verzeichnis auswählen",
            AllowMultiple = false,
        });

        if (dirs.Count > 0)
            Vm.GameDirectory = dirs[0].Path.LocalPath;
    }

    private void OnOpenGameDirClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null)
            return;

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Vm.GameDirectory,
            UseShellExecute = true,
        });
    }

    private void OnResetGameDirClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null)
            return;

        Vm.GameDirectory = LauncherSettings.DefaultGameDirectory;
    }

    private void OnUseJavaClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: JavaRuntimeItem item })
            Vm?.SelectJava(item);
    }
}
