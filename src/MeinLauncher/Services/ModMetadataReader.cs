using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace MeinLauncher.Services;

/// <summary>
/// Liest echte Mod-Metadaten direkt aus der .jar (Zip):
/// fabric.mod.json, quilt.mod.json, META-INF/mods.toml (Forge/NeoForge),
/// META-INF/neoforge.mods.toml, mcmod.info und litemod.json.
/// Liefert null, wenn die Datei keine erkennbaren Mod-Metadaten enthält.
/// </summary>
public sealed class InstalledModMeta
{
    public string ModId { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string Version { get; set; } = "";

    /// <summary>Normalisierter Loader: fabric, quilt, forge, neoforge oder liteloader.</summary>
    public string Loader { get; set; } = "";
}

public static class ModMetadataReader
{
    public static InstalledModMeta? Read(string jarPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(jarPath);

            // LiteLoader
            var litemod = ReadEntry(archive, "litemod.json");
            if (litemod is not null)
                return ParseLiteLoader(litemod);

            // Quilt (hat eigene Mod-Deskriptor-Datei)
            var quilt = ReadEntry(archive, "quilt.mod.json");
            if (quilt is not null)
            {
                var meta = ParseFabricStyle(quilt);
                if (meta is not null)
                {
                    meta.Loader = "quilt";
                    return meta;
                }
            }

            // Fabric
            var fabric = ReadEntry(archive, "fabric.mod.json");
            if (fabric is not null)
                return ParseFabricStyle(fabric);

            // NeoForge
            var neoforge = ReadEntry(archive, "META-INF/neoforge.mods.toml");
            if (neoforge is not null)
                return ParseToml(neoforge, "neoforge");

            // Forge (neu: mods.toml)
            var forge = ReadEntry(archive, "META-INF/mods.toml");
            if (forge is not null)
                return ParseToml(forge, "forge");

            // Forge (alt: mcmod.info)
            var mcmod = ReadEntry(archive, "mcmod.info");
            if (mcmod is not null)
                return ParseMcmodInfo(mcmod);
        }
        catch
        {
            // Beschädigte oder unlesbare Datei – als „unbekannt“ behandeln.
        }

        return null;
    }

    private static string? ReadEntry(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        if (entry is null)
            return null;

        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    /// <summary>fabric.mod.json / quilt.mod.json (gleiche Struktur).</summary>
    private static InstalledModMeta? ParseFabricStyle(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        var id = GetString(root, "id") ?? "";
        if (id.Length == 0)
            return null;

        return new InstalledModMeta
        {
            ModId = id,
            DisplayName = GetString(root, "name") ?? id,
            Version = GetString(root, "version") ?? "",
            Loader = "fabric",
        };
    }

    /// <summary>LiteLoader: {"name": "...", "version": "...", "mcversion": "..."}</summary>
    private static InstalledModMeta? ParseLiteLoader(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var name = GetString(root, "name") ?? "";
        if (name.Length == 0)
            return null;

        return new InstalledModMeta
        {
            ModId = name,
            DisplayName = name,
            Version = GetString(root, "version") ?? "",
            Loader = "liteloader",
        };
    }

    /// <summary>TOML (mods.toml / neoforge.mods.toml): erste [[mods]]-Sektion.</summary>
    private static InstalledModMeta? ParseToml(string toml, string loader)
    {
        var modId = "";
        var displayName = "";
        var version = "";

        foreach (var rawLine in toml.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("#", StringComparison.Ordinal) || line.Length == 0)
                continue;

            if (line.StartsWith("[[mods]]", StringComparison.Ordinal))
            {
                // Bei mehreren Einträgen nur den ersten auswerten.
                if (modId.Length > 0)
                    break;
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim().Trim('"');

            if (key == "modId" && modId.Length == 0)
                modId = value;
            else if (key == "version" && version.Length == 0)
                version = value;
            else if (key is "displayName" or "name" && displayName.Length == 0)
                displayName = value;
        }

        if (modId.Length == 0)
            return null;

        return new InstalledModMeta
        {
            ModId = modId,
            DisplayName = displayName.Length > 0 ? displayName : modId,
            Version = version,
            Loader = loader,
        };
    }

    /// <summary>Alte Forge-Metadaten: JSON-Array von Mod-Beschreibungen.</summary>
    private static InstalledModMeta? ParseMcmodInfo(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            return null;

        var first = doc.RootElement[0];
        var modId = GetString(first, "modid") ?? "";
        if (modId.Length == 0)
            return null;

        return new InstalledModMeta
        {
            ModId = modId,
            DisplayName = GetString(first, "name") ?? modId,
            Version = GetString(first, "version") ?? "",
            Loader = "forge",
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }
}
