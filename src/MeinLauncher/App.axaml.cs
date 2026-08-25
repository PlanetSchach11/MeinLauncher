using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Themes.Fluent;
using MeinLauncher.Services;
using MeinLauncher.ViewModels;
using MeinLauncher.Views;

namespace MeinLauncher;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = new SettingsService();

            // Sprache und Theme vor dem ersten Fensteraufbau anwenden.
            LocalizationManager.Instance.SetLanguage(settingsService.Current.Language);
            ThemeManager.Apply(settingsService.Current);

            // UI-Klick-Sound mit den Live-Einstellungen verdrahten.
            UISoundService.Instance.Configure(settingsService.Current);

            // Microsoft-Konto: gespeicherte Session im Hintergrund wiederherstellen
            // (Refresh ohne Browser, falls das Token abgelaufen ist).
            var accountService = new MicrosoftAccountService();
            _ = RestoreAccountAsync(settingsService, accountService);

            var window = new MainWindow
            {
                DataContext = new MainViewModel(settingsService, accountService),
            };

            ThemeManager.ApplyWindow(window);
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Stellt die gespeicherte Microsoft-Session beim Start wieder her. Läuft im
    /// Hintergrund und meldet keine Fehler, damit die App immer startklar ist.
    /// </summary>
    private static async Task RestoreAccountAsync(
        SettingsService settingsService,
        MicrosoftAccountService accountService)
    {
        try
        {
            AccountDiagnostics.Log("App-Start: Stelle Session wieder her …");
            var session = await accountService.RestoreAsync();
            AccountDiagnostics.Log(
                session is null
                    ? "App-Start: keine Session wiederhergestellt."
                    : $"App-Start: Session wiederhergestellt ({session.MinecraftUsername}).");
        }
        catch (Exception ex)
        {
            AccountDiagnostics.Log($"App-Start: Wiederherstellen fehlgeschlagen: {ex.GetType().Name}: {ex.Message}");
            // Beim nächsten Start erneut versuchen – die gespeicherte Session bleibt erhalten.
        }
    }
}
