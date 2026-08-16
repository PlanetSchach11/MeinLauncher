using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MeinLauncher.Models;

namespace MeinLauncher.Services;

/// <summary>
/// Zugriff auf die offizielle Modrinth-API (v2). Reine JSON-API – es wird nie ein
/// Browser geöffnet und nie eine Modrinth-Webseite angezeigt. Alle Suchergebnisse,
/// Projektinfos und Dateien kommen direkt von api.modrinth.com.
/// </summary>
public sealed class ModrinthApiClient
{
    private const string BaseUrl = "https://api.modrinth.com/v2";

    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public ModrinthApiClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        // Modrinth verlangt einen aussagekräftigen User-Agent.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PickleLauncher/0.1.0 (MeinLauncher; personal use)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    /// <summary>
    /// Sucht Projekte über die offizielle Suche, gefiltert auf die angegebene
    /// Minecraft-Version und den Loader (Facets).
    /// </summary>
    public async Task<List<ModrinthHit>> SearchAsync(
        string query,
        string gameVersion,
        string loader,
        int limit = 30,
        string index = "relevance")
    {
        var facets = new List<string[]>
        {
            new[] { $"versions:{gameVersion}" },
            new[] { $"categories:{loader}" },
        };

        var url = $"{BaseUrl}/search?query={Uri.EscapeDataString(query)}"
            + $"&limit={limit}&index={index}"
            + $"&facets={Uri.EscapeDataString(JsonSerializer.Serialize(facets))}";

        var json = await GetStringAsync(url);
        return JsonSerializer.Deserialize<ModrinthSearchResponse>(json, JsonOpts)?.Hits ?? [];
    }

    /// <summary>
    /// Liefert alle Versionen eines Projekts, die zur Minecraft-Version und zum
    /// Loader passen (absteigend nach Veröffentlichung sortiert – [0] ist neueste).
    /// </summary>
    public async Task<List<ModrinthVersionDto>> GetVersionsAsync(string projectId, string gameVersion, string loader)
    {
        var url = $"{BaseUrl}/project/{Uri.EscapeDataString(projectId)}/version"
            + $"?game_versions={Uri.EscapeDataString(JsonSerializer.Serialize(new[] { gameVersion }))}"
            + $"&loaders={Uri.EscapeDataString(JsonSerializer.Serialize(new[] { loader }))}";

        var json = await GetStringAsync(url);
        return JsonSerializer.Deserialize<List<ModrinthVersionDto>>(json, JsonOpts) ?? [];
    }

    public async Task<ModrinthProjectDto?> GetProjectAsync(string projectId)
    {
        var url = $"{BaseUrl}/project/{Uri.EscapeDataString(projectId)}";
        var response = await _http.GetAsync(url);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ModrinthProjectDto>(json, JsonOpts);
    }

    /// <summary>Holt mehrere Projekte in einem Request (Batch).</summary>
    public async Task<List<ModrinthProjectDto>> GetProjectsAsync(IEnumerable<string> projectIds)
    {
        var ids = projectIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().Take(100).ToList();
        if (ids.Count == 0)
            return [];

        var url = $"{BaseUrl}/projects?ids={Uri.EscapeDataString(JsonSerializer.Serialize(ids))}";
        var json = await GetStringAsync(url);
        return JsonSerializer.Deserialize<List<ModrinthProjectDto>>(json, JsonOpts) ?? [];
    }

    /// <summary>
    /// Ordnet SHA-1-Hashes lokaler .jar-Dateien den Modrinth-Versionen zu.
    /// Unbekannte Hashes fehlen schlicht im Ergebnis. Exakte Erkennung bereits
    /// installierter Mods ohne Raten.
    /// </summary>
    public async Task<Dictionary<string, ModrinthVersionDto?>> GetVersionsByHashesAsync(IReadOnlyList<string> sha1Hashes)
    {
        var result = new Dictionary<string, ModrinthVersionDto?>();
        if (sha1Hashes.Count == 0)
            return result;

        var body = JsonSerializer.Serialize(new { hashes = sha1Hashes, algorithm = "sha1" });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{BaseUrl}/version_files", content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind == JsonValueKind.Null
                ? null
                : JsonSerializer.Deserialize<ModrinthVersionDto>(property.Value.GetRawText(), JsonOpts);
        }

        return result;
    }

    /// <summary>Lädt eine Datei (z. B. eine Mod-.jar) in den Zielordner.</summary>
    public async Task DownloadFileAsync(string url, string destinationPath, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var tmp = destinationPath + ".part";
        try
        {
            await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await response.Content.CopyToAsync(fs, ct);
            }

            File.Move(tmp, destinationPath, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(tmp))
                    File.Delete(tmp);
            }
            catch
            {
                // Aufräumen darf nichts verschlimmern.
            }

            throw;
        }
    }

    /// <summary>Lädt die Icon-Bytes eines Projekts (CDN).</summary>
    public async Task<byte[]> GetIconBytesAsync(string iconUrl)
    {
        using var response = await _http.GetAsync(iconUrl);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    private async Task<string> GetStringAsync(string url)
    {
        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
