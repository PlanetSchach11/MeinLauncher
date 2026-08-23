using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeinLauncher.Services;

namespace MeinLauncher.ViewModels;

/// <summary>
/// News-Seite: zeigt die neuesten öffentlichen Uploads von @ANG3L0WW (offizieller
/// YouTube-RSS-Feed, kein Scraping). Ein periodischer Timer prüft alle 5 Minuten auf
/// neue Videos und zeigt automatisch den roten Punkt – ohne dass die Seite geöffnet
/// werden muss. Beim Öffnen der Seite werden die Videos als „gesehen" markiert.
/// </summary>
public partial class NewsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly NewsService _newsService = new();
    private Timer? _pollTimer;

    private NewsFetchResult? _lastResult;
    private Task<NewsFetchResult?>? _inFlight;

    public ObservableCollection<NewsVideoItem> Videos { get; } = [];

    /// <summary>Die übrigen Videos (alle außer dem Hero) für die Karten-Liste.</summary>
    public ObservableCollection<NewsVideoItem> Cards { get; } = [];

    [ObservableProperty]
    public partial NewsVideoItem? HeroItem { get; set; }

    [ObservableProperty]
    public partial string HeroTitle { get; set; } = "";

    /// <summary>Z. B. „ANG3L0W · vor 2 Tagen“.</summary>
    [ObservableProperty]
    public partial string HeroChannelLine { get; set; } = "";

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial bool IsInPlayer { get; set; }

    [ObservableProperty]
    public partial NewsVideoItem? PlayerVideo { get; set; }

    public bool HasVideos => Videos.Count > 0;

    public bool HasCards => Cards.Count > 0;

    /// <summary>Weder geladen noch Fehler noch Videos – eigener Leer-Zustand.</summary>
    public bool IsEmpty => !IsLoading && !HasError && !HasVideos;

    /// <summary>
    /// Wird ausgelöst, sobald sich der Ungelesen-Status ändert (true = neue Uploads,
    /// false = als gesehen markiert). Der Launcher steuert darüber den roten Punkt.
    /// </summary>
    public event Action<bool>? UnreadStateChanged;

    public NewsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;

        LocalizationManager.Instance.LanguageChanged += () =>
        {
            foreach (var video in Videos)
                video.PublishedLabel = FormatTimeAgo(video);
            OnPropertyChanged(nameof(HeroChannelLine));
        };

        // Alle 5 Minuten auf neue Videos prüfen (auch wenn die Seite nicht offen ist).
        _pollTimer = new Timer(_ => _ = CheckForNewVideosAsync(), null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Wird bei jeder Navigation auf die News-Seite aufgerufen: Player-Zustand
    /// zurücksetzen und frische Daten laden (und dabei als gesehen markieren).
    /// </summary>
    public void OnOpened()
    {
        ClosePlayer();
        _ = RefreshAndMarkSeenAsync();
    }

    /// <summary>
    /// Leichter Start-Check beim App-Start: meldet nur den Ungelesen-Status an
    /// (roter Punkt), ohne die Seite selbst zu laden oder als gesehen zu markieren.
    /// </summary>
    public async Task CheckForNewVideosAsync()
    {
        var result = await FetchOnceAsync();
        if (result is null)
            return;

        _lastResult = result;
        UnreadStateChanged?.Invoke(ComputeUnread(result));
    }

    /// <summary>Lädt die News; teilt sich einen laufenden Abruf mit anderen Aufrufern.</summary>
    private async Task<NewsFetchResult?> FetchOnceAsync()
    {
        if (_inFlight is not null)
            return await _inFlight;

        var task = _newsService.FetchAsync();
        _inFlight = task;
        try
        {
            return await task;
        }
        finally
        {
            _inFlight = null;
        }
    }

    private async Task RefreshAndMarkSeenAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        HasError = false;
        try
        {
            var result = await FetchOnceAsync();
            if (result is null)
            {
                HasError = true;
            }
            else
            {
                _lastResult = result;
                ApplyResult(result);
                MarkSeen(result);
                UnreadStateChanged?.Invoke(false);
            }
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    [RelayCommand]
    private Task RetryAsync() => RefreshAndMarkSeenAsync();

    private void ApplyResult(NewsFetchResult result)
    {
        Videos.Clear();
        foreach (var video in result.Videos)
        {
            video.PublishedLabel = FormatTimeAgo(video);
            Videos.Add(video);
            _ = _newsService.LoadThumbnailAsync(video);
        }

        Cards.Clear();
        foreach (var video in result.Videos.Skip(1))
            Cards.Add(video);

        HeroItem = result.Videos.FirstOrDefault();
        HeroTitle = HeroItem?.Title ?? "";
        HeroChannelLine = $"{result.ChannelName} · {HeroItem?.PublishedLabel ?? ""}";

        OnPropertyChanged(nameof(HasVideos));
        OnPropertyChanged(nameof(HasCards));
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Neue Uploads vorhanden und noch nicht gesehen?</summary>
    private bool ComputeUnread(NewsFetchResult result)
    {
        if (result.Videos.Count == 0)
            return false;

        var seen = _settingsService.Current.LastSeenNewsVideoId;
        return !string.Equals(result.Videos[0].VideoId, seen, StringComparison.Ordinal);
    }

    /// <summary>Neuestes Video dauerhaft als „gesehen“ speichern.</summary>
    private void MarkSeen(NewsFetchResult result)
    {
        if (result.Videos.Count == 0)
            return;

        var settings = _settingsService.Current;
        var newestId = result.Videos[0].VideoId;
        if (string.Equals(settings.LastSeenNewsVideoId, newestId, StringComparison.Ordinal))
            return;

        settings.LastSeenNewsVideoId = newestId;
        _settingsService.Save();
    }

    private string FormatTimeAgo(NewsVideoItem item)
    {
        if (item.PublishedUtc == DateTimeOffset.MinValue)
            return "";

        var diff = DateTimeOffset.UtcNow - item.PublishedUtc;
        if (diff < TimeSpan.FromMinutes(1))
            return t("News.JustNow");
        if (diff < TimeSpan.FromHours(1))
            return t("News.MinAgo", Math.Max(1, (int)diff.TotalMinutes));
        if (diff < TimeSpan.FromDays(1))
            return t("News.HourAgo", Math.Max(1, (int)diff.TotalHours));
        if (diff < TimeSpan.FromDays(7))
            return t("News.DaysAgo", Math.Max(1, (int)diff.TotalDays));
        return item.PublishedUtc.ToLocalTime().ToString("d");
    }

    [RelayCommand]
    private void OpenPlayer(NewsVideoItem? item)
    {
        if (item is null)
            return;

        PlayerVideo = item;
        IsInPlayer = true;
    }

    [RelayCommand]
    private void ClosePlayer()
    {
        IsInPlayer = false;
        PlayerVideo = null;
    }
}
