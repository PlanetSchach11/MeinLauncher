using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MeinLauncher.ViewModels;

namespace MeinLauncher.Views;

public partial class BackgroundSettingsView : UserControl
{
    public BackgroundSettingsView()
    {
        InitializeComponent();
    }

    private BackgroundSettingsViewModel? Vm => DataContext as BackgroundSettingsViewModel;

    private async void OnChooseImageClick(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null || Vm is null)
            return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Hintergrundbild auswählen",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Bilder")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif"],
                },
                FilePickerFileTypes.All,
            ],
        });

        if (files.Count > 0)
            Vm.ImagePath = files[0].Path.LocalPath;
    }

    private void OnResetImageClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not null)
            Vm.ImagePath = "";
    }

    private void OnColorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: BackgroundColorItem item })
            Vm?.SelectColor(item);
    }
}
