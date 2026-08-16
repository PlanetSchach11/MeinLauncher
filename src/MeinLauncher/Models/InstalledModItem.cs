using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using MeinLauncher.Services;

namespace MeinLauncher.Models;

/// <summary>
/// Gemeinsame Basis für anzeigbare Mod-Einträge: lädt das Icon von Modrinth (CDN).
/// </summary>
    public abstract partial class ModDisplayItem : ObservableObject
{
    [ObservableProperty]
    public partial string IconUrl { get; set; } = "";

    [ObservableProperty]
    public partial Bitmap? Icon { get; set; }

    private bool _iconRequested;

    /// <summary>Lädt das Modrinth-Icon asynchron (optional – Fehler bleiben stumm).</summary>
    public async Task LoadIconAsync(ModrinthApiClient api)
    {
        if (_iconRequested || string.IsNullOrWhiteSpace(IconUrl))
            return;

        _iconRequested = true;
        try
        {
            var bytes = await api.GetIconBytesAsync(IconUrl);
            using var ms = new MemoryStream(bytes);
            Icon = new Bitmap(ms);
        }
        catch
        {
            // Ohne Icon wird der Platzhalter angezeigt.
        }
    }
}

/// <summary>
/// Eine installierte Mod-Datei im Mods-Ordner mit echten Metadaten aus der .jar
/// und (falls erkennbar) der zugehörigen Modrinth-Version.
/// </summary>
public partial class InstalledModItem : ModDisplayItem
{
    public LocalizationManager L => LocalizationManager.Instance;

    public string FullPath { get; set; } = "";

    public string FileName { get; init; } = "";

    public long SizeBytes { get; init; }

    public DateTime Modified { get; init; }

    /// <summary>ModId aus der .jar-Metadaten (z. B. "sodium").</summary>
    public string ModId { get; init; } = "";

    /// <summary>Anzeigename aus den Metadaten bzw. Dateiname.</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>Version aus der .jar bzw. von Modrinth.</summary>
    public string Version { get; init; } = "";

    /// <summary>Loader aus der .jar (fabric/quilt/forge/neoforge/liteloader).</summary>
    public string Loader { get; init; } = "";

    /// <summary>Modrinth-Projekt (falls die Datei über den SHA-1-Hash erkannt wurde).</summary>
    public string? ProjectId { get; init; }

    public string? ProjectSlug { get; init; }

    public long ProjectDownloads { get; init; }

    public bool HasModrinthInfo => !string.IsNullOrEmpty(ProjectId);

    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool HasUpdate { get; set; }

    [ObservableProperty]
    public partial bool IsUpdating { get; set; }

    [ObservableProperty]
    public partial string UpdateVersionLabel { get; set; } = "";

    /// <summary>Tooltip auf dem Update-Badge (nutzt den lokalen Text „Update verfügbar: …“).</summary>
    public string UpdateTooltip => L.ModsUpdateAvailable(UpdateVersionLabel);

    public string SizeLabel
    {
        get
        {
            string[] units = ["B", "KB", "MB", "GB"];
            double size = SizeBytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return $"{size:0.##} {units[unit]}";
        }
    }

    public string StatusLabel => IsEnabled ? L.ModsActive : L.ModsDisabled;

    partial void OnIsEnabledChanged(bool value) => OnPropertyChanged(nameof(StatusLabel));

    /// <summary>Zweite Zeile: Version · Loader · Größe (bzw. „unbekannt“).</summary>
    public string MetaLabel
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();
            parts.Add(Version.Length > 0 ? $"v{Version}" : L.ModsUnknownVersion);
            parts.Add(Loader.Length > 0 ? Loader : L.ModsUnknownLoader);
            parts.Add(SizeLabel);
            return string.Join(" · ", parts);
        }
    }

    /// <summary>Untertitel: Dateiname und Änderungsdatum.</summary>
    public string FileLabel => $"{FileName} · {Modified:dd.MM.yyyy HH:mm}";

    public string Initial => DisplayName.Length > 0 ? DisplayName[..1].ToUpperInvariant() : "?";
}
