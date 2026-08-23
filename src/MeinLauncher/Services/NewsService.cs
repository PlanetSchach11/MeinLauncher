using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MeinLauncher.ViewModels;

namespace MeinLauncher.Services;

/// <summary>
/// Liefert die neuesten Uploads des YouTube-Kanals @ANG3L0WW über den offiziellen
/// Atom-/RSS-Feed (<c>https://www.youtube.com/feeds/videos.xml?channel_id=…</c>).
/// Es wird bewusst NICHT die YouTube-Webseite gescrapt, kein Browser geöffnet und
/// es fallen keine YouTube-Anmelde- oder Kontodaten an – nur öffentliche Videos.
/// Alle Fehler (keine Internetverbindung, YouTube nicht erreichbar, Timeout,
/// ungültige Daten) werden abgefangen und als „Fehlschlag“ zurückgegeben.
/// </summary>
public sealed class NewsService
{
    /// <summary>Stabile Kanal-ID des Kanals @ANG3L0WW (einmalig aufgelöst).</summary>
    public const string ChannelId = "UCzPlt5uZB5bDnAJYaeOYk_w";

    /// <summary>Anzeige-Handle des Kanals (Fallback, falls der Feed keinen Namen liefert).</summary>
    public const string ChannelHandle = "@ANG3L0WW";

    private const string FeedUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=" + ChannelId;

    private const int MaxVideos = 15;

    private readonly HttpClient _http;

    private static readonly HttpClient ThumbnailHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    public NewsService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppVersion.UserAgent} (News; personal use)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/atom+xml, application/xml, text/xml");
    }

    /// <summary>
    /// Holt die neuesten Videos (neueste zuerst). Gibt bei Erfolg das Ergebnis mit
    /// Kanalname und Videos zurück, bei Netzwerk-/Timeout-/Datenfehlern <c>null</c>.
    /// </summary>
    public async Task<NewsFetchResult?> FetchAsync()
    {
        try
        {
            using var response = await _http.GetAsync(FeedUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var xml = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return Parse(xml);
        }
        catch
        {
            // Offline, YouTube nicht erreichbar, Timeout oder HTTP-Fehler – nie werfen.
            return null;
        }
    }

    /// <summary>Lädt das Thumbnail eines Videos (Fehler erzeugen nur einen Platzhalter).</summary>
    public async Task LoadThumbnailAsync(NewsVideoItem item)
    {
        try
        {
            var bytes = await ThumbnailHttp.GetByteArrayAsync(item.ThumbnailUrl).ConfigureAwait(false);
            using var stream = new MemoryStream(bytes);
            var bmp = new Bitmap(stream);
            Dispatcher.UIThread.Post(() => item.Thumbnail = bmp);
        }
        catch
        {
            // Platzhalter (Bildbereich) bleibt sichtbar.
        }
    }

    private static NewsFetchResult? Parse(string xml)
    {
        try
        {
            XNamespace ns = "http://www.w3.org/2005/Atom";
            XNamespace yt = "http://www.youtube.com/xml/schemas/2015";
            XNamespace media = "http://search.yahoo.com/mrss/";

            var doc = XDocument.Parse(xml);
            var channelName = doc.Root?.Element(ns + "title")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(channelName))
                channelName = ChannelHandle;

            var videos = doc.Root?
                .Elements(ns + "entry")
                .Take(MaxVideos)
                .Select(entry => ParseEntry(entry, ns, yt, media))
                .Where(v => v is not null)
                .Select(v => v!)
                .ToList() ?? [];

            return videos.Count == 0 ? null : new NewsFetchResult(channelName, videos);
        }
        catch
        {
            // Ungültiges/fehlendes XML – als Fehlschlag behandeln.
            return null;
        }
    }

    private static NewsVideoItem? ParseEntry(
        XElement entry, XNamespace ns, XNamespace yt, XNamespace media)
    {
        var videoId = entry.Element(yt + "videoId")?.Value?.Trim();
        var title = entry.Element(ns + "title")?.Value?.Trim();
        var published = entry.Element(ns + "published")?.Value?.Trim();
        var group = entry.Element(media + "group");
        var description = group?.Element(media + "description")?.Value?.Trim();
        var thumbnail = group?.Element(media + "thumbnail")?.Attribute("url")?.Value?.Trim();

        if (string.IsNullOrWhiteSpace(videoId) || string.IsNullOrWhiteSpace(title))
            return null;

        return new NewsVideoItem
        {
            VideoId = videoId,
            Title = title,
            Description = description ?? "",
            ThumbnailUrl = string.IsNullOrWhiteSpace(thumbnail)
                ? $"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg"
                : thumbnail,
            PublishedUtc = DateTimeOffset.TryParse(published, out var parsed) ? parsed : DateTimeOffset.MinValue,
        };
    }
}

/// <summary>Ergebnis einer erfolgreichen Feed-Abfrage.</summary>
public sealed record NewsFetchResult(string ChannelName, List<NewsVideoItem> Videos);
