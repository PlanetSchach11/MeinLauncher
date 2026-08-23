using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MeinLauncher.Services;

namespace MeinLauncher.Models;

/// <summary>
/// Ein CurseForge-Projekt als Suchergebnis.
/// Enthält echte Daten von der CurseForge-API.
/// </summary>
public partial class CurseForgeModItem : ModDisplayItem
{
    public LocalizationManager L => LocalizationManager.Instance;

    /// <summary>CurseForge Mod-ID (numerisch).</summary>
    public int CurseForgeModId { get; init; }

    public string Slug { get; init; } = "";

    public string Title { get; init; } = "";

    public string Author { get; init; } = "";

    public string Description { get; init; } = "";

    public long Downloads { get; init; }

    public List<string> Loaders { get; init; } = [];

    public List<string> GameVersions { get; init; } = [];

    /// <summary>Project IDs used for dedup with Modrinth.</summary>
    public string ProjectId => $"cf-{CurseForgeModId}";

    /// <summary>Ist dieses Projekt bereits installiert.</summary>
    [ObservableProperty]
    public partial bool IsInstalled { get; set; }

    /// <summary>Für die installierte Version dieses Projekts ist ein Update verfügbar.</summary>
    [ObservableProperty]
    public partial bool HasUpdate { get; set; }

    [ObservableProperty]
    public partial bool IsInstalling { get; set; }

    /// <summary>Update-Lauf aktiv.</summary>
    [ObservableProperty]
    public partial bool IsUpdating { get; set; }

    /// <summary>Button „Installieren" nur für nicht installierte Projekte.</summary>
    public bool ShowInstallButton => !IsInstalled;

    /// <summary>Deaktivierter „Installiert"-Button.</summary>
    public bool ShowInstalledButton => IsInstalled && !HasUpdate;

    /// <summary>Aktiver „Update"-Button.</summary>
    public bool ShowUpdateButton => IsInstalled && HasUpdate;

    partial void OnIsInstalledChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowInstallButton));
        OnPropertyChanged(nameof(ShowInstalledButton));
        OnPropertyChanged(nameof(ShowUpdateButton));
    }

    partial void OnHasUpdateChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowInstalledButton));
        OnPropertyChanged(nameof(ShowUpdateButton));
    }

    public string DownloadsLabel => L.ModsDownloads(Downloads);

    public string AuthorLine => $"{Author} · {DownloadsLabel}";

    /// <summary>Meta-Zeile: Loader · unterstützte MC-Versionen (gekürzt).</summary>
    public string MetaLabel => $"{LoaderLabel} · MC {GameVersionsLabel}";

    public string GameVersionsLabel => GameVersions.Count == 0
        ? "?"
        : string.Join(", ", GameVersions.Take(3));

    public string DescriptionTrimmed
    {
        get
        {
            var text = Description.Replace("\r", " ").Replace("\n", " ");
            return text.Length <= 160 ? text : text[..157] + "…";
        }
    }

    /// <summary>Loader der aktuell passenden Versionen.</summary>
    public string LoaderLabel => Loaders.Count == 0
        ? L.ModsUnknownLoader
        : string.Join(", ", Loaders.Select(NormalizeLoader));

    public static string NormalizeLoader(string loader) => loader switch
    {
        "fabric" => "Fabric",
        "forge" => "Forge",
        "neoforge" => "NeoForge",
        "quilt" => "Quilt",
        "liteloader" => "LiteLoader",
        _ => loader,
    };

    public string Initial => Title.Length > 0 ? Title[..1].ToUpperInvariant() : "?";
}
