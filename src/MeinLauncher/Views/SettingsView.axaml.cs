using Avalonia.Controls;
using Avalonia.Interactivity;
using MeinLauncher.ViewModels;

namespace MeinLauncher.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private SettingsViewModel? Vm => DataContext as SettingsViewModel;

    private void OnSectionCardClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key } && Vm is not null)
        {
            Vm.NavigateToSection(key);
            SectionHost.Content = CreateSectionView(key);
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        Vm?.BackToOverview();
    }

    private static Control CreateSectionView(string key) => key switch
    {
        "design" => new Settings.DesignSettingsView(),
        "ui" => new Settings.UiSettingsView(),
        "minecraft" => new Settings.MinecraftSettingsView(),
        "mods" => new Settings.ModsSettingsView(),
        "account" => new Settings.AccountSettingsView(),
        _ => new Settings.GeneralSettingsView(),
    };
}
