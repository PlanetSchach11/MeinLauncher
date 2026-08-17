using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeinLauncher.Models;
using MeinLauncher.Services;

namespace MeinLauncher.ViewModels;

/// <summary>
/// Startseite: Begrüßung, aktives Profil und direkter Start von Minecraft: Java Edition
/// über die installierte Java-Runtime – ohne den offiziellen Launcher. Die Profilverwaltung
/// liegt auf der Profil-Seite; hier erscheint nur das aktive Profil als Hinweis.
/// Die Microsoft-Anmeldung läuft über die eigenen Einstellungen (Client-ID).
/// </summary>
public partial class HomeViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly GameLauncherService _gameLauncher;
    private readonly ProfileService _profileService;

    /// <summary>Öffnet die Profil-Seite (z. B. „Profile verwalten“).</summary>
    public Action? OpenProfileRequested { get; init; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial int InstalledVersionCount { get; set; }

    [ObservableProperty]
    public partial int ModCount { get; set; }

    [ObservableProperty]
    public partial string Username { get; set; } = "";

    private string _versionId = "";

    public string Greeting => $"{t("Home.WelcomeBack")}, {Username}!";

    public string SelectedVersionLabel => string.IsNullOrEmpty(_versionId)
        ? t("Home.NoVersionSelected")
        : $"Minecraft {_versionId}";

    /// <summary>Name des aktiven Profils (aus den Einstellungen).</summary>
    public string ActiveProfileName =>
        _profileService.Settings.ActiveProfile?.Name ?? t("Home.ProfileStandard");

    /// <summary>Kleine Anzeige „Aktives Profil: X“.</summary>
    public string ActiveProfileLine => $"{t("Home.ActiveProfile")}: {ActiveProfileName}";

    partial void OnUsernameChanged(string value) => OnPropertyChanged(nameof(Greeting));

    public HomeViewModel(SettingsService settingsService, GameLauncherService gameLauncher, ProfileService profileService)
    {
        _settingsService = settingsService;
        _gameLauncher = gameLauncher;
        _profileService = profileService;
        StatusMessage = t("Home.Ready");

        LocalizationManager.Instance.LanguageChanged += () =>
        {
            OnPropertyChanged(nameof(Greeting));
            OnPropertyChanged(nameof(SelectedVersionLabel));
            OnPropertyChanged(nameof(ActiveProfileName));
            OnPropertyChanged(nameof(ActiveProfileLine));
        };
    }

    public void Refresh()
    {
        var s = _settingsService.Current;
        Username = s.Username;
        _versionId = s.SelectedVersionId;
        OnPropertyChanged(nameof(SelectedVersionLabel));
        OnPropertyChanged(nameof(ActiveProfileName));
        OnPropertyChanged(nameof(ActiveProfileLine));

        InstalledVersionCount = Directory.Exists(s.VersionsDirectory)
            ? Directory.EnumerateDirectories(s.VersionsDirectory).Count()
            : 0;

        ModCount = new ModService().ScanMods(s.ModsDirectory).Count;
    }

    // ---------------------------------------------------------------- Spiel starten

    [RelayCommand]
    private void OpenProfile()
    {
        OpenProfileRequested?.Invoke();
    }

    [RelayCommand]
    private async Task PlayAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = t("Home.StartingGame");
        try
        {
            var s = _settingsService.Current;
            var activeProfile = s.ActiveProfile;
            if (activeProfile is null && s.Profiles.Count > 0)
            {
                activeProfile = s.Profiles[0];
                _profileService.ApplyProfile(activeProfile);
                OnPropertyChanged(nameof(ActiveProfileName));
                OnPropertyChanged(nameof(ActiveProfileLine));
            }

            var result = await _gameLauncher.LaunchAsync(s, activeProfile);
            AccountDiagnostics.Log(
                $"PlayAsync: Ergebnis '{result.MessageKey}' (Success: {result.Success}).");
            StatusMessage = t(result.MessageKey, result.Args);
        }
        catch (Exception ex)
        {
            AccountDiagnostics.Log($"PlayAsync fehlgeschlagen: {ex.GetType().Name}: {ex.Message}");
            StatusMessage = t("Home.GameLaunchFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
