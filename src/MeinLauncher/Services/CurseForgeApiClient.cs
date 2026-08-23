using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MeinLauncher.Models;

namespace MeinLauncher.Services;

/// <summary>
/// Zugriff auf die CurseForge-API (v1). Reine JSON-API mit x-api-key Authentifizierung.
/// </summary>
public sealed class CurseForgeApiClient
{
    private const string BaseUrl = "https://api.curseforge.com";
    private const int MinecraftGameId = 432;

    private readonly HttpClient _http;
    private readonly string _apiKey;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public CurseForgeApiClient(string apiKey)
    {
        _apiKey = apiKey?.Trim() ?? "";
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppVersion.UserAgent} (Kulka Client; personal use)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        if (!string.IsNullOrWhiteSpace(_apiKey))
            _http.DefaultRequestHeaders.Add("x-api-key", _apiKey);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    /// <summary>
    /// Sucht Mods über die CurseForge-API, gefiltert auf MC-Version und Loader.
    /// </summary>
    public async Task<List<CurseForgeModDto>> SearchAsync(
        string query,
        string gameVersion,
        string loader,
        int limit = 30)
    {
        var modLoaderType = CurseForgeLoaderType.FromString(loader);

        var url = $"{BaseUrl}/v1/mods/search?gameId={MinecraftGameId}"
            + $"&pageSize={limit}&sortField=2&sortOrder=desc"; // sortField=2 = Popularity

        if (!string.IsNullOrWhiteSpace(query))
            url += $"&searchFilter={Uri.EscapeDataString(query)}";

        if (!string.IsNullOrWhiteSpace(gameVersion))
            url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";

        if (modLoaderType > 0)
            url += $"&modLoaderType={modLoaderType}";

        var json = await GetStringAsync(url);
        return JsonSerializer.Deserialize<CurseForgeSearchResponse>(json, JsonOpts)?.Data ?? [];
    }

    /// <summary>
    /// Holt die Dateien eines Mods, gefiltert auf MC-Version und Loader.
    /// </summary>
    public async Task<List<CurseForgeFileDto>> GetModFilesAsync(
        int modId,
        string gameVersion,
        string loader)
    {
        var modLoaderType = CurseForgeLoaderType.FromString(loader);

        var url = $"{BaseUrl}/v1/mods/{modId}/files?gameId={MinecraftGameId}"
            + $"&pageSize=50";

        if (!string.IsNullOrWhiteSpace(gameVersion))
            url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";

        if (modLoaderType > 0)
            url += $"&modLoaderType={modLoaderType}";

        var json = await GetStringAsync(url);
        return JsonSerializer.Deserialize<CurseForgeModFilesResponse>(json, JsonOpts)?.Data ?? [];
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

    /// <summary>Lädt die Icon-Bytes eines Mods (CDN).</summary>
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
