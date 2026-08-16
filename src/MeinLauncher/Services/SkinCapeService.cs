using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MeinLauncher.Services;

/// <summary>Ergebnis eines Skins-/Capes-Scans.</summary>
public sealed record SkinCapeScanResult(int SkinCount, int CapeCount, IReadOnlyList<string> Files);

/// <summary>
/// Architektur-Grundlage für die automatische Skin-/Cape-Aktualisierung.
/// Konkrete Anbieter implementieren <see cref="ScanAsync"/>. Die spätere
/// Online-Anbindung (Mojang-Account) wird als eigener Anbieter ergänzt –
/// bis dahin wird bewusst nichts vorgetäuscht (siehe <see cref="OnlineSkinCapeService"/>).
/// </summary>
public interface ISkinCapeService
{
    string Name { get; }

    /// <summary>true, sobald eine echte Online-Synchronisierung implementiert ist.</summary>
    bool SupportsOnlineSync { get; }

    Task<SkinCapeScanResult> ScanAsync(string gameDirectory, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lokaler Anbieter: durchsucht den Spieldaten-Ordner nach Skins (skins/) und
/// Capes (capes/) – PNG/JPG-Dateien. Funktioniert sofort, ohne Online-Zugang.
/// </summary>
public sealed class LocalSkinCapeService : ISkinCapeService
{
    public string Name => "Lokal";

    public bool SupportsOnlineSync => false;

    public Task<SkinCapeScanResult> ScanAsync(string gameDirectory, CancellationToken cancellationToken = default)
    {
        string[] extensions = [".png", ".jpg", ".jpeg"];

        var skins = ScanFolder(Path.Combine(gameDirectory, "skins"), extensions);
        var capes = ScanFolder(Path.Combine(gameDirectory, "capes"), extensions);

        return Task.FromResult(new SkinCapeScanResult(
            skins.Count,
            capes.Count,
            skins.Concat(capes).ToList()));
    }

    private static List<string> ScanFolder(string folder, IReadOnlyCollection<string> extensions)
    {
        if (!Directory.Exists(folder))
            return [];

        return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

/// <summary>
/// Platzhalter für die zukünftige Online-Anbindung an Mojang (Skins/Capes aus
/// dem Minecraft-Account automatisch aktualisieren). Wirft absichtlich
/// <see cref="NotSupportedException"/>, damit nicht funktionierender Code
/// nicht als funktionierend ausgegeben wird.
/// </summary>
public sealed class OnlineSkinCapeService : ISkinCapeService
{
    public string Name => "Mojang (online)";

    public bool SupportsOnlineSync => false;

    public Task<SkinCapeScanResult> ScanAsync(string gameDirectory, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(
            "Die Online-Synchronisierung für Skins und Capes folgt in einem späteren Schritt.");
}
