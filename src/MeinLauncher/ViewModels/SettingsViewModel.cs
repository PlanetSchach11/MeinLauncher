using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeinLauncher.Models;
using MeinLauncher.Services;

namespace MeinLauncher.ViewModels;

/// <summary>
/// Einstellungen-Seite: aufgeteilt in Kategorien (Design, Oberfläche, Minecraft,
/// Mods, Konto, Allgemein) mit kompakter Navigation. Enthält außerdem Java-Runtimes,
/// Spieldaten, RAM sowie Theme, Akzentfarbe, Transparenz, Hintergrund und Sprache.
/// Die Werte gelten für das aktive Spielprofil (Write-through über ProfileService).
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly ProfileService _profileService;
    private readonly MicrosoftAccountService _accountService;
    private readonly JavaService _javaService = new();
    private readonly ISkinCapeService _skinCapeService = new LocalSkinCapeService();
    private readonly Action? _backgroundChanged;

    private static readonly HttpClient ProfileHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Die sechs Einstellungs-Kategorien (Übersichtskarten).</summary>
    public ObservableCollection<SettingsSectionItem> Sections { get; } =
    [
        new("design", "🎨", "Settings.CategoryDesign", "Settings.CategoryDesignSubtitle"),
        new("ui", "🖥️", "Settings.CategoryUi", "Settings.CategoryUiSubtitle"),
        new("minecraft", "🎮", "Settings.CategoryMinecraft", "Settings.CategoryMinecraftSubtitle"),
        new("mods", "🧩", "Settings.CategoryMods", "Settings.CategoryModsSubtitle"),
        new("account", "👤", "Settings.CategoryAccount", "Settings.CategoryAccountSubtitle"),
        new("general", "⚙️", "Settings.CategoryGeneral", "Settings.CategoryGeneralSubtitle"),
    ];

    [ObservableProperty]
    public partial SettingsSectionItem? CurrentSection { get; set; }

    /// <summary>Benachrichtigt abgeleitete Eigenschaften bei einem Kategoriewechsel.</summary>
    partial void OnCurrentSectionChanged(SettingsSectionItem? value)
    {
        OnPropertyChanged(nameof(IsInSection));
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderSubtitle));
    }

    /// <summary>True, wenn eine Kategorie-Unterseite statt der Übersicht gezeigt wird.</summary>
    public bool IsInSection => CurrentSection is not null;

    public string HeaderTitle => CurrentSection?.Title ?? t("Settings.Title");

    public string HeaderSubtitle => CurrentSection?.Subtitle ?? t("Settings.Subtitle");

    /// <summary>Mod-Loader-Auswahl (Mods-Kategorie, wird direkt übernommen).</summary>
    public ObservableCollection<ModLoaderItem> LoaderOptions { get; } =
    [
        new ModLoaderItem("fabric"),
        new ModLoaderItem("forge"),
        new ModLoaderItem("neoforge"),
        new ModLoaderItem("quilt"),
        new ModLoaderItem("liteloader"),
    ];

    [ObservableProperty]
    public partial ModLoaderItem? SelectedLoaderOption { get; set; }

    /// <summary>Aktueller Mods-Ordner (profilabhängig).</summary>
    public string ModsFolderPath => _settingsService.Current.ModsDirectory;

    [ObservableProperty]
    public partial string Username { get; set; } = "";

    [ObservableProperty]
    public partial string GameDirectory { get; set; } = "";

    [ObservableProperty]
    public partial string JavaPath { get; set; } = "";

    [ObservableProperty]
    public partial int MaxRamMb { get; set; } = 2048;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "";

    [ObservableProperty]
    public partial string AutoDetectedJava { get; set; } = "";

    [ObservableProperty]
    public partial bool SoundEnabled { get; set; } = true;

    [ObservableProperty]
    public partial int SoundVolume { get; set; } = 25;

    [ObservableProperty]
    public partial string SkinCapeStatus { get; set; } = "";

    [ObservableProperty]
    public partial bool IsJavaScanning { get; set; }

    /// <summary>Microsoft-Application (client) ID für die Anmeldung mit dem Microsoft-Konto.</summary>
    [ObservableProperty]
    public partial string MicrosoftClientId { get; set; } = "";

    /// <summary>Verständliche Statusmeldung zum Microsoft-Konto (Login, Fehler, angemeldet).</summary>
    [ObservableProperty]
    public partial string MicrosoftStatus { get; set; } = "";

    [ObservableProperty]
    public partial bool IsMicrosoftBusy { get; set; }

    [ObservableProperty]
    public partial bool IsMicrosoftLoggedIn { get; set; }

    [ObservableProperty]
    public partial string MicrosoftUsername { get; set; } = "";

    /// <summary>Profilbild (Kopf des Minecraft-Skins), wenn ein Skin vorhanden ist.</summary>
    [ObservableProperty]
    public partial IImage? MicrosoftProfileImage { get; set; }

    public ObservableCollection<LocalizedItem> ThemeOptions { get; } =
        [new("Settings.Dark"), new("Settings.Light")];

    public ObservableCollection<LocalizedItem> TransparencyOptions { get; } =
        [new("Settings.TransparencyNone"), new("Settings.TransparencyLight"), new("Settings.TransparencyStrong")];

    public ObservableCollection<LocalizedItem> LanguageOptions { get; } =
        [new("Settings.German"), new("Settings.English")];

    public ObservableCollection<AccentItem> AccentOptions { get; } = [];

    public ObservableCollection<JavaRuntimeItem> JavaRuntimes { get; } = [];

    [ObservableProperty]
    public partial LocalizedItem? SelectedThemeItem { get; set; }

    [ObservableProperty]
    public partial LocalizedItem? SelectedTransparencyItem { get; set; }

    [ObservableProperty]
    public partial LocalizedItem? SelectedLanguageItem { get; set; }

    [ObservableProperty]
    public partial AccentItem? SelectedAccentItem { get; set; }

    /// <summary>Unter-ViewModel des Hintergrund-Editors (Einstellungen → Hintergrund).</summary>
    public BackgroundSettingsViewModel BackgroundSettings { get; }

    public bool HasJavaRuntimes => JavaRuntimes.Count > 0;

    public string RamLabel => $"{MaxRamMb} MB";

    public string SoundVolumeLabel => $"{SoundVolume} %";

    partial void OnMaxRamMbChanged(int value) => OnPropertyChanged(nameof(RamLabel));

    // Live-Übernahme wie bei den übrigen Design-Einstellungen.
    partial void OnSoundEnabledChanged(bool value) => _settingsService.Current.SoundEnabled = value;

    partial void OnSoundVolumeChanged(int value)
    {
        _settingsService.Current.SoundVolume = value;
        OnPropertyChanged(nameof(SoundVolumeLabel));
    }

    public SettingsViewModel(
        SettingsService settingsService,
        ProfileService profileService,
        MicrosoftAccountService accountService,
        Action? backgroundChanged,
        Action? backgroundTweaked = null)
    {
        _settingsService = settingsService;
        _profileService = profileService;
        _accountService = accountService;
        _backgroundChanged = backgroundChanged;

        _accountService.SessionChanged += OnAccountSessionChanged;

        BackgroundSettings = new BackgroundSettingsViewModel(
            settingsService.Current, backgroundChanged, backgroundTweaked);

        foreach (var key in ThemeManager.Accents.Keys)
            AccentOptions.Add(new AccentItem(key));

        LoadFromSettings();

        _ = ScanJavaAsync();
        _ = RefreshSkinsAsync();
        _ = RestoreSessionAsync();

        LocalizationManager.Instance.LanguageChanged += () =>
        {
            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderSubtitle));
        };
    }

    public void LoadFromSettings()
    {
        var s = _settingsService.Current;

        Username = s.Username;
        GameDirectory = s.GameDirectory;
        JavaPath = s.JavaPath;
        MaxRamMb = s.MaxRamMb;
        SoundEnabled = s.SoundEnabled;
        SoundVolume = s.SoundVolume;
        MicrosoftClientId = s.MicrosoftClientId;

        SelectedThemeItem = ThemeOptions.FirstOrDefault(o => o.Key == (s.Theme == "Light" ? "Settings.Light" : "Settings.Dark"));
        SelectedTransparencyItem = TransparencyOptions.FirstOrDefault(o => o.Key == s.Transparency switch
        {
            "Light" => "Settings.TransparencyLight",
            "Strong" => "Settings.TransparencyStrong",
            _ => "Settings.TransparencyNone",
        });
        SelectedLanguageItem = LanguageOptions.FirstOrDefault(o => o.Key == (s.Language == "en" ? "Settings.English" : "Settings.German"));
        SelectedAccentItem = AccentOptions.FirstOrDefault(o => o.Key == s.Accent);
        if (SelectedAccentItem is not null)
        {
            foreach (var item in AccentOptions)
                item.IsSelected = item == SelectedAccentItem;
        }

        SelectedLoaderOption = LoaderOptions.FirstOrDefault(o => o.Value == s.ModLoader) ?? LoaderOptions[0];
    }

    // ---------------------------------------------------------------- Kategorie-Navigation

    /// <summary>Wechselt in eine Kategorie-Unterseite (Key aus <see cref="Sections"/>).</summary>
    public void NavigateToSection(string? key)
    {
        CurrentSection = Sections.FirstOrDefault(s => s.Key == key);

        // Sektionsdaten beim Öffnen auffrischen (Loader/Ordner können sich geändert haben).
        if (CurrentSection?.Key == "mods")
            SyncModsSection();
        if (CurrentSection?.Key == "minecraft")
            JavaPath = _settingsService.Current.JavaPath;
    }

    /// <summary>Zurück zur Kategorie-Übersicht.</summary>
    public void BackToOverview()
    {
        CurrentSection = null;
    }

    /// <summary>Gleicht die Loader-Auswahl und den Mods-Ordner mit den Einstellungen ab.</summary>
    public void SyncModsSection()
    {
        SelectedLoaderOption = LoaderOptions.FirstOrDefault(o => o.Value == _settingsService.Current.ModLoader)
                               ?? LoaderOptions[0];
        OnPropertyChanged(nameof(ModsFolderPath));
    }

    partial void OnSelectedLoaderOptionChanged(ModLoaderItem? value)
    {
        if (value is null)
            return;

        // Mod-Loader sofort übernehmen (Write-through inkl. aktives Profil).
        _profileService.SyncLoader(value.Value);
        _settingsService.Save();
    }

    // ---------------------------------------------------------------- Live-Anwendung der Design-Auswahl

    partial void OnSelectedThemeItemChanged(LocalizedItem? value)
    {
        if (value is null)
            return;

        _settingsService.Current.Theme = value.Key == "Settings.Light" ? "Light" : "Dark";
        ThemeManager.Apply(_settingsService.Current);
    }

    partial void OnSelectedAccentItemChanged(AccentItem? value)
    {
        if (value is null)
            return;

        _settingsService.Current.Accent = value.Key;
        foreach (var item in AccentOptions)
            item.IsSelected = item == value;

        ThemeManager.Apply(_settingsService.Current);
    }

    partial void OnSelectedTransparencyItemChanged(LocalizedItem? value)
    {
        if (value is null)
            return;

        _settingsService.Current.Transparency = value.Key switch
        {
            "Settings.TransparencyLight" => "Light",
            "Settings.TransparencyStrong" => "Strong",
            _ => "None",
        };
        ThemeManager.Apply(_settingsService.Current);
    }

    partial void OnSelectedLanguageItemChanged(LocalizedItem? value)
    {
        if (value is null)
            return;

        _settingsService.Current.Language = value.Key == "Settings.English" ? "en" : "de";
        LocalizationManager.Instance.SetLanguage(_settingsService.Current.Language);
    }

    // ---------------------------------------------------------------- Java

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

    // ---------------------------------------------------------------- Skins & Capes

    [RelayCommand]
    private async Task RefreshSkinsAsync()
    {
        var s = _settingsService.Current;
        try
        {
            var result = await _skinCapeService.ScanAsync(s.GameDirectory);
            SkinCapeStatus = t("Settings.SkinCapesCount", result.SkinCount, result.CapeCount);
        }
        catch (Exception ex)
        {
            SkinCapeStatus = t("Mods.Error", ex.Message);
        }
    }

    // ---------------------------------------------------------------- Microsoft-Konto

    [RelayCommand]
    private async Task LoginMicrosoftAsync()
    {
        if (IsMicrosoftBusy)
            return;

        var clientId = MicrosoftClientId?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(clientId))
        {
            MicrosoftStatus = t("Settings.MicrosoftLoginNoClientId");
            return;
        }

        // Client-ID sofort sichern – auch wenn „Speichern“ nicht extra geklickt wurde.
        // So steht sie beim nächsten App-Start für das Wiederherstellen bereit.
        _settingsService.Current.MicrosoftClientId = clientId;
        _settingsService.Save();

        IsMicrosoftBusy = true;
        var progress = new Progress<MicrosoftLoginStage>(stage =>
        {
            MicrosoftStatus = stage switch
            {
                MicrosoftLoginStage.OpeningBrowser => t("Settings.MicrosoftLoginOpeningBrowser"),
                MicrosoftLoginStage.WaitingForBrowser => t("Settings.MicrosoftLoginWaiting"),
                _ => t("Settings.MicrosoftLoginChecking"),
            };
        });

        try
        {
            await _accountService.LoginAsync(clientId, progress);
            // Status/Profil werden über SessionChanged von OnAccountSessionChanged gesetzt.
        }
        catch (OperationCanceledException ex)
        {
            AccountDiagnostics.Log($"LoginMicrosoftAsync abgebrochen: {ex.Message}");
            MicrosoftStatus = string.IsNullOrWhiteSpace(ex.Message)
                ? t("Settings.MicrosoftLoginCancelled")
                : ex.Message;
        }
        catch (Exception ex)
        {
            AccountDiagnostics.Log($"LoginMicrosoftAsync fehlgeschlagen: {ex.GetType().Name}: {ex.Message}");
            MicrosoftStatus = t("Settings.MicrosoftLoginError", ex.Message);
        }
        finally
        {
            IsMicrosoftBusy = false;
        }
    }

    /// <summary>
    /// Stellt die gespeicherte Session wieder her (Refresh per Token, ohne Browser).
    /// Läuft still im Hintergrund – Fehler werden nicht als Meldung angezeigt.
    /// </summary>
    private async Task RestoreSessionAsync()
    {
        var clientId = _settingsService.Current.MicrosoftClientId?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(clientId))
            return;

        if (_accountService.CurrentSession is not null)
        {
            ApplySession(_accountService.CurrentSession);
            return;
        }

        MicrosoftStatus = t("Settings.MicrosoftLoginRestoring");
        try
        {
            var session = await _accountService.RestoreAsync(clientId);
            if (session is null && _accountService.CurrentSession is null)
                MicrosoftStatus = "";
            else
                AccountDiagnostics.Log($"RestoreSessionAsync: wiederhergestellt ({session?.MinecraftUsername ?? "?"}).");
        }
        catch (Exception ex)
        {
            AccountDiagnostics.Log($"RestoreSessionAsync fehlgeschlagen: {ex.GetType().Name}: {ex.Message}");
            MicrosoftStatus = "";
        }
    }

    private void OnAccountSessionChanged()
    {
        var session = _accountService.CurrentSession;
        if (session is null)
        {
            IsMicrosoftLoggedIn = false;
            MicrosoftUsername = "";
            MicrosoftProfileImage = null;
            MicrosoftStatus = "";
            return;
        }

        ApplySession(session);
    }

    private void ApplySession(MicrosoftSession session)
    {
        IsMicrosoftLoggedIn = true;
        MicrosoftUsername = session.MinecraftUsername;
        MicrosoftStatus = t("Settings.MicrosoftLoggedInAs", session.MinecraftUsername);
        MicrosoftProfileImage = null;
        _ = LoadProfileImageAsync(session);
    }

    /// <summary>Lädt den Skin des Kontos und zeigt den Kopf als Profilbild an.</summary>
    private async Task LoadProfileImageAsync(MicrosoftSession session)
    {
        if (string.IsNullOrEmpty(session.SkinUrl))
            return;

        try
        {
            var bytes = await ProfileHttpClient.GetByteArrayAsync(session.SkinUrl);

            var image = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                using var stream = new MemoryStream(bytes);
                var source = new Bitmap(stream);
                // Kopf des Skins: im 64x64-Skin liegt er bei (8,8) mit 8x8 Pixeln.
                return (IImage?)new CroppedBitmap(source, new PixelRect(8, 8, 8, 8));
            });

            MicrosoftProfileImage = image;
        }
        catch
        {
            MicrosoftProfileImage = null;
        }
    }

    // ---------------------------------------------------------------- Speichern

    [RelayCommand]
    private void Save()
    {
        var s = _settingsService.Current;

        s.Username = string.IsNullOrWhiteSpace(Username) ? "Spieler" : Username.Trim();
        s.GameDirectory = string.IsNullOrWhiteSpace(GameDirectory)
            ? LauncherSettings.DefaultGameDirectory
            : GameDirectory.Trim();
        s.JavaPath = JavaPath?.Trim() ?? "";
        s.MaxRamMb = MaxRamMb;
        s.Theme = SelectedThemeItem?.Key == "Settings.Light" ? "Light" : "Dark";
        s.Accent = SelectedAccentItem?.Key ?? s.Accent;
        s.Transparency = SelectedTransparencyItem?.Key switch
        {
            "Settings.TransparencyLight" => "Light",
            "Settings.TransparencyStrong" => "Strong",
            _ => "None",
        };
        s.Language = SelectedLanguageItem?.Key == "Settings.English" ? "en" : "de";
        s.SoundEnabled = SoundEnabled;
        s.SoundVolume = SoundVolume;
        s.MicrosoftClientId = MicrosoftClientId?.Trim() ?? "";

        // Java/RAM gehören zum aktiven Profil (Write-through), falls eines gewählt ist.
        if (s.ActiveProfile is { } profile)
        {
            profile.JavaPath = s.JavaPath;
            profile.MaxRamMb = s.MaxRamMb;
        }

        _settingsService.Save();
        _settingsService.EnsureDirectories();

        ThemeManager.Apply(s);
        LocalizationManager.Instance.SetLanguage(s.Language);
        _backgroundChanged?.Invoke();

        OnPropertyChanged(nameof(ModsFolderPath));
        StatusMessage = t("Settings.Saved");
    }
}
