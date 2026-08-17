using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MeinLauncher.Models;

/// <summary>
/// Vollständigere Deserialisierung der Versions-JSON einer Minecraft-Version –
/// alles, was ein echter Start braucht (Libraries, Argumente, Regeln, Logging).
/// </summary>
public sealed class VersionJson
{
    [JsonPropertyName("mainClass")]
    public string MainClass { get; set; } = "net.minecraft.client.main.Main";

    [JsonPropertyName("downloads")]
    public DownloadsSection? Downloads { get; set; }

    [JsonPropertyName("assetIndex")]
    public AssetIndexSection? AssetIndex { get; set; }

    [JsonPropertyName("libraries")]
    public List<LibraryEntry> Libraries { get; set; } = [];

    [JsonPropertyName("arguments")]
    public ArgumentsSection? Arguments { get; set; }

    /// <summary>Legacy-Argument-String (vor 1.13) mit ${…}-Token.</summary>
    [JsonPropertyName("minecraftArguments")]
    public string MinecraftArguments { get; set; } = "";

    [JsonPropertyName("logging")]
    public LoggingSection? Logging { get; set; }

    [JsonPropertyName("javaVersion")]
    public JavaVersionSection? JavaVersion { get; set; }

    /// <summary>Typ der Version, z. B. "release" (für --versionType).</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "release";

    /// <summary>
    /// Eltern-Version, von der geerbt wird (z.B. "26.2" für Fabric-Versionen).
    /// Wird vom Launcher aufgelöst: Libraries werden gemerget.
    /// </summary>
    [JsonPropertyName("inheritsFrom")]
    public string? InheritsFrom { get; set; }
}

public sealed class DownloadsSection
{
    [JsonPropertyName("client")]
    public DownloadArtifact? Client { get; set; }
}

public sealed class DownloadArtifact
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("sha1")]
    public string Sha1 { get; set; } = "";
}

public sealed class AssetIndexSection
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("sha1")]
    public string Sha1 { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

/// <summary>
/// Game- und JVM-Argumente. Jedes Argument ist entweder ein String oder ein
/// Objekt mit optionalen Regeln (z. B. nur für ein bestimmtes Betriebssystem).
/// </summary>
public sealed class ArgumentsSection
{
    [JsonPropertyName("game")]
    public List<JsonElement> Game { get; set; } = [];

    [JsonPropertyName("jvm")]
    public List<JsonElement> Jvm { get; set; } = [];
}

public sealed class LibraryEntry
{
    /// <summary>Maven-Koordinaten, z. B. "org.lwjgl:lwjgl:3.3.3".</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("downloads")]
    public LibraryDownloads? Downloads { get; set; }

    [JsonPropertyName("rules")]
    public List<RuleEntry>? Rules { get; set; }

    /// <summary>Betriebssystem → Classifier, z. B. "windows" → "natives-windows".</summary>
    [JsonPropertyName("natives")]
    public Dictionary<string, string>? Natives { get; set; }

    /// <summary>Maven-Repository-URL (z.B. für Fabric/Forge-Bibliotheken).</summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public sealed class LibraryDownloads
{
    [JsonPropertyName("artifact")]
    public DownloadArtifact? Artifact { get; set; }

    [JsonPropertyName("classifiers")]
    public Dictionary<string, DownloadArtifact>? Classifiers { get; set; }
}

public sealed class RuleEntry
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = "allow";

    [JsonPropertyName("os")]
    public RuleOs? Os { get; set; }

    [JsonPropertyName("features")]
    public Dictionary<string, bool>? Features { get; set; }
}

public sealed class RuleOs
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arch")]
    public string? Arch { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

public sealed class LoggingSection
{
    [JsonPropertyName("client")]
    public LoggingConfig? Client { get; set; }
}

public sealed class LoggingConfig
{
    [JsonPropertyName("argument")]
    public string Argument { get; set; } = "";

    [JsonPropertyName("file")]
    public DownloadArtifact? File { get; set; }
}

public sealed class JavaVersionSection
{
    [JsonPropertyName("majorVersion")]
    public int MajorVersion { get; set; }
}
