using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MeinLauncher.Services;

/// <summary>
/// Installiert Fabric Loader automatisch aus dem Fabric Meta API.
/// Wird beim Spielstart aufgerufen, wenn ein Profil mit Fabric als ModLoader
/// gewählt ist, aber die passende fabric-loader-Version noch nicht existiert.
/// </summary>
public sealed class FabricInstallerService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private const string MavenFabricNet = "https://maven.fabricmc.net/";

    /// <summary>
    /// Stellt sicher, dass ein Fabric Loader für die gegebene Minecraft-Version installiert ist.
    /// Gibt das Verzeichnis der Fabric-Version zurück oder null bei Fehler.
    /// </summary>
    public static async Task<string?> EnsureFabricLoaderInstalledAsync(
        string gameVersion, string versionsDirectory, string librariesDirectory)
    {
        // 1. Prüfen ob Fabric-Version bereits existiert.
        var existing = FindExistingFabricDir(versionsDirectory, gameVersion);
        if (existing is not null)
            return existing;

        AccountDiagnostics.Log($"[FABRIC] Keine Fabric-Version für {gameVersion} gefunden – installiere...");

        try
        {
            // 2. Kompatible Loader-Versionen von Fabric Meta API abrufen.
            var loaderEntries = await FetchLoaderVersionsAsync(gameVersion);
            if (loaderEntries is null || loaderEntries.Count == 0)
            {
                AccountDiagnostics.Log($"[FABRIC] Keine Loader-Versionen für {gameVersion} gefunden.");
                return null;
            }

            // Stabilste Version nehmen (stable zuerst, dann höchste Build-Nummer).
            var best = loaderEntries
                .OrderByDescending(e => e.Stable)
                .ThenByDescending(e => e.Build)
                .First();

            AccountDiagnostics.Log($"[FABRIC] Gewählter Loader: {best.LoaderVersion} (Build {best.Build}, stable={best.Stable})");

            // 3. Version-JSON aus launcherMeta konstruieren (kein clientData.url mehr in der API).
            var versionJson = ConstructFabricVersionJson(best, gameVersion);

            var dirName = $"fabric-loader-{best.LoaderVersion}-{gameVersion}";
            var versionDir = Path.Combine(versionsDirectory, dirName);
            Directory.CreateDirectory(versionDir);

            var jsonPath = Path.Combine(versionDir, dirName + ".json");
            await File.WriteAllTextAsync(jsonPath, versionJson);
            AccountDiagnostics.Log($"[FABRIC] Version-JSON geschrieben: {jsonPath}");

            // 4. Alle Libraries herunterladen.
            var downloaded = await DownloadLibrariesAsync(versionJson, librariesDirectory);
            AccountDiagnostics.Log($"[FABRIC] {downloaded} Libraries heruntergeladen/verifiziert für {gameVersion}.");

            return versionDir;
        }
        catch (Exception ex)
        {
            AccountDiagnostics.Log($"[FABRIC] Fehler bei der Installation: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sucht ein vorhandenes fabric-loader-Verzeichnis für die gegebene MC-Version.
    /// </summary>
    private static string? FindExistingFabricDir(string versionsDirectory, string gameVersion)
    {
        if (!Directory.Exists(versionsDirectory))
            return null;

        var suffix = $"-{gameVersion}";
        foreach (var dir in Directory.EnumerateDirectories(versionsDirectory))
        {
            var name = Path.GetFileName(dir);
            if (name.StartsWith("fabric-loader-", StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(Path.Combine(dir, name + ".json")))
            {
                return dir;
            }
        }
        return null;
    }

    // ---------------------------------------------------------------- Meta API

    private const string MetaApiBase = "https://meta.fabricmc.net/v2/versions/loader";

    /// <summary>
    /// Holt die kompatiblen Fabric Loader-Versionen für eine MC-Version.
    /// Das aktuelle API-Format liefert: { loader: {...}, intermediary: {...}, launcherMeta: {...} }
    /// (kein clientData mehr – die Version-JSON wird aus launcherMeta konstruiert).
    /// </summary>
    private static async Task<List<LoaderEntry>?> FetchLoaderVersionsAsync(string gameVersion)
    {
        var url = $"{MetaApiBase}/{gameVersion}";
        using var response = await Http.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return null;

        var entries = new List<LoaderEntry>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("loader", out var loaderObj))
                continue;

            var loaderVersion = loaderObj.GetProperty("version").GetString() ?? "";
            var build = loaderObj.TryGetProperty("build", out var buildProp) ? buildProp.GetInt32() : 0;
            var stable = loaderObj.TryGetProperty("stable", out var stableProp) && stableProp.GetBoolean();
            var loaderMaven = loaderObj.TryGetProperty("maven", out var mavenProp) ? mavenProp.GetString() ?? "" : "";

            // launcherMeta enthält Libraries + mainClass direkt (kein URL-Download mehr).
            if (!item.TryGetProperty("launcherMeta", out var launcherMeta))
                continue;

            // Intermediary-Maven-Koordinate für die Libraries.
            var intermediaryMaven = "";
            if (item.TryGetProperty("intermediary", out var intermObj) &&
                intermObj.TryGetProperty("maven", out var intermMaven))
            {
                intermediaryMaven = intermMaven.GetString() ?? "";
            }

            entries.Add(new LoaderEntry(loaderVersion, build, stable, loaderMaven, intermediaryMaven, launcherMeta.Clone()));
        }

        return entries;
    }

    /// <summary>Liefert einen Eintrag für eine Fabric Loader-Version.</summary>
    private sealed record LoaderEntry(
        string LoaderVersion,
        int Build,
        bool Stable,
        string LoaderMaven,
        string IntermediaryMaven,
        JsonElement LauncherMeta);

    // ---------------------------------------------------------------- JSON construction

    /// <summary>
    /// Konstruiert die Fabric-Version-JSON aus den Meta-API-Daten.
    /// Die JSON verwendet inheritsFrom, damit der Launcher die Vanilla-Libraries und
    /// Assets automatisch erbt und die Fabric-Libraries darüber gelegt werden.
    /// </summary>
    private static string ConstructFabricVersionJson(LoaderEntry entry, string gameVersion)
    {
        var id = $"fabric-loader-{entry.LoaderVersion}-{gameVersion}";

        var libraries = new List<object>();

        // Fabric Loader JAR selbst.
        libraries.Add(new
        {
            name = entry.LoaderMaven,
            url = MavenFabricNet,
        });

        // Intermediary Mappings.
        if (!string.IsNullOrEmpty(entry.IntermediaryMaven))
        {
            libraries.Add(new
            {
                name = entry.IntermediaryMaven,
                url = MavenFabricNet,
            });
        }

        // Libraries aus launcherMeta: common + client.
        var meta = entry.LauncherMeta;
        if (meta.TryGetProperty("libraries", out var libsObj))
        {
            // Common libraries (ASM, Mixin, etc.)
            if (libsObj.TryGetProperty("common", out var commonArray))
            {
                foreach (var lib in commonArray.EnumerateArray())
                    libraries.Add(ConvertLibraryEntry(lib));
            }

            // Client-specific libraries.
            if (libsObj.TryGetProperty("client", out var clientArray))
            {
                foreach (var lib in clientArray.EnumerateArray())
                    libraries.Add(ConvertLibraryEntry(lib));
            }
        }

        // MainClass aus launcherMeta.
        var mainClass = "net.fabricmc.loader.impl.launch.knot.KnotClient";
        if (meta.TryGetProperty("mainClass", out var mainClassObj) &&
            mainClassObj.TryGetProperty("client", out var clientMainClass))
        {
            mainClass = clientMainClass.GetString() ?? mainClass;
        }

        var versionObj = new
        {
            id,
            inheritsFrom = gameVersion,
            mainClass,
            libraries,
        };

        return JsonSerializer.Serialize(versionObj, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Konvertiert einen Library-Eintrag aus launcherMeta in ein passendes Objekt
    /// für die Version-JSON (name + url Pflicht, Rest optional).
    /// </summary>
    private static object ConvertLibraryEntry(JsonElement lib)
    {
        var name = lib.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
        var url = lib.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? MavenFabricNet : MavenFabricNet;

        // SHA1 für Download-Verifizierung.
        string? sha1 = lib.TryGetProperty("sha1", out var sha1Prop) ? sha1Prop.GetString() : null;
        long size = lib.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;

        return new
        {
            name,
            url,
            sha1,
            size,
        };
    }

    // ---------------------------------------------------------------- Libraries

    /// <summary>
    /// Parst die Libraries aus der Fabric-Version-JSON und lädt fehlende JARs herunter.
    /// </summary>
    private static async Task<int> DownloadLibrariesAsync(string versionJson, string librariesDirectory)
    {
        using var doc = JsonDocument.Parse(versionJson);
        if (!doc.RootElement.TryGetProperty("libraries", out var libsArray))
            return 0;

        Directory.CreateDirectory(librariesDirectory);
        var downloaded = 0;

        foreach (var lib in libsArray.EnumerateArray())
        {
            var name = lib.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            if (string.IsNullOrEmpty(name))
                continue;

            var mavenPath = MavenNameToPath(name);
            if (string.IsNullOrEmpty(mavenPath))
                continue;

            var fullPath = Path.Combine(librariesDirectory, mavenPath);
            if (File.Exists(fullPath))
                continue; // Bereits vorhanden.

            // Repository-URL aus dem Library-Eintrag oder Fallback Maven Central.
            var baseUrl = "";
            if (lib.TryGetProperty("url", out var urlProp))
                baseUrl = urlProp.GetString() ?? "";

            if (string.IsNullOrEmpty(baseUrl))
                baseUrl = "https://repo1.maven.org/maven2/";

            if (!baseUrl.EndsWith("/"))
                baseUrl += "/";

            var downloadUrl = baseUrl + mavenPath;

            try
            {
                var dir = Path.GetDirectoryName(fullPath);
                if (dir is not null)
                    Directory.CreateDirectory(dir);

                var data = await Http.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(fullPath, data);
                downloaded++;
            }
            catch (Exception ex)
            {
                AccountDiagnostics.Log($"[FABRIC] Library-Download fehlgeschlagen: {name} → {downloadUrl}: {ex.Message}");
            }
        }

        return downloaded;
    }

    /// <summary>Konvertiert Maven-Koordinaten in einen Dateipfad.</summary>
    private static string MavenNameToPath(string name)
    {
        var parts = name.Split(':');
        if (parts.Length < 3)
            return "";

        var groupId = parts[0].Replace('.', '/');
        var artifactId = parts[1];
        var version = parts[2];
        var classifier = parts.Length > 3 ? $"-{parts[3]}" : "";

        return $"{groupId}/{artifactId}/{version}/{artifactId}-{version}{classifier}.jar";
    }
}
