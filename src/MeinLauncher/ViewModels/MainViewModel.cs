using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
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

    private HomeViewModel? _home;
    private ProfileViewModel? _profile;
    private SettingsViewModel? _settingsVm;
    private NewsViewModel? _news;
    private bool _newsOpened;

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

        var homeItem = new NavItem("Nav.Start", Icons.Home, CreateHome);
        var profileItem = new NavItem("Nav.Profile", Icons.Profile, CreateProfile);
        var settingsItem = new NavItem("Nav.Settings", Icons.Settings, CreateSettings);
        var newsItem = new NavItem("Nav.News", Icons.News, CreateNews);

        NavItems = [homeItem, profileItem, settingsItem, newsItem];
        SelectedNavItem = homeItem;

        // News-Status beim Start prüfen: roter Punkt, sobald neue Uploads bekannt sind.
        // Erst das Öffnen der News-Seite markiert sie als „gesehen“ und setzt den Punkt zurück.
        _news = new NewsViewModel(_settingsService);
        _news.UnreadStateChanged += unread =>
            newsItem.HasUnread = _newsOpened ? false : unread;
        _ = _news.CheckForNewVideosAsync();

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
        return _settingsVm;
    }

    private ViewModelBase CreateNews()
    {
        _newsOpened = true;
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
}
