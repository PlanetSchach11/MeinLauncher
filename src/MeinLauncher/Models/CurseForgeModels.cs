using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MeinLauncher.Models;

/// <summary>DTOs der CurseForge-API v1 (camelCase/ PascalCase gemischt).</summary>

public sealed class CurseForgeSearchResponse
{
    [JsonPropertyName("data")]
    public List<CurseForgeModDto> Data { get; set; } = [];

    [JsonPropertyName("pagination")]
    public CurseForgePagination? Pagination { get; set; }
}

public sealed class CurseForgePagination
{
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("resultCount")]
    public int ResultCount { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
}

public sealed class CurseForgeModDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("downloadCount")]
    public long DownloadCount { get; set; }

    [JsonPropertyName("logo")]
    public CurseForgeLogo? Logo { get; set; }

    [JsonPropertyName("authors")]
    public List<CurseForgeAuthor> Authors { get; set; } = [];

    [JsonPropertyName("categories")]
    public List<CurseForgeCategory> Categories { get; set; } = [];

    [JsonPropertyName("latestFiles")]
    public List<CurseForgeFileDto> LatestFiles { get; set; } = [];
}

public sealed class CurseForgeLogo
{
    [JsonPropertyName("thumbnailUrl")]
    public string ThumbnailUrl { get; set; } = "";
}

public sealed class CurseForgeAuthor
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public sealed class CurseForgeCategory
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";
}

public sealed class CurseForgeModFilesResponse
{
    [JsonPropertyName("data")]
    public List<CurseForgeFileDto> Data { get; set; } = [];

    [JsonPropertyName("pagination")]
    public CurseForgePagination? Pagination { get; set; }
}

public sealed class CurseForgeFileDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";

    [JsonPropertyName("gameVersions")]
    public List<string> GameVersions { get; set; } = [];

    [JsonPropertyName("sortableGameVersions")]
    public List<string> SortableGameVersions { get; set; } = [];

    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; }

    [JsonPropertyName("modules")]
    public List<CurseForgeModule> Modules { get; set; } = [];

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("fileLength")]
    public long FileLength { get; set; }

    [JsonPropertyName("releaseType")]
    public int ReleaseType { get; set; }

    [JsonPropertyName("fileFingerprint")]
    public long FileFingerprint { get; set; }

    [JsonPropertyName("dependencies")]
    public List<CurseForgeDependency> Dependencies { get; set; } = [];

    [JsonPropertyName("exposeAsAlternative")]
    public bool ExposeAsAlternative { get; set; }

    [JsonPropertyName("parentProjectFileId")]
    public int? ParentProjectFileId { get; set; }

    [JsonPropertyName("isAlternate")]
    public bool IsAlternate { get; set; }
}

public sealed class CurseForgeModule
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("fingerprint")]
    public long Fingerprint { get; set; }
}

public sealed class CurseForgeDependency
{
    [JsonPropertyName("modId")]
    public int ModId { get; set; }

    [JsonPropertyName("fileId")]
    public int FileId { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    /// <summary>Dependency types: 1=Embedded, 2=Optional, 3=Required, 4=Tool, 5=Incompatible, 6=Include</summary>
}

public static class CurseForgeLoaderType
{
    public const int None = 0;
    public const int Forge = 1;
    public const int LiteLoader = 3;
    public const int Fabric = 4;
    public const int Quilt = 5;
    public const int NeoForge = 6;

    public static int FromString(string loader) => loader.ToLowerInvariant() switch
    {
        "fabric" => Fabric,
        "forge" => Forge,
        "neoforge" => NeoForge,
        "quilt" => Quilt,
        "liteloader" => LiteLoader,
        _ => None,
    };

    public static string ToString(int type) => type switch
    {
        Fabric => "fabric",
        Forge => "forge",
        NeoForge => "neoforge",
        Quilt => "quilt",
        LiteLoader => "liteloader",
        _ => "",
    };
}
