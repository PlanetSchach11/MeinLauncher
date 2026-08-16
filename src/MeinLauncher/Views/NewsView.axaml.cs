using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform;
using MeinLauncher.ViewModels;

namespace MeinLauncher.Views;

/// <summary>
/// News-Seite. Die Wiedergabe läuft eingebettet im Launcher über einen
/// <see cref="NativeWebView"/> (WebView2) – es öffnet sich kein Browserfenster und
/// es werden nur öffentliche Videos über die yt-nocookie-Einbettungs-URL gezeigt.
/// Fehlt WebView2, erscheint eine freundliche Meldung statt eines Absturzes.
/// </summary>
public partial class NewsView : UserControl
{
    private NewsViewModel? _vm;
    private NativeWebView? _webView;

    /// <summary>Lokale HTTP-Quelle für die Player-Seite (kein URL-ACL nötig, Port 0 = frei).</summary>
    private TcpListener? _playerServer;
    private string? _playerPageUrl;

    public NewsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as NewsViewModel;
        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NewsViewModel.IsInPlayer))
        {
            if (_vm!.IsInPlayer)
                OpenPlayer();
            else
                ClosePlayer();
        }
        else if (e.PropertyName == nameof(NewsViewModel.PlayerVideo) && _vm!.IsInPlayer)
        {
            OpenPlayer();
        }
    }

    /// <summary>Lädt das gewählte Video in den eingebetteten WebView2-Player.</summary>
    private void OpenPlayer()
    {
        if (_vm?.PlayerVideo is null)
        {
            ClosePlayer();
            return;
        }

        if (!IsWebView2Available())
        {
            PlayerErrorPanel.IsVisible = true;
            if (_webView is not null)
                _webView.IsVisible = false;
            return;
        }

        PlayerErrorPanel.IsVisible = false;
        EnsureWebView();
        _webView!.IsVisible = true;
        try
        {
            // YouTube-Embeds verlangen seit Nov 2025 einen gültigen Referrer/Origin,
            // sonst liefert der Player „Fehler 153“. Statt direkt auf die Embed-URL zu
            // navigieren (kein Referrer) oder per NavigateToString (opaker Origin) dient
            // eine lokale HTTP-Seite als Einbettungs-Kontext: Der Browser sendet für das
            // iframe automatisch den Referrer der lokalen Seite, und der `origin`-Parameter
            // der Embed-URL passt dazu. Die Seite läuft nur in-memory auf 127.0.0.1.
            StartPlayerServer(_vm.PlayerVideo.VideoId);
            _webView.Source = new Uri(_playerPageUrl!);
        }
        catch
        {
            StopPlayerServer();
            PlayerErrorPanel.IsVisible = true;
        }
    }

    /// <summary>
    /// Baut die Einbettungs-Seite: dunkel, randlos, iframe auf die öffentliche
    /// yt-nocookie-Embed-URL (keine Cookies, keine Anmeldedaten, nur öffentliche Videos).
    /// Als nicht-interpolierter Rohstring, damit die CSS-Klammern als Inhalt gelten.
    /// </summary>
    private static string BuildPlayerHtml(string videoId, string origin)
    {
        const string template = """
            <!DOCTYPE html>
            <html>
            <head>
              <meta name="referrer" content="strict-origin-when-cross-origin">
              <style>
                html, body { margin: 0; padding: 0; height: 100%; overflow: hidden; background: #000; }
                iframe { position: absolute; top: 0; left: 0; width: 100%; height: 100%; border: 0; }
              </style>
            </head>
            <body>
              <iframe src="https://www.youtube-nocookie.com/embed/VIDEO_ID?autoplay=1&rel=0&origin=ORIGIN"
                      allow="autoplay; encrypted-media; picture-in-picture"
                      allowfullscreen></iframe>
            </body>
            </html>
            """;
        return template.Replace("VIDEO_ID", videoId, StringComparison.Ordinal)
                       .Replace("ORIGIN", origin, StringComparison.Ordinal);
    }

    /// <summary>Startet die lokale HTTP-Quelle auf einem freien 127.0.0.1-Port.</summary>
    private void StartPlayerServer(string videoId)
    {
        StopPlayerServer();

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        _playerServer = listener;

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var origin = $"http://127.0.0.1:{port}";
        var html = BuildPlayerHtml(videoId, origin);
        _playerPageUrl = $"{origin}/player.html";

        _ = Task.Run(() => ServeLoop(listener, html));
    }

    /// <summary>
    /// Bedient Anfragen an die Player-Seite (einfacher HTTP/1.1-Antwort ohne Keep-Alive).
    /// Läuft bis der Listener gestoppt wird; Fehler pro Verbindung werden ignoriert.
    /// </summary>
    private static void ServeLoop(TcpListener listener, string html)
    {
        var head = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {html.Length}\r\n" +
            "Connection: close\r\n" +
            "Cache-Control: no-store\r\n\r\n");
        var body = Encoding.UTF8.GetBytes(html);

        while (true)
        {
            TcpClient client;
            try
            {
                client = listener.AcceptTcpClient();
            }
            catch
            {
                return; // Listener gestoppt
            }

            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    // Anforderungskopf bis CRLFCRLF konsumieren (Inhalt egal).
                    var buffer = new byte[4096];
                    var total = 0;
                    while (total < buffer.Length)
                    {
                        var read = stream.Read(buffer, total, buffer.Length - total);
                        if (read <= 0)
                            break;
                        total += read;
                        if (Encoding.ASCII.GetString(buffer, 0, total).Contains("\r\n\r\n", StringComparison.Ordinal))
                            break;
                    }

                    stream.Write(head, 0, head.Length);
                    stream.Write(body, 0, body.Length);
                    stream.Flush();
                }
            }
            catch
            {
                // Einzelne fehlerhafte Verbindungen ignorieren.
            }
        }
    }

    /// <summary>Stoppt die lokale HTTP-Quelle.</summary>
    private void StopPlayerServer()
    {
        if (_playerServer is not null)
        {
            try
            {
                _playerServer.Stop();
            }
            catch
            {
                // Bereits gestoppt – ignorieren.
            }

            _playerServer = null;
        }
    }

    /// <summary>Stoppt die Wiedergabe und blendet den Player aus.</summary>
    private void ClosePlayer()
    {
        StopPlayerServer();

        if (_webView is not null)
        {
            try
            {
                _webView.Source = new Uri("about:blank");
            }
            catch
            {
                // Ignorieren – der Player wird ohnehin ausgeblendet.
            }

            _webView.IsVisible = false;
        }

        PlayerErrorPanel.IsVisible = false;
    }

    private void EnsureWebView()
    {
        if (_webView is not null)
            return;

        _webView = new NativeWebView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        PlayerHost.Child = _webView;
    }

    /// <summary>
    /// Prüft, ob WebView2 verfügbar und die Einbettung in das Native-Control-Host-Szenario
    /// unterstützt ist. Bei jedem Fehler wird „nicht verfügbar“ gemeldet.
    /// </summary>
    private static bool IsWebView2Available()
    {
        try
        {
            var info = WebViewAdapterInfo.GetAdapterInfo(WebViewAdapterType.WebView2);
            return info.IsInstalled && info.IsSupported
                && info.SupportedScenarios.HasFlag(WebViewEmbeddingScenario.NativeControlHost);
        }
        catch
        {
            return false;
        }
    }
}
