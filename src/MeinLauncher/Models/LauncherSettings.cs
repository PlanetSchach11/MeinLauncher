using System;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace MeinLauncher.Models;

/// <summary>
/// Gespeicherte Launcher-Einstellungen (JSON).
/// </summary>
public sealed class LauncherSettings
{
    public string Username { get; set; } = "Spieler";

    public string GameDirectory { get; set; } = DefaultGameDirectory;

    public string JavaPath { get; set; } = string.Empty;

    public int MaxRamMb { get; set; } = 2048;

    public string SelectedVersionId { get; set; } = string.Empty;

    public string Theme { get; set; } = "Dark";

    /// <summary>Akzentfarbe (Schlüssel aus ThemeManager.Accents, z. B. "Green").</summary>
    public string Accent { get; set; } = "Green";

    /// <summary>Transparenz: "None", "Light" oder "Strong".</summary>
    public string Transparency { get; set; } = "None";

    /// <summary>Pfad zu einem eigenen Hintergrundbild (leer = keins).</summary>
    public string BackgroundImagePath { get; set; } = string.Empty;

    /// <summary>Konfiguration des dekorativen, animierten Hintergrunds.</summary>
    public BackgroundConfig Background { get; set; } = new();

    /// <summary>Sprache: "de" oder "en".</summary>
    public string Language { get; set; } = "de";

    /// <summary>
    /// Video-Id des zuletzt in den News angesehenen YouTube-Uploads (leer = noch
    /// nie gesehen). Der Launcher zeigt den roten Punkt in der Seitenleiste, solange
    /// ein neueres Video existiert. Wird nur lokal gespeichert.
    /// </summary>
    public string LastSeenNewsVideoId { get; set; } = string.Empty;

    /// <summary>UI-Klick-Sound aktiviert (sehr dezenter „Click“ bei Schaltflächen/Navigation).</summary>
    public bool SoundEnabled { get; set; } = true;

    /// <summary>Lautstärke des UI-Klick-Sounds (0–100).</summary>
    public int SoundVolume { get; set; } = 25;

    /// <summary>
    /// Mod-Loader für den Mods-Bereich (fabric, forge, neoforge, quilt, liteloader).
    /// Wird für Suche und Installation bei Modrinth berücksichtigt.
    /// </summary>
    public string ModLoader { get; set; } = "fabric";

    /// <summary>
    /// CurseForge-API-Schlüssel für die Modsuche. Leer = nicht konfiguriert.
    /// Der CurseForge-Bereich in Mods zeigt dann einen Hinweis statt Suchergebnissen.
    /// </summary>
    public string CurseForgeApiKey { get; set; } = string.Empty;

    /// <summary>Alle Spielprofile. Das Standardprofil (ohne Eintrag) ist implizit enthalten.</summary>
    public System.Collections.Generic.List<LauncherProfile> Profiles { get; set; } = [];

    /// <summary>Id des aktiven Profils (leer = Standardprofil mit den globalen Einstellungen).</summary>
    public string SelectedProfileId { get; set; } = string.Empty;

    /// <summary>Das aktive Profil oder null, wenn das Standardprofil verwendet wird.</summary>
    [JsonIgnore]
    public LauncherProfile? ActiveProfile =>
        Profiles.FirstOrDefault(p => p.Id == SelectedProfileId);

    public static string DefaultGameDirectory { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MeinLauncher",
            "games");

    public string VersionsDirectory => Path.Combine(GameDirectory, "versions");

    /// <summary>
    /// Instanz-Verzeichnis des aktiven Profils: beim Standardprofil das Spieldaten-
    /// Verzeichnis selbst, sonst <c>Spieldaten\profiles\&lt;Name&gt;</c>. Versionen
    /// bleiben global, Mods und Logs sind damit pro Profil getrennt.
    /// </summary>
    [JsonIgnore]
    public string InstanceDirectory =>
        ActiveProfile is { } profile
            ? Path.Combine(GameDirectory, "profiles", SanitizeFolderName(profile.Name))
            : GameDirectory;

    public string ModsDirectory => Path.Combine(InstanceDirectory, "mods");

    public string LogsDirectory => Path.Combine(InstanceDirectory, "logs");

    /// <summary>
    /// Ersetzt ungültige Dateinamenzeichen in einem Profilnamen, damit daraus ein
    /// sicherer Unterordnername entsteht.
    /// </summary>
    public static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = (name ?? "").Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var result = new string(chars).Trim().Trim('.');
        return result.Length == 0 ? "profil" : result;
    }
}
