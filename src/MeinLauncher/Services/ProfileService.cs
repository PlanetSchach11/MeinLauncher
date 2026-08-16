using System;
using System.IO;
using System.Linq;
using MeinLauncher.Models;

namespace MeinLauncher.Services;

/// <summary>Ergebnis einer Profiloperation mit lokalisierter Meldung.</summary>
public sealed record ProfileOperationResult(bool Success, string Message, LauncherProfile? Profile = null);

/// <summary>
/// Verwaltet Spielprofile: erstellen, umbenennen, duplizieren, löschen sowie die
/// Synchronisation zwischen dem aktiven Profil und den globalen Einstellungen.
///
/// Prinzip: Das aktive Profil ist die „Quelle der Wahrheit“ – beim Umschalten werden
/// dessen Werte (Version, Loader, Java, RAM) in die globalen Einstellungsfelder
/// übernommen, auf denen Versions-/Mods-/Start-Logik weiterhin arbeiten. Änderungen
/// auf diesen Seiten werden zurück ins Profil geschrieben (Write-through).
/// </summary>
public sealed class ProfileService
{
    private readonly SettingsService _settings;

    public ProfileService(SettingsService settings)
    {
        _settings = settings;
    }

    public LauncherSettings Settings => _settings.Current;

    public LauncherProfile? ActiveProfile => _settings.Current.ActiveProfile;

    // ---------------------------------------------------------------- Umschalten

    /// <summary>Aktiviert ein Profil: dessen Werte werden auf die globalen Felder übertragen.</summary>
    public void ApplyProfile(LauncherProfile profile)
    {
        var s = _settings.Current;
        s.SelectedProfileId = profile.Id;
        s.SelectedVersionId = profile.VersionId ?? "";
        s.ModLoader = string.IsNullOrWhiteSpace(profile.ModLoader) ? "fabric" : profile.ModLoader;
        s.JavaPath = profile.JavaPath ?? "";
        s.MaxRamMb = profile.MaxRamMb > 0 ? profile.MaxRamMb : 2048;
        _settings.Save();
        _settings.EnsureDirectories();
    }

    /// <summary>Aktiviert das Standardprofil (globale Einstellungen bleiben unverändert).</summary>
    public void ApplyDefault()
    {
        var s = _settings.Current;
        s.SelectedProfileId = "";
        _settings.Save();
        _settings.EnsureDirectories();
    }

    // ---------------------------------------------------------------- Operationen

    /// <summary>
    /// Erstellt ein Profil mit den aktuell wirksamen Einstellungen (Version, Loader,
    /// Java, RAM) und aktiviert es sofort.
    /// </summary>
    public ProfileOperationResult CreateProfile(string name)
    {
        if (!ValidateName(name, null, out var error))
            return new ProfileOperationResult(false, error);

        var s = _settings.Current;
        var profile = new LauncherProfile
        {
            Name = name.Trim(),
            VersionId = s.SelectedVersionId ?? "",
            ModLoader = string.IsNullOrWhiteSpace(s.ModLoader) ? "fabric" : s.ModLoader,
            JavaPath = s.JavaPath ?? "",
            MaxRamMb = s.MaxRamMb > 0 ? s.MaxRamMb : 2048,
        };

        s.Profiles.Add(profile);
        ApplyProfile(profile);
        return new ProfileOperationResult(
            true,
            LocalizationManager.Instance.Get("Home.ProfileCreated"),
            profile);
    }

