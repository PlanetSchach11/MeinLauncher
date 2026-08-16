using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MeinLauncher.Models;

public sealed class VersionManifest
{
    [JsonPropertyName("versions")]
    public List<VersionManifestEntry> Versions { get; set; } = [];

    [JsonPropertyName("latest")]
    public LatestVersions? Latest { get; set; }
}

public sealed class VersionManifestEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("time")]
    public string Time { get; set; } = "";

    [JsonPropertyName("releaseTime")]
    public string ReleaseTime { get; set; } = "";
}

public sealed class LatestVersions
{
    [JsonPropertyName("release")]
    public string Release { get; set; } = "";

    [JsonPropertyName("snapshot")]
    public string Snapshot { get; set; } = "";
}
