using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MeinLauncher.Models;

namespace MeinLauncher.Services;

/// <summary>
/// Holt die Minecraft-Versionsliste von der offiziellen Mojang-API und
/// lädt einzelne Versionen (Version-JSON + Client-JAR) herunter.
/// </summary>
public sealed class MojangVersionService
{
    private const string ManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<IReadOnlyList<MinecraftVersionInfo>> GetVersionsAsync()
    {
        try
        {
            using var response = await _http.GetAsync(ManifestUrl);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var manifest = JsonSerializer.Deserialize<VersionManifest>(json);
            if (manifest?.Versions is { Count: > 0 })
            {
                return manifest.Versions
                    .Select(v => new MinecraftVersionInfo(v.Id, v.Type, v.ReleaseTime, v.Url))
                    .ToList();
            }
        }
        catch
        {
            // Offline: Fallback-Daten verwenden.
        }

        return GetFallbackVersions();
    }

    /// <summary>
    /// Lädt Version-JSON und Client-JAR in den Versions-Ordner herunter.
    /// </summary>
    public async Task DownloadVersionAsync(
        MinecraftVersionInfo version,
        string versionsDirectory,
        IProgress<double>? progress,
        IProgress<string>? status)
    {
        var versionDir = Path.Combine(versionsDirectory, version.Id);
        Directory.CreateDirectory(versionDir);

        var jsonPath = Path.Combine(versionDir, version.Id + ".json");
        status?.Report($"Lade Versionsdaten für {version.Id} …");
        var jsonContent = await _http.GetStringAsync(version.Url);
        await File.WriteAllTextAsync(jsonPath, jsonContent);

        var versionJson = JsonSerializer.Deserialize<VersionJson>(jsonContent);
        var clientUrl = versionJson?.Downloads?.Client?.Url;
        var clientSize = versionJson?.Downloads?.Client?.Size ?? 0;

        if (string.IsNullOrWhiteSpace(clientUrl))
        {
            throw new InvalidOperationException("Die Versionsdaten enthalten keine Client-URL.");
        }

        var jarPath = Path.Combine(versionDir, version.Id + ".jar");
        status?.Report($"Lade Client-JAR für {version.Id} herunter …");

        using var response = await _http.GetAsync(clientUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var total = clientSize > 0 ? clientSize : response.Content.Headers.ContentLength ?? 0;

        await using var source = await response.Content.ReadAsStreamAsync();
        await using var target = File.Create(jarPath);
        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read));
            readTotal += read;
            if (total > 0)
                progress?.Report((double)readTotal / total);
        }

        progress?.Report(1.0);
    }

    /// <summary>
    /// Prüft, ob eine Version bereits heruntergeladen wurde.
    /// </summary>
    public bool IsInstalled(MinecraftVersionInfo version, string versionsDirectory)
    {
        var versionDir = Path.Combine(versionsDirectory, version.Id);
        return File.Exists(Path.Combine(versionDir, version.Id + ".json")) &&
               File.Exists(Path.Combine(versionDir, version.Id + ".jar"));
    }

    private static IReadOnlyList<MinecraftVersionInfo> GetFallbackVersions()
    {
        // Offline-Fallback: bekannte Versionen, damit die UI auch ohne Netz nutzbar bleibt.
        string[] releaseIds =
        [
            "1.21.8", "1.21.7", "1.21.6", "1.21.5", "1.21.4", "1.21.3",
            "1.21.1", "1.20.6", "1.20.4", "1.20.1", "1.19.4", "1.18.2",
        ];

        string[] snapshots = ["25w31a", "25w30a", "25w29a"];

        var list = new List<MinecraftVersionInfo>();
        foreach (var id in releaseIds)
        {
            list.Add(new MinecraftVersionInfo(
                id,
                "release",
                "2025-01-01T00:00:00Z",
                $"https://piston-meta.mojang.com/v1/packages/fallback/{id}.json"));
        }

        foreach (var id in snapshots)
        {
            list.Add(new MinecraftVersionInfo(
                id,
                "snapshot",
                "2025-07-01T00:00:00Z",
                $"https://piston-meta.mojang.com/v1/packages/fallback/{id}.json"));
        }

        return list;
    }
}