    /// <summary>Benennt ein Profil um. Der Instanz-Ordner wird mit verschoben.</summary>
    public ProfileOperationResult RenameProfile(LauncherProfile profile, string name)
    {
        if (!ValidateName(name, profile.Id, out var error))
            return new ProfileOperationResult(false, error);

        var s = _settings.Current;
        var oldFolder = ProfileFolder(profile);
        profile.Name = name.Trim();

        var newFolder = ProfileFolder(profile);
        if (!string.Equals(oldFolder, newFolder, StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(oldFolder))
        {
            try
            {
                Directory.Move(oldFolder, newFolder);
            }
            catch
            {
                // Ordner ist z. B. geöffnet/gesperrt – Profilname wird trotzdem übernommen,
                // der alte Ordner bleibt bestehen (ohne Datenverlust).
            }
        }

        _settings.Save();
        return new ProfileOperationResult(
            true,
            LocalizationManager.Instance.Get("Home.ProfileRenamed"),
            profile);
    }

    /// <summary>
    /// Dupliziert ein Profil (Name „… (Kopie)“, eigene Mods-Kopie) und aktiviert die Kopie.
    /// </summary>
    public ProfileOperationResult DuplicateProfile(LauncherProfile profile)
    {
        var baseName = $"{profile.Name} (Kopie)";
        var name = baseName;
        var suffix = 2;
        while (Settings.Profiles.Any(p => p.Id != profile.Id && SameName(p.Name, name)))
        {
            name = $"{baseName} {suffix}";
            suffix++;
        }

        var copy = new LauncherProfile
        {
            Name = name,
            VersionId = profile.VersionId ?? "",
            ModLoader = string.IsNullOrWhiteSpace(profile.ModLoader) ? "fabric" : profile.ModLoader,
            JavaPath = profile.JavaPath ?? "",
            MaxRamMb = profile.MaxRamMb > 0 ? profile.MaxRamMb : 2048,
        };

        var s = _settings.Current;
        s.Profiles.Add(copy);
        CopyModsFolder(profile, copy);
        ApplyProfile(copy);
        return new ProfileOperationResult(
            true,
            LocalizationManager.Instance.Get("Home.ProfileDuplicated"),
            copy);
    }

    /// <summary>Löscht ein Profil inklusive Instanz-Ordner. Fallback auf das Standardprofil.</summary>
    public ProfileOperationResult DeleteProfile(LauncherProfile profile)
    {
        var s = _settings.Current;
        var folder = ProfileFolder(profile);

        s.Profiles.Remove(profile);
        if (s.SelectedProfileId == profile.Id)
            ApplyDefault();

        if (Directory.Exists(folder))
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch
            {
                // Ordner gesperrt – wird beim nächsten Start erneut entfernt oder bleibt harmlos.
            }
        }

        _settings.Save();
        return new ProfileOperationResult(
            true,
            LocalizationManager.Instance.Get("Home.ProfileDeleted", profile.Name));
    }

    // ---------------------------------------------------------------- Synchronisation

    /// <summary>Schreibt die gewählte Version ins aktive Profil (Write-through).</summary>
    public void SyncVersion(string versionId)
    {
        var s = _settings.Current;
        s.SelectedVersionId = versionId;
        if (s.ActiveProfile is { } p)
            p.VersionId = versionId;
    }

    /// <summary>Schreibt den gewählten Loader ins aktive Profil (Write-through).</summary>
    public void SyncLoader(string loader)
    {
        var s = _settings.Current;
        s.ModLoader = loader;
        if (s.ActiveProfile is { } p)
            p.ModLoader = loader;
    }

    /// <summary>Schreibt den Java-Pfad ins aktive Profil (Write-through).</summary>
    public void SyncJavaPath(string javaPath)
    {
        var s = _settings.Current;
        s.JavaPath = javaPath;
        if (s.ActiveProfile is { } p)
            p.JavaPath = javaPath;
    }

    /// <summary>Schreibt die RAM-Größe ins aktive Profil (Write-through).</summary>
    public void SyncRam(int ramMb)
    {
        var s = _settings.Current;
        s.MaxRamMb = ramMb;
        if (s.ActiveProfile is { } p)
            p.MaxRamMb = ramMb;
    }

    // ---------------------------------------------------------------- Hilfen

    /// <summary>Pfad zum Instanz-Ordner eines Profils (auch wenn es nicht aktiv ist).</summary>
    public string ProfileFolder(LauncherProfile profile)
    {
        var s = _settings.Current;
        return Path.Combine(s.GameDirectory, "profiles", LauncherSettings.SanitizeFolderName(profile.Name));
    }

    private bool ValidateName(string name, string? excludeId, out string error)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            error = LocalizationManager.Instance.Get("Home.ProfileEmptyName");
            return false;
        }

        if (Settings.Profiles.Any(p => p.Id != excludeId && SameName(p.Name, trimmed)))
        {
            error = LocalizationManager.Instance.Get("Home.ProfileNameExists");
            return false;
        }

        var folder = LauncherSettings.SanitizeFolderName(trimmed);
        if (Settings.Profiles.Any(p =>
                p.Id != excludeId &&
                string.Equals(
                    LauncherSettings.SanitizeFolderName(p.Name),
                    folder,
                    StringComparison.OrdinalIgnoreCase)))
        {
            error = LocalizationManager.Instance.Get("Home.ProfileFolderConflict");
            return false;
        }

        error = "";
        return true;
    }

    private static bool SameName(string a, string b)
        => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Kopiert den Mods-Ordner eines Profils inklusive deaktivierter Mods.</summary>
    private void CopyModsFolder(LauncherProfile source, LauncherProfile target)
    {
        var sourceDir = Path.Combine(ProfileFolder(source), "mods");
        var targetDir = Path.Combine(ProfileFolder(target), "mods");
        if (!Directory.Exists(sourceDir))
            return;

        try
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.EnumerateFiles(sourceDir, "*.jar*", SearchOption.TopDirectoryOnly))
            {
                if (file.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: false);
                }
            }
        }
        catch
        {
            // Kopieren ist Best-Effort – ein gesperrter Ordner bricht das Duplizieren nicht ab.
        }
    }
}
