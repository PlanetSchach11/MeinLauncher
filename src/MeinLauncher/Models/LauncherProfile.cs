using System;

namespace MeinLauncher.Models;

/// <summary>
/// Ein Spielprofil: bündelt Minecraft-Version, Mod-Loader, Java-Runtime und
/// Arbeitsspeicher zu einer wählbaren Einheit. Jedes Profil besitzt einen
/// eigenen Mods-/Logs-Ordner unter <c>games\profiles\&lt;Name&gt;</c>.
/// </summary>
public sealed class LauncherProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "";

    /// <summary>Gewählte Minecraft-Version (Versions-ID aus der Versionsliste).</summary>
    public string VersionId { get; set; } = "";

    /// <summary>Mod-Loader (fabric, forge, neoforge, quilt, liteloader).</summary>
    public string ModLoader { get; set; } = "fabric";

    /// <summary>Pfad zur java.exe (leer = automatische Suche).</summary>
    public string JavaPath { get; set; } = "";

    public int MaxRamMb { get; set; } = 2048;
}
