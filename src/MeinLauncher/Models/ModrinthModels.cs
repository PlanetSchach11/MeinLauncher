using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MeinLauncher.Models;

/// <summary>DTOs der offiziellen Modrinth-API v2 (JSON camelCase).</summary>

public sealed class ModrinthSearchResponse
{
    [JsonPropertyName("hits")]
    public List<ModrinthHit> Hits { get; set; } = [];

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("total_hits")]
    public long TotalHits { get; set; }
}

/// <summary>Ein Suchtreffer der /search-Endpunkte.</summary>
public sealed class ModrinthHit
{
    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; } = "";

    [JsonPropertyName("project_type")]
    public string ProjectType { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = [];

    [JsonPropertyName("versions")]
    public List<string> Versions { get; set; } = [];

    [JsonPropertyName("downloads")]
    public long Downloads { get; set; }

    [JsonPropertyName("follows")]
    public long Follows { get; set; }

    [JsonPropertyName("icon_url")]
    public string IconUrl { get; set; } = "";

    [JsonPropertyName("latest_version")]
    public string LatestVersion { get; set; } = "";

    [JsonPropertyName("date_created")]
    public DateTime DateCreated { get; set; }

    [JsonPropertyName("date_modified")]
    public DateTime DateModified { get; set; }
}

public sealed class ModrinthProjectDto
{
    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("icon_url")]
    public string IconUrl { get; set; } = "";

    [JsonPropertyName("downloads")]
    public long Downloads { get; set; }

    [JsonPropertyName("follows")]
    public long Follows { get; set; }

    [JsonPropertyName("project_type")]
    public string ProjectType { get; set; } = "";
}

public sealed class ModrinthFile
{
    [JsonPropertyName("hashes")]
    public Dictionary<string, string>? Hashes { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = "";

    [JsonPropertyName("primary")]
    public bool Primary { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

public sealed class ModrinthDependency
{
    [JsonPropertyName("project_id")]
    public string? ProjectId { get; set; }

    [JsonPropertyName("version_id")]
    public string? VersionId { get; set; }

    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }

    /// <summary>"required", "optional", "incompatible" oder "embedded".</summary>
    [JsonPropertyName("dependency_type")]
    public string DependencyType { get; set; } = "";
}

public sealed class ModrinthVersionDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version_number")]
    public string VersionNumber { get; set; } = "";

    [JsonPropertyName("game_versions")]
    public List<string> GameVersions { get; set; } = [];

    [JsonPropertyName("loaders")]
    public List<string> Loaders { get; set; } = [];

    [JsonPropertyName("files")]
    public List<ModrinthFile> Files { get; set; } = [];

    [JsonPropertyName("dependencies")]
    public List<ModrinthDependency> Dependencies { get; set; } = [];

    [JsonPropertyName("date_published")]
    public DateTime DatePublished { get; set; }

    [JsonPropertyName("downloads")]
    public long Downloads { get; set; }

    [JsonPropertyName("version_type")]
    public string VersionType { get; set; } = "";
}
