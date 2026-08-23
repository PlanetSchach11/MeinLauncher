using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MeinLauncher.Services;

/// <summary>Ergebnis einer Update-Prüfung.</summary>
internal sealed record UpdateInfo(
    bool IsAvailable,
    string RemoteVersion,
    string? DownloadUrl,
    string? AssetName);

/// <summary>
/// Prüft über die GitHub-API, ob eine neue Version verfügbar ist, und kann
/// die passende ZIP herunterladen und entpacken. Enthält KEINE Benutzerdaten.
/// </summary>
internal sealed class UpdateService : IDisposable
{
    private const string RepoApiUrl =
        "https://api.github.com/repos/PlanetSchach11/MeinLauncher/releases/latest";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Erkennt, ob die App als Microsoft-Store-Paket (MSIX) läuft.
    /// Im MSIX-Container liegt eine AppxManifest.xml im Installationsverzeichnis.
    /// Store-Updates ersetzen in diesem Fall den Auto-Updater.
    /// </summary>
    internal static bool IsStorePackage { get; } =
        File.Exists(Path.Combine(AppContext.BaseDirectory, "AppxManifest.xml"));

    private readonly HttpClient _http;

    public UpdateService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppVersion.UserAgent} (Update-Check)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    // --------------------------------------------------------- Update-Check

    /// <summary>
    /// Prüft auf GitHub, ob eine neue Version vorliegt. Liefert <c>null</c>
    /// bei Netzwerkfehlern oder wenn kein Update vorhanden ist.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync(RepoApiUrl, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Tag: "v0.3.0" → "0.3.0"
            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var remoteVersion = tagName.StartsWith('v') ? tagName[1..] : tagName;

            if (!IsNewer(remoteVersion, AppVersion.Current))
                return null;

            // Passendes Asset suchen: KulkaClient-v{VERSION}-win-x64.zip
            string? downloadUrl = null;
            string? assetName = null;

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.Contains("win-x64", StringComparison.OrdinalIgnoreCase) &&
                        name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        assetName = name;
                        break;
                    }
                }
            }

            if (downloadUrl is null)
                return null;

            return new UpdateInfo(true, remoteVersion, downloadUrl, assetName);
        }
        catch
        {
            // Offline / Timeout / API-Fehler → kein Update, kein Fehler.
            return null;
        }
    }

    // --------------------------------------------------------- Download

    /// <summary>
    /// Lädt das Update-ZIP herunter und entpackt es in ein temporäres Verzeichnis.
    /// Gibt den Pfad zum Entpackungs-Ordner zurück oder wirft bei Fehler.
    /// </summary>
    public async Task<string> DownloadAndExtractAsync(
        UpdateInfo update, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (update.DownloadUrl is null)
            throw new InvalidOperationException("Keine Download-URL vorhanden.");

        var tempDir = Path.Combine(Path.GetTempPath(), "KulkaUpdate_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        var zipPath = Path.Combine(tempDir, "update.zip");

        try
        {
            // --- Download ---
            using (var resp = await _http.GetAsync(update.DownloadUrl,
                       HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();

                var totalBytes = resp.Content.Headers.ContentLength ?? 0;
                await using var contentStream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write,
                    FileShare.None, 81920, true);

                var buffer = new byte[81920];
                long downloaded = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    downloaded += read;
                    if (totalBytes > 0)
                        progress?.Report((double)downloaded / totalBytes);
                }
            }

            // --- Zip-Slip-Schutz + Entpacken ---
            var extractDir = Path.Combine(tempDir, "extracted");
            Directory.CreateDirectory(extractDir);
            ExtractWithZipSlipProtection(zipPath, extractDir);

            // ZIP nach erfolgreichem Entpacken löschen (spart Platz).
            try { File.Delete(zipPath); } catch { /* egal */ }

            return extractDir;
        }
        catch
        {
            // Bei jedem Fehler: temporäres Verzeichnis vollständig aufräumen.
            SafeDeleteDirectory(tempDir);
            throw;
        }
    }

    // --------------------------------------------------------- Installation

    /// <summary>
    /// Startet den sicheren Update-Prozess:
    /// 1. Erzeugt ein Update-Batch-Skript
    /// 2. Startet das Skript als neuen Prozess
    /// 3. Beendet den aktuellen Launcher
    /// Das Skript ersetzt dann die Dateien und startet den neuen Launcher.
    /// Bei Store-Versionen (MSIX) ist diese Methode ein No-Op – Store-Updates ersetzen den Auto-Updater.
    /// </summary>
    public static void StartUpdateAndExit(string extractedDir)
    {
        if (IsStorePackage)
            return; // Store-Updates übernehmen das.

        var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var batchPath = Path.Combine(Path.GetTempPath(), "KulkaUpdate_" + Guid.NewGuid().ToString("N")[..8] + ".bat");

        // Batch-Skript: Warte bis Launcher beendet → Kopiere Dateien → Starte neuen Launcher.
        var batch = $"""
            @echo off
            timeout /t 2 /nobreak >nul
            xcopy /s /y /e "{extractedDir}\*" "{installDir}\" >nul 2>&1
            rmdir /s /q "{extractedDir}" >nul 2>&1
            del "%~f0" >nul 2>&1
            start "" "{installDir}\KulkaClient.exe"
            """;

        File.WriteAllText(batchPath, batch);

        // Batch starten (verborgenes Fenster).
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{batchPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
        };
        System.Diagnostics.Process.Start(psi);

        // Aktuellen Launcher beenden.
        Environment.Exit(0);
    }

    // --------------------------------------------------------- Helpers

    /// <summary>Vergleicht zwei Semantic-Version-Strings. "remote" muss neuer sein.</summary>
    internal static bool IsNewer(string remote, string current)
    {
        try
        {
            var r = remote.Split('.', '-', '+');
            var c = current.Split('.', '-', '+');

            for (var i = 0; i < Math.Max(r.Length, c.Length); i++)
            {
                var rv = i < r.Length && int.TryParse(r[i], out var rv2) ? rv2 : 0;
                var cv = i < c.Length && int.TryParse(c[i], out var cv2) ? cv2 : 0;
                if (rv > cv) return true;
                if (rv < cv) return false;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Entpackt eine ZIP-Datei mit Zip-Slip-Schutz: Pfade die über
    /// das Zielverzeichnis hinausgehen werden übersprungen.
    /// </summary>
    private static void ExtractWithZipSlipProtection(string zipPath, string extractDir)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue; // Verzeichnisse überspringen.

            var destPath = Path.GetFullPath(Path.Combine(extractDir, entry.FullName));

            // Zip-Slip-Schutz: Pfad muss innerhalb des Zielordners liegen.
            if (!destPath.StartsWith(extractDir, StringComparison.OrdinalIgnoreCase))
                continue;

            var destDir = Path.GetDirectoryName(destPath);
            if (destDir is not null && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            entry.ExtractToFile(destPath, overwrite: true);
        }
    }

    private static void SafeDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch { /* beste Mühe */ }
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
