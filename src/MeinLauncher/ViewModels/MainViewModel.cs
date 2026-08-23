using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeinLauncher.Models;
using MeinLauncher.Services;

namespace MeinLauncher.ViewModels;

/// <summary>
/// Haupt-ViewModel: steuert die Navigation zwischen den Seiten, verwaltet das
/// Hintergrundbild und die lokalisierte Seitenleiste.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly MicrosoftAccountService _accountService;
    private readonly GameLauncherService _gameLauncher;
    private readonly MojangVersionService _versionService;
    private readonly ModService _modService;
    private readonly ProfileService _profileService;
    private readonly UpdateService _updateService;

    private HomeViewModel? _home;
    private ProfileViewModel? _profile;
    private SettingsViewModel? _settingsVm;
    private NewsViewModel? _news;
    private UpdateInfo? _pendingUpdate;
    private string? _pendingExtractedDir;

    [ObservableProperty]
    public partial ObservableCollection<NavItem> NavItems { get; set; } = [];

    [ObservableProperty]
    public partial NavItem? SelectedNavItem { get; set; }

    [ObservableProperty]
    public partial ViewModelBase? CurrentPage { get; set; }

    [ObservableProperty]
    public partial Bitmap? BackgroundImage { get; set; }

    /// <summary>Dezente Einblendung des Hintergrundbilds (keine Ablenkung).</summary>
    public double BackgroundOpacity => 0.55;

    /// <summary>Aktuelle Launcher-Version für UI-Anzeige.</summary>
    public string Version => $"Kulka Client v{AppVersion.Current}";

    // --- Update ---

    [ObservableProperty]
    public partial bool IsUpdateAvailable { get; set; }

    [ObservableProperty]
    public partial string? UpdateVersion { get; set; }

    /// <summary>Lokalisierte Update-Nachricht mit Version.</summary>
    public string UpdateText => L.UpdateAvailable.Replace("{0}", UpdateVersion ?? "?");

    [ObservableProperty]
    public partial bool IsDownloading { get; set; }

    [ObservableProperty]
    public partial double DownloadProgress { get; set; }

    [ObservableProperty]
    public partial bool IsUpdateError { get; set; }

    [ObservableProperty]
    public partial string? UpdateErrorMessage { get; set; }

    public bool HasBackgroundImage => BackgroundImage is not null;

    /// <summary>Die geteilte, live bearbeitbare Hintergrund-Konfiguration (dekorative Elemente).</summary>
    public BackgroundConfig BackgroundConfig => _settingsService.Current.Background;

    /// <summary>Zeigt das Bild nur, wenn der Hintergrund insgesamt aktiviert ist.</summary>
    public bool IsImageVisible => HasBackgroundImage && _settingsService.Current.Background.Enabled;

    /// <summary>Dekorativer Hintergrund sichtbar, sofern aktiviert und Elementtypen gewählt.</summary>
    public bool IsBackgroundDecorVisible =>
        _settingsService.Current.Background.Enabled && _settingsService.Current.Background.Kinds.Count > 0;

    public MainViewModel(SettingsService settingsService, MicrosoftAccountService accountService)
    {
        _settingsService = settingsService;
        _accountService = accountService;
        _gameLauncher = new GameLauncherService(accountService);
        _versionService = new MojangVersionService();
        _modService = new ModService();
        _profileService = new ProfileService(settingsService);
        _updateService = new UpdateService();

        var homeItem = new NavItem("Nav.Start", Icons.Home, CreateHome);
        var profileItem = new NavItem("Nav.Profile", Icons.Profile, CreateProfile);
        var settingsItem = new NavItem("Nav.Settings", Icons.Settings, CreateSettings);
        var newsItem = new NavItem("Nav.News", Icons.News, CreateNews);

        NavItems = [homeItem, profileItem, settingsItem, newsItem];
        SelectedNavItem = homeItem;

        // News-Status beim Start prüfen: roter Punkt, sobald neue Uploads bekannt sind.
        // Ein periodischer Timer im NewsViewModel prüft alle 5 Minuten erneut.
        _news = new NewsViewModel(_settingsService);
        _news.UnreadStateChanged += unread =>
            Dispatcher.UIThread.Post(() => newsItem.HasUnread = unread);
        _ = _news.CheckForNewVideosAsync();

        // Update-Check im Hintergrund (blockiert den Launcher nicht).
        // Store-Versionen (MSIX) überspringen dies – Store-Updates ersetzen den Auto-Updater.
        if (!UpdateService.IsStorePackage)
            _ = CheckForUpdateInBackgroundAsync();

        LocalizationManager.Instance.LanguageChanged += () =>
        {
            foreach (var item in NavItems)
                item.RefreshTitle();
        };

        RefreshBackground();
    }

    /// <summary>Lädt das Hintergrundbild aus den Einstellungen neu (oder entfernt es).</summary>
    public void RefreshBackground()
    {
        var path = _settingsService.Current.BackgroundImagePath;

        Bitmap? bitmap = null;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                bitmap = new Bitmap(path);
            }
            catch
            {
                bitmap = null;
            }
        }

        BackgroundImage = bitmap;
        OnPropertyChanged(nameof(HasBackgroundImage));
        NotifyBackgroundChanged();
    }

    /// <summary>
    /// Leichte Benachrichtigung: die Sichtbarkeit von Bild-/Dekor-Ebene hängt von der
    /// (live geänderten) Hintergrund-Konfiguration ab. Lädt kein Bild neu.
    /// </summary>
    public void NotifyBackgroundChanged()
    {
        OnPropertyChanged(nameof(IsImageVisible));
        OnPropertyChanged(nameof(IsBackgroundDecorVisible));
    }

    private ViewModelBase CreateHome()
    {
        _home ??= new HomeViewModel(_settingsService, _gameLauncher, _profileService)
        {
            OpenProfileRequested = () => NavigateTo("Nav.Profile"),
        };
        _home.Refresh();
        return _home;
    }

    private ViewModelBase CreateProfile()
    {
        _profile ??= new ProfileViewModel(
            _settingsService, _gameLauncher, _versionService, _modService, _profileService);
        _profile.Refresh();
        return _profile;
    }

    private ViewModelBase CreateSettings()
    {
        _settingsVm ??= new SettingsViewModel(
            _settingsService, _profileService, _accountService,
            RefreshBackground, NotifyBackgroundChanged);
        // Beim Öffnen mit den Einstellungen des (ggf. gewechselten) aktiven Profils abgleichen.
        _settingsVm.LoadFromSettings();
        // Kategorie-Übersicht zurücksetzen: Das View wird vom ViewLocator bei jeder
        // Navigation neu erstellt (leerer SectionHost), daher muss der ViewModel-State
        // harmonieren – CurrentSection auf null, damit die Übersicht gezeigt wird.
        _settingsVm.CurrentSection = null;
        return _settingsVm;
    }

    private ViewModelBase CreateNews()
    {
        _news ??= new NewsViewModel(_settingsService);
        _news.OnOpened();
        return _news;
    }

    private void NavigateTo(string key)
    {
        SelectedNavItem = NavItems.FirstOrDefault(n => n.Key == key);
    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value is not null)
            CurrentPage = value.Factory();
    }

    partial void OnUpdateVersionChanged(string? value)
    {
        OnPropertyChanged(nameof(UpdateText));
    }

    // --------------------------------------------------------- Auto-Update

    private async System.Threading.Tasks.Task CheckForUpdateInBackgroundAsync()
    {
        try
        {
            var update = await _updateService.CheckForUpdateAsync();
            if (update is { IsAvailable: true })
            {
                _pendingUpdate = update;
                UpdateVersion = update.RemoteVersion;
                IsUpdateAvailable = true;
            }
        }
        catch
        {
            // Offline / Fehler → still weitermachen.
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task DownloadUpdateAsync()
    {
        if (UpdateService.IsStorePackage)
            return; // Store-Updates übernehmen das.
        if (_pendingUpdate is not { DownloadUrl: not null })
            return;

        IsDownloading = true;
        IsUpdateError = false;
        UpdateErrorMessage = null;
        DownloadProgress = 0;

        try
        {
            var progress = new Progress<double>(p => DownloadProgress = p);
            var extractedDir = await _updateService.DownloadAndExtractAsync(
                _pendingUpdate, progress, CancellationToken.None);

            _pendingExtractedDir = extractedDir;
            IsDownloading = false;

            // Sofort installieren und neustarten.
            UpdateService.StartUpdateAndExit(extractedDir);
        }
        catch (Exception ex)
        {
            IsDownloading = false;
            IsUpdateError = true;
            UpdateErrorMessage = L.UpdateFailed.Replace("{0}", ex.Message);
        }
    }

    [RelayCommand]
    private void DismissUpdate()
    {
        IsUpdateAvailable = false;
        _pendingUpdate = null;
    }
}
