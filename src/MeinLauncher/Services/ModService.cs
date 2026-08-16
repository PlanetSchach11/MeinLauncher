using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MeinLauncher.Models;

namespace MeinLauncher.Services;

/// <summary>
/// Scannt den Mods-Ordner und verwaltet Mod-Dateien (.jar / .jar.disabled).
/// </summary>
public sealed class ModService
{
    private const string DisabledSuffix = ".jar.disabled";
    private const string JarSuffix = ".jar";

    public IReadOnlyList<ModEntry> ScanMods(string modsDirectory)
    {
        if (!Directory.Exists(modsDirectory))
            return [];

        return Directory
            .EnumerateFiles(modsDirectory, "*.jar*", SearchOption.TopDirectoryOnly)
            .Where(f =>
                f.EndsWith(JarSuffix, StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase))
            .Select(f =>
            {
                var info = new FileInfo(f);
                var isDisabled = f.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase);
                return new ModEntry
                {
                    FullPath = f,
                    Name = Path.GetFileName(f),
                    SizeBytes = info.Length,
                    Modified = info.LastWriteTime,
                    IsEnabled = !isDisabled,
                };
            })
            .OrderBy(m => m.Name)
            .ToList();
    }

    /// <summary>Löscht eine Mod-Datei endgültig (Deinstallieren).</summary>
    public void UninstallFile(string fullPath)
    {
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    /// <summary>
    /// Aktiviert/deaktiviert eine Mod über ihren Pfad (umbenennen .jar &lt;-&gt; .jar.disabled).
    /// Gibt den neuen Pfad zurück.
    /// </summary>
    public string SetEnabledByPath(string fullPath, bool enabled)
    {
        var isDisabled = fullPath.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase);
        if (enabled == !isDisabled)
            return fullPath;

        var target = enabled
            ? fullPath.Substring(0, fullPath.Length - DisabledSuffix.Length)
            : fullPath + DisabledSuffix;

        File.Move(fullPath, target, overwrite: true);
        return target;
    }

    /// <summary>SHA-1 einer Datei (streamend, für die Modrinth-Hash-Erkennung).</summary>
    public string ComputeSha1(string fullPath)
    {
        using var stream = File.OpenRead(fullPath);
        var hash = SHA1.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
