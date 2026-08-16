using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MeinLauncher.Services;

namespace MeinLauncher.Models;

/// <summary>
/// Ein Modrinth-Projekt als Suchergebnis (Suche und Modrinth-Bereich).
/// Enthält echte Daten von der Modrinth-API.
/// </summary>
public partial class ModrinthModItem : ModDisplayItem
{
    public LocalizationManager L => LocalizationManager.Instance;

    public string ProjectId { get; init; } = "";

    public string Slug { get; init; } = "";

    public string Title { get; init; } = "";

    public string Author { get; init; } = "";

    public string Description { get; init; } = "";

    public long Downloads { get; init; }

    public long Follows { get; init; }

    public List<string> Loaders { get; init; } = [];

    public List<string> GameVersions { get; init; } = [];

    /// <summary>Ist dieses Projekt bereits installiert (kein Doppel-Download).</summary>
    [ObservableProperty]
    public partial bool IsInstalled { get; set; }

    /// <summary>Für die installierte Version dieses Projekts ist ein Update verfügbar.</summary>
    [ObservableProperty]
    public partial bool HasUpdate { get; set; }

    [ObservableProperty]
    public partial bool IsInstalling { get; set; }

    /// <summary>Update-Lauf für dieses bereits installierte Projekt aktiv.</summary>
    [ObservableProperty]
    public partial bool IsUpdating { get; set; }

    /// <summary>Button „Installieren“ nur für nicht installierte Projekte.</summary>
    public bool ShowInstallButton => !IsInstalled;

    /// <summary>Deaktivierter „Installiert“-Button (installiert, kein Update).</summary>
    public bool ShowInstalledButton => IsInstalled && !HasUpdate;

    /// <summary>Aktiver „Update“-Button (installiert, Update verfügbar).</summary>
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

    public string FollowsLabel => L.ModsFollows(Follows);

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

    /// <summary>Loader der aktuell passenden Versionen (z. B. "Fabric, Quilt").</summary>
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
