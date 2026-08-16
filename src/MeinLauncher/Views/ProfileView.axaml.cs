using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MeinLauncher.ViewModels;

namespace MeinLauncher.Views;

public partial class ProfileView : UserControl
{
    public ProfileView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (DataContext is ProfileViewModel { IsEditingProfile: true })
                ProfileEditBox.Focus();
        };
    }

    private ProfileViewModel? Vm => DataContext as ProfileViewModel;

    private void OnBackClick(object? sender, RoutedEventArgs e) => Vm?.BackToOverview();

    private void OnOpenVersionsClick(object? sender, RoutedEventArgs e) => Vm?.OpenVersions();

    private void OnOpenModsClick(object? sender, RoutedEventArgs e) => Vm?.OpenMods();

    private void OnProfileChipClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ProfileItem item } && Vm is { } vm)
            vm.SelectedProfile = item;
    }

    /// <summary>Enter übernimmt den Profilnamen, Escape bricht ab.</summary>
    private void OnProfileEditKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ProfileViewModel vm)
            return;

        if (e.Key == Key.Enter)
        {
            vm.ApplyProfileEditCommand.Execute(null);
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelProfileEditCommand.Execute(null);
        }
    }

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

    private void OnUseJavaClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: JavaRuntimeItem item })
            Vm?.SelectJava(item);
    }
}
