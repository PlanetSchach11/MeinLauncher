using System;
using System.IO;
using System.Text.Json;
using MeinLauncher.Models;

namespace MeinLauncher.Services;

/// <summary>
/// Lädt und speichert die Launcher-Einstellungen als JSON.
/// </summary>
public sealed class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MeinLauncher",
        "settings.json");

    public LauncherSettings Current { get; private set; } = new();

    public SettingsService()
    {
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<LauncherSettings>(json);
                if (loaded is not null)
                {
                    Current = loaded;
                    return;
                }
            }
        }
        catch
        {
            // Beschädigte Einstellungen ignorieren und Standardwerte verwenden.
        }

        Current = new LauncherSettings();
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Current.VersionsDirectory);
        Directory.CreateDirectory(Current.ModsDirectory);
        Directory.CreateDirectory(Current.LogsDirectory);

        // Wurzel für profilbasierte Instanz-Ordner anlegen (Leer-Operation beim Standardprofil).
        Directory.CreateDirectory(Path.Combine(Current.GameDirectory, "profiles"));
    }
}
