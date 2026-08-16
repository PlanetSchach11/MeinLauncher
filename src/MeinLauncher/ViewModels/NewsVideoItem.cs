using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MeinLauncher.ViewModels;

/// <summary>
/// Ein einzelnes YouTube-Video aus dem News-Feed von @ANG3L0WW.
/// </summary>
public sealed partial class NewsVideoItem : ObservableObject
{
    /// <summary>YouTube-Video-Id (aus &lt;yt:videoId&gt;).</summary>
    public string VideoId { get; set; } = "";

    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public string ThumbnailUrl { get; set; } = "";

    /// <summary>Veröffentlichungszeitpunkt (UTC) aus dem Feed.</summary>
    public DateTimeOffset PublishedUtc { get; set; }

    /// <summary>Lokalisierte, relative Zeitangabe (z. B. „vor 2 Tagen“).</summary>
    [ObservableProperty]
    public partial string PublishedLabel { get; set; } = "";

    /// <summary>Geladenes Thumbnail (fehlgeschlagene Ladevorgänge bleiben ohne Bild).</summary>
    [ObservableProperty]
    public partial Bitmap? Thumbnail { get; set; }

    /// <summary>
    /// Eingebettete, werbefreie YouTube-URL für die Wiedergabe im Launcher
    /// (yt-nocookie: keine Cookies, nur öffentliche Videos).
    /// </summary>
    public string EmbedUrl => $"https://www.youtube-nocookie.com/embed/{VideoId}?autoplay=1&rel=0";
}
