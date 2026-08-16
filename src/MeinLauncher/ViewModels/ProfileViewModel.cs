using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeinLauncher.Models;
using MeinLauncher.Services;

namespace MeinLauncher.ViewModels;

/// <summary>
/// Profil-Seite: Profilverwaltung (Standardprofil + eigene Profile), das aktive
/// Profil mit Minecraft-Version, Loader, Mods-Anzahl und Java/RAM sowie Unter-Ansichten
/// für die Versions- und Mods-Verwaltung. Die Profil-Logik läuft unverändert über den
/// <see cref="ProfileService"/> – jedes Profil hat seine eigene Minecraft-Version,
/// seinen Loader, Java-Einstellungen und einen eigenen Mods-Ordner.
/// </summary>
public partial class ProfileViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly GameLauncherService _gameLauncher;
    private readonly MojangVersionService _versionService;
    private readonly ModService _modService;
    private readonly ProfileService _profileService;
    private readonly JavaService _javaService = new();

    private LauncherProfile? _renameTarget;
    private bool _isRefreshing;
    private bool _javaScanned;

    private VersionsViewModel? _versions;
    private ModsViewModel? _mods;

    // ---------------------------------------------------------------- Profil-Auswahl

    public ObservableCollection<ProfileItem> ProfileItems { get; } = [];

    [ObservableProperty]
    public partial ProfileItem? SelectedProfile { get; set; }

    [ObservableProperty]
    public partial bool IsEditingProfile { get; set; }

    [ObservableProperty]
    public partial string ProfileEditName { get; set; } = "";

    [ObservableProperty]
    public partial bool IsDeleteArmed { get; set; }

    [ObservableProperty]
    public partial string ProfileStatus { get; set; } = "";

    /// <summary>True, wenn ein echtes Profil (nicht „Standard“) gewählt ist.</summary>
    public bool HasProfileSelected => SelectedProfile is { Profile: not null };

    /// <summary>Name des aktiven Profils (aus den Einstellungen).</summary>
    public string ActiveProfileName =>
        _profileService.Settings.ActiveProfile?.Name ?? t("Home.ProfileStandard");

    // ---------------------------------------------------------------- Aktives Profil

    public ObservableCollection<string> InstalledVersions { get; } = [];

    public ObservableCollection<ModLoaderItem> LoaderOptions { get; } =
    [
        new("fabric"),
        new("forge"),
        new("neoforge"),
        new("quilt"),
        new("liteloader"),
    ];

    public ObservableCollection<JavaRuntimeItem> JavaRuntimes { get; } = [];

    [ObservableProperty]
    public partial string SelectedVersion { get; set; } = "";

    [ObservableProperty]
    public partial ModLoaderItem? SelectedLoaderOption { get; set; }

    [ObservableProperty]
    public partial int ModCount { get; set; }

    [ObservableProperty]
    public partial string JavaPath { get; set; } = "";

    [ObservableProperty]
    public partial int MaxRamMb { get; set; } = 2048;

    [ObservableProperty]
    public partial bool IsJavaScanning { get; set; }

    [ObservableProperty]
    public partial string AutoDetectedJava { get; set; } = "";

    public bool HasJavaRuntimes => JavaRuntimes.Count > 0;

    public string RamLabel => $"{MaxRamMb} MB";

    /// <summary>Lokalisierter Mods-Zähler (z. B. „4 installiert“).</summary>
    public string ModCountLabel => t("Profile.ModCount", ModCount);

    // ---------------------------------------------------------------- Status / Spielen

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    // ---------------------------------------------------------------- Unter-Ansichten

    [ObservableProperty]
    public partial ViewModelBase? SubPage { get; set; }

    [ObservableProperty]
    public partial bool IsInSubPage { get; set; }

    /// <summary>True, wenn die Profil-Übersicht statt einer Unter-Ansicht gezeigt wird.</summary>
    public bool IsOverview => !IsInSubPage;

    public string HeaderTitle => (IsInSubPage, SubPage) switch
    {
        (true, VersionsViewModel) => t("Versions.Title"),
        (true, ModsViewModel) => t("Mods.Title"),
        _ => t("Profile.Title"),
    };

    public string HeaderSubtitle => (IsInSubPage, SubPage) switch
    {
        (true, VersionsViewModel) => t("Profile.VersionsSubtitle"),
        (true, ModsViewModel) => t("Profile.ModsSubtitle"),
        _ => t("Profile.Subtitle"),
    };

    partial void OnIsInSubPageChanged(bool value)
    {
        OnPropertyChanged(nameof(IsOverview));
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderSubtitle));
    }

    public ProfileViewModel(
        SettingsService settingsService,
        GameLauncherService gameLauncher,
        MojangVersionService versionService,
        ModService modService,
        ProfileService profileService)
    {
        _settingsService = settingsService;
        _gameLauncher = gameLauncher;
        _versionService = versionService;
        _modService = modService;
        _profileService = profileService;
        StatusMessage = t("Home.Ready");

        LocalizationManager.Instance.LanguageChanged += () =>
        {
            OnPropertyChanged(nameof(ActiveProfileName));
            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderSubtitle));
        };
    }

    // ---------------------------------------------------------------- Lebenszyklus

    /// <summary>Frischt die Übersicht auf (wird beim Öffnen der Seite aufgerufen).</summary>
    public void Refresh()
    {
        _isRefreshing = true;
        try
        {
            var s = _settingsService.Current;

            JavaPath = s.JavaPath;
            MaxRamMb = s.MaxRamMb;
            SelectedLoaderOption = LoaderOptions.FirstOrDefault(o => o.Value == s.ModLoader) ?? LoaderOptions[0];
            SelectedVersion = s.SelectedVersionId;

            if (!_javaScanned)
            {
                _javaScanned = true;
                _ = ScanJavaAsync();
            }
            else
            {
                MarkCurrentJava();
            }

            RefreshOverview();
            if (!IsEditingProfile)
                RebuildProfiles();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    /// <summary>Aktualisiert alle Werte der Profil-Übersicht aus den Einstellungen.</summary>
    private void RefreshOverview()
    {
        var s = _settingsService.Current;
        SelectedVersion = s.SelectedVersionId;
        SelectedLoaderOption = LoaderOptions.FirstOrDefault(o => o.Value == s.ModLoader) ?? LoaderOptions[0];
        JavaPath = s.JavaPath;
        MaxRamMb = s.MaxRamMb;
        ModCount = new ModService().ScanMods(s.ModsDirectory).Count;
        RebuildInstalledVersions();
        OnPropertyChanged(nameof(ActiveProfileName));
    }

    partial void OnModCountChanged(int value) => OnPropertyChanged(nameof(ModCountLabel));

    /// <summary>Baut die Liste der installierten Versionen für die Auswahl neu auf.</summary>
    private void RebuildInstalledVersions()
    {
        var dir = _settingsService.Current.VersionsDirectory;
        var list = Directory.Exists(dir)
            ? Directory.EnumerateDirectories(dir).Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n)).Cast<string>().ToList()
            : [];

        list.Sort(CompareMcVersions);

        var current = _settingsService.Current.SelectedVersionId;
        if (!string.IsNullOrWhiteSpace(current) && !list.Contains(current))
            list.Add(current);

        InstalledVersions.Clear();
        foreach (var v in list)
            InstalledVersions.Add(v);

        SelectedVersion = current;
    }

    /// <summary>Sortiert Versionen absteigend (neueste zuerst) mit natürlichem Vergleich.</summary>
    private static int CompareMcVersions(string a, string b)
    {
        var sa = a.Split(['.', '-'], StringSplitOptions.RemoveEmptyEntries);
        var sb = b.Split(['.', '-'], StringSplitOptions.RemoveEmptyEntries);
        var n = Math.Max(sa.Length, sb.Length);
        for (var i = 0; i < n; i++)
        {
            var cmp = CompareSegment(i < sa.Length ? sa[i] : "", i < sb.Length ? sb[i] : "");
            if (cmp != 0)
                return cmp;
        }
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareSegment(string x, string y)
    {
        if (x.Length == 0)
            return y.Length == 0 ? 0 : -1;
        if (y.Length == 0)
            return 1;

        if (int.TryParse(x, out var nx) && int.TryParse(y, out var ny))
            return ny.CompareTo(nx);

        // Nicht-numerische Segmente (z. B. "pre", "rc") absteigend vergleichen.
        return string.Compare(y, x, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- Profil-Aktionen

    partial void OnSelectedProfileChanged(ProfileItem? value)
    {
        OnPropertyChanged(nameof(HasProfileSelected));
        IsDeleteArmed = false;

        if (value is null)
            return;

        if (value.IsDefault)
            _profileService.ApplyDefault();
        else if (value.Profile is { } profile)
            _profileService.ApplyProfile(profile);

        MarkActiveProfiles();
        RefreshOverview();
    }

    partial void OnIsEditingProfileChanged(bool value)
    {
        if (!value)
            _renameTarget = null;
    }

    /// <summary>Baut die Profil-Liste neu auf (Standard + alle Profile).</summary>
    private void RebuildProfiles()
    {
        var selectedId = _profileService.Settings.SelectedProfileId;

        ProfileItems.Clear();
        ProfileItems.Add(new ProfileItem(null)
        {
            IsActive = string.IsNullOrEmpty(selectedId),
        });
        foreach (var profile in _profileService.Settings.Profiles)
            ProfileItems.Add(new ProfileItem(profile) { IsActive = profile.Id == selectedId });

        SelectedProfile = ProfileItems.FirstOrDefault(i => !i.IsDefault && i.Profile?.Id == selectedId)
                          ?? ProfileItems[0];
    }

    /// <summary>Setzt die IsActive-Markierung der Chips auf das aktive Profil.</summary>
    private void MarkActiveProfiles()
    {
        var selectedId = _profileService.Settings.SelectedProfileId;
        foreach (var item in ProfileItems)
            item.IsActive = item.IsDefault
                ? string.IsNullOrEmpty(selectedId)
                : item.Profile?.Id == selectedId;
    }

    [RelayCommand]
    private void NewProfile()
    {
        _renameTarget = null;
        ProfileEditName = "";
        IsDeleteArmed = false;
        ProfileStatus = "";
        IsEditingProfile = true;
    }

    [RelayCommand]
    private void RenameProfile()
    {
        if (SelectedProfile is not { Profile: { } profile })
            return;

        _renameTarget = profile;
        ProfileEditName = profile.Name;
        IsDeleteArmed = false;
        ProfileStatus = "";
        IsEditingProfile = true;
    }

    [RelayCommand]
    private void DuplicateProfile()
    {
        if (SelectedProfile is not { Profile: { } profile })
            return;

        var result = _profileService.DuplicateProfile(profile);
        RebuildProfiles();
        ProfileStatus = result.Message;
    }

    /// <summary>Zweistufiges Löschen: erster Klick bewaffnet, zweiter löscht wirklich.</summary>
    [RelayCommand]
    private void DeleteProfile()
    {
        if (SelectedProfile is not { Profile: { } profile })
            return;

        if (!IsDeleteArmed)
        {
            IsDeleteArmed = true;
            ProfileStatus = t("Home.ProfileDeleteHint", profile.Name);
            return;
        }

        var result = _profileService.DeleteProfile(profile);
        IsDeleteArmed = false;
        RebuildProfiles();
        ProfileStatus = result.Message;
    }

    [RelayCommand]
    private void ApplyProfileEdit()
    {
        var name = ProfileEditName?.Trim() ?? "";

        if (_renameTarget is { } target)
        {
            var result = _profileService.RenameProfile(target, name);
            ProfileStatus = result.Message;
            IsEditingProfile = false;
            if (result.Success)
                RebuildProfiles();
            return;
        }

        var created = _profileService.CreateProfile(name);
        IsEditingProfile = false;
        if (created.Success)
            RebuildProfiles();
        else
            ProfileStatus = created.Message;
    }

    [RelayCommand]
    private void CancelProfileEdit()
    {
        IsEditingProfile = false;
        ProfileStatus = "";
    }

    // ---------------------------------------------------------------- Version / Loader / Java

    partial void OnSelectedVersionChanged(string value)
    {
        if (_isRefreshing)
            return;

        var s = _settingsService.Current;
        if (s.SelectedVersionId != value)
        {
            // Write-through: globale Auswahl + aktives Profil.
            _profileService.SyncVersion(value);
            _settingsService.Save();
        }
    }

    partial void OnSelectedLoaderOptionChanged(ModLoaderItem? value)
    {
        if (_isRefreshing || value is null)
            return;

        var s = _settingsService.Current;
        if (s.ModLoader != value.Value)
        {
            // Write-through: globale Auswahl + aktives Profil.
            _profileService.SyncLoader(value.Value);
            _settingsService.Save();
        }
    }

    partial void OnJavaPathChanged(string value)
    {
        if (_isRefreshing)
            return;

        _profileService.SyncJavaPath(value);
        _settingsService.Save();
    }

    partial void OnMaxRamMbChanged(int value)
    {
        OnPropertyChanged(nameof(RamLabel));
        if (_isRefreshing)
            return;

        _profileService.SyncRam(value);
        _settingsService.Save();
    }

    [RelayCommand]
    private async Task ScanJavaAsync()
    {
        if (IsJavaScanning)
            return;

        IsJavaScanning = true;
        JavaRuntimes.Clear();
        OnPropertyChanged(nameof(HasJavaRuntimes));

        try
        {
            var preferred = _settingsService.Current.JavaPath;
            var runtimes = await Task.Run(() => _javaService.DetectAll(preferred));

            foreach (var runtime in runtimes)
            {
                JavaRuntimes.Add(new JavaRuntimeItem(runtime.Path, runtime.Version)
                {
                    IsCurrent = string.Equals(runtime.Path, preferred, StringComparison.OrdinalIgnoreCase),
                });
            }

            OnPropertyChanged(nameof(HasJavaRuntimes));

            AutoDetectedJava = runtimes.Count == 0
                ? t("Settings.JavaNotFound")
                : t("Settings.JavaFound",
                    string.IsNullOrEmpty(runtimes[0].Version) ? runtimes[0].Path : runtimes[0].Version);
        }
        catch
        {
            AutoDetectedJava = t("Settings.JavaNotFound");
        }
        finally
        {
            IsJavaScanning = false;
        }
    }

    private void MarkCurrentJava()
    {
        var preferred = _settingsService.Current.JavaPath;
        foreach (var runtime in JavaRuntimes)
            runtime.IsCurrent = string.Equals(runtime.Path, preferred, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Übernimmt eine erkannte Java-Runtime als aktiven Java-Pfad.</summary>
    public void SelectJava(JavaRuntimeItem? item)
    {
        if (item is null)
            return;

        JavaPath = item.Path;
        foreach (var runtime in JavaRuntimes)
            runtime.IsCurrent = runtime == item;

        AutoDetectedJava = t("Settings.JavaFound",
            string.IsNullOrEmpty(item.Version) ? item.Path : item.Version);
    }

    // ---------------------------------------------------------------- Unter-Ansichten

    /// <summary>Öffnet die Versions-Verwaltung (installieren, aktive Version wählen).</summary>
    public void OpenVersions()
    {
        _versions ??= new VersionsViewModel(_settingsService, _versionService, _profileService);
        SubPage = _versions;
        IsInSubPage = true;
    }

    /// <summary>Öffnet die Mods-Verwaltung (lokale Mods + Modrinth).</summary>
    public void OpenMods()
    {
        _mods ??= new ModsViewModel(_settingsService, _modService, _profileService)
        {
            OpenVersionsRequested = OpenVersions,
        };
        _mods.Refresh();
        _mods.Start();
        SubPage = _mods;
        IsInSubPage = true;
    }

    /// <summary>Zurück zur Profil-Übersicht (frischt die Werte auf).</summary>
    public void BackToOverview()
    {
        IsInSubPage = false;
        SubPage = null;
        RefreshOverview();
    }

    // ---------------------------------------------------------------- Spiel starten

    [RelayCommand]
    private async Task PlayAsync()
    {
        if (IsBusy)
            return;

        // Java/RAM sind per Write-through bereits übernommen – hier nur zur Sicherheit speichern.
        _settingsService.Save();

        IsBusy = true;
        StatusMessage = t("Home.StartingGame");
        try
        {
            var result = await _gameLauncher.LaunchAsync(_settingsService.Current);
            AccountDiagnostics.Log(
                $"ProfileViewModel.PlayAsync: Ergebnis '{result.MessageKey}' (Success: {result.Success}).");
            StatusMessage = t(result.MessageKey, result.Args);
        }
        catch (Exception ex)
        {
            AccountDiagnostics.Log($"ProfileViewModel.PlayAsync fehlgeschlagen: {ex.GetType().Name}: {ex.Message}");
            StatusMessage = t("Home.GameLaunchFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
