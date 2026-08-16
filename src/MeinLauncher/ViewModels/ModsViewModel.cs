using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeinLauncher.Models;
using MeinLauncher.Services;

namespace MeinLauncher.ViewModels;

/// <summary>
/// Mods-Seite mit zwei Bereichen:
///  - Installierte Mods: lokale .jar-Dateien (auch .jar.disabled), per SHA-1 exakt
///    gegen Modrinth erkannt (Update-Hinweis über die neueste passende Version).
///    Der lokale Scan läuft immer – die Modrinth-Anreicherung ist optional.
///  - Modrinth: direkt eingebettet, echte API-Daten, Suche und Installation ohne Browser.
/// </summary>
public partial class ModsViewModel : ViewModelBase
{
    private static readonly string[] KnownLoaders = ["fabric", "forge", "neoforge", "quilt", "liteloader"];

    private readonly SettingsService _settings;
    private readonly ModService _modService;
    private readonly ProfileService _profileService;
    private readonly ModrinthApiClient _api = new();
    private readonly SemaphoreSlim _installLock = new(1, 1);

    private HashSet<string> _installedProjectIds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>ModIds aus lokalen .jar-Metadaten (Fallback-Vergleich für Suchergebnisse).</summary>
    private HashSet<string> _installedModIds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Projekt-Id → Id der installierten Version (für Update-Erkennung).</summary>
    private Dictionary<string, string> _installedVersionById = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Projekt-Id → Id der neuesten passenden Version (für Update-Erkennung).</summary>
    private Dictionary<string, string> _latestVersionById = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Wechselt zur Versionsseite (vom „Zur Versionsauswahl“-Button).</summary>
    public Action? OpenVersionsRequested { get; init; }

    public ObservableCollection<InstalledModItem> InstalledMods { get; } = [];
    public ObservableCollection<ModrinthModItem> BrowseResults { get; } = [];

    public ObservableCollection<ModLoaderItem> LoaderOptions { get; } =
    [
        new ModLoaderItem("fabric"),
        new ModLoaderItem("forge"),
        new ModLoaderItem("neoforge"),
        new ModLoaderItem("quilt"),
        new ModLoaderItem("liteloader"),
    ];

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ModrinthQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowInstalled { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowModrinth { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsBrowsing { get; set; }

    [ObservableProperty]
    public partial bool HasBrowsed { get; set; }

    [ObservableProperty]
    public partial ModLoaderItem? SelectedLoaderOption { get; set; }

    public ModsViewModel(SettingsService settings, ModService modService, ProfileService profileService)
    {
        _settings = settings;
        _modService = modService;
        _profileService = profileService;
        SelectedLoaderOption = LoaderOptions.FirstOrDefault(o => o.Value == settings.Current.ModLoader) ?? LoaderOptions[0];
    }

    public string ModsDirectory => _settings.Current.ModsDirectory;

    /// <summary>Angezeigte Minecraft-Version (aus der Versions-Seite).</summary>
    public string GameVersion
    {
        get
        {
            var v = _settings.Current.SelectedVersionId;
            return string.IsNullOrWhiteSpace(v) ? "—" : v;
        }
    }

    public bool HasGameVersion => !string.IsNullOrWhiteSpace(_settings.Current.SelectedVersionId);

    public bool IsInstalledEmpty => InstalledMods.Count == 0;
    public bool IsBrowseEmpty => BrowseResults.Count == 0;

    partial void OnSelectedLoaderOptionChanged(ModLoaderItem? value)
    {
        if (value is null)
            return;

        if (_settings.Current.ModLoader != value.Value)
        {
            // Write-through: globale Auswahl + aktives Profil.
            _profileService.SyncLoader(value.Value);
            _settings.Save();
        }
    }

    /// <summary>
    /// Beim Öffnen der Mods-Seite: gleicht die Loader-Auswahl mit dem (ggf.
    /// über ein Profil gewählten) Loader ab und aktualisiert den Ordnerpfad.
    /// </summary>
    public void Refresh()
    {
        SelectedLoaderOption = LoaderOptions.FirstOrDefault(o => o.Value == _settings.Current.ModLoader)
                               ?? LoaderOptions[0];
        OnPropertyChanged(nameof(ModsDirectory));
        OnPropertyChanged(nameof(GameVersion));
        OnPropertyChanged(nameof(HasGameVersion));
    }

    /// <summary>Beim Öffnen der Seite: installierte Mods neu scannen.</summary>
    public void Start()
    {
        if (!IsBusy)
            _ = RefreshInstalledAsync();
    }

    [RelayCommand]
    private void SelectSection(string? key)
    {
        ShowInstalled = key == "installed";
        ShowModrinth = key == "modrinth";

        if (ShowInstalled)
        {
            // Beim Wechsel auf „Installierte Mods“ zuverlässig neu scannen.
            if (!IsBusy)
                _ = RefreshInstalledAsync();
        }
        else if (ShowModrinth && BrowseResults.Count == 0 && !IsBrowsing)
        {
            _ = BrowseModrinthAsync();
        }
    }

    [RelayCommand]
    private void GoToVersions()
    {
        OpenVersionsRequested?.Invoke();
    }

    [RelayCommand]
    public async Task RefreshInstalledAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = t("Mods.IsLoading");
        try
        {
            await LoadInstalledAsync();
            StatusMessage = InstalledMods.Count == 0
                ? t("Mods.EmptyShort")
                : t("Mods.Count", InstalledMods.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = t("Mods.Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Scannt den Mods-Ordner (über <see cref="ModService"/>, inkl. .jar.disabled)
    /// und zeigt die lokalen Mods immer an. Die Modrinth-Anreicherung (SHA-1-Erkennung,
    /// Projektinfos, Update-Hinweise) ist Best-Effort: Ein API-Fehler verbirgt niemals
    /// die lokal vorhandenen Mods.
    /// </summary>
    private async Task LoadInstalledAsync()
    {
        var modsDir = _settings.Current.ModsDirectory;
        var entries = _modService.ScanMods(modsDir);

        // 1) Lokale Metadaten + SHA-1 (CPU-lastig -> Task.Run)
        var prepared = await Task.Run(() => entries.Select(entry =>
        {
            var meta = ModMetadataReader.Read(entry.FullPath);
            return new PreparedFile
            {
                Path = entry.FullPath,
                IsEnabled = entry.IsEnabled,
                Size = entry.SizeBytes,
                Modified = entry.Modified,
                ModId = meta?.ModId ?? "",
                DisplayName = meta?.DisplayName ?? "",
                Version = meta?.Version ?? "",
                Loader = meta?.Loader ?? "",
                Sha1 = _modService.ComputeSha1(entry.FullPath),
            };
        }).ToList());

        var installedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var installedModIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var installedVersionById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var latestVersionById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in prepared)
        {
            if (p.ModId.Length > 0)
                installedModIds.Add(p.ModId);
        }

        // 2) Lokale Liste sofort aufbauen (auch wenn die API nicht erreichbar ist).
        var localItems = BuildInstalledItems(prepared, hashMap: null, projectById: null, latestByProject: null);
        ReplaceInstalledList(localItems);

        // 3) Best-effort-Anreicherung über die Modrinth-API.
        try
        {
            var hashMap = await _api.GetVersionsByHashesAsync(prepared.Select(p => p.Sha1).ToList());

            // Projektinfos in einem Batch-Request
            var projectIds = hashMap.Values
                .Where(v => v is not null)
                .Select(v => v!.ProjectId)
                .Distinct()
                .ToList();
            var projects = await _api.GetProjectsAsync(projectIds);
            var projectById = projects.ToDictionary(p => p.ProjectId, StringComparer.OrdinalIgnoreCase);

            // Neueste passende Version je Projekt (für den Update-Hinweis)
            var gameVersion = GetSelectedVersion();
            var latestByProject = new Dictionary<string, ModrinthVersionDto?>(StringComparer.OrdinalIgnoreCase);
            if (gameVersion.Length > 0)
            {
                foreach (var pid in projectIds)
                {
                    try
                    {
                        var versions = await _api.GetVersionsAsync(pid, gameVersion, SelectedLoaderOption?.Value ?? "fabric");
                        latestByProject[pid] = versions.FirstOrDefault();
                    }
                    catch
                    {
                        latestByProject[pid] = null;
                    }
                }
            }

            // Erkennung + Update-Maps füllen
            foreach (var p in prepared)
            {
                if (!hashMap.TryGetValue(p.Sha1, out var version) || version is null)
                    continue;
                if (!projectById.TryGetValue(version.ProjectId, out var project))
                    continue;

                installedIds.Add(project.ProjectId);
                installedVersionById[project.ProjectId] = version.Id;
                if (latestByProject.TryGetValue(project.ProjectId, out var latest) && latest is not null)
                    latestVersionById[project.ProjectId] = latest.Id;
            }

            _installedProjectIds = installedIds;
            _installedModIds = installedModIds;
            _installedVersionById = installedVersionById;
            _latestVersionById = latestVersionById;

            var enriched = BuildInstalledItems(prepared, hashMap, projectById, latestByProject);
            ReplaceInstalledList(enriched);
            foreach (var item in enriched)
                _ = item.LoadIconAsync(_api);
        }
        catch
        {
            // API nicht erreichbar: lokale Liste bleibt sichtbar.
            _installedProjectIds = installedIds;
            _installedModIds = installedModIds;
            _installedVersionById = installedVersionById;
            _latestVersionById = latestVersionById;
        }

        UpdateInstalledMarks();
        OnPropertyChanged(nameof(IsInstalledEmpty));
    }

    private void ReplaceInstalledList(List<InstalledModItem> items)
    {
        InstalledMods.Clear();
        foreach (var item in items)
            InstalledMods.Add(item);
    }

    /// <summary>
    /// Baut die Anzeige-Items für den Mods-Ordner. Ohne API-Daten (alle Parameter null)
    /// entstehen reine „lokale“ Einträge mit den Metadaten aus der .jar.
    /// </summary>
    private static List<InstalledModItem> BuildInstalledItems(
        List<PreparedFile> prepared,
        Dictionary<string, ModrinthVersionDto?>? hashMap,
        Dictionary<string, ModrinthProjectDto>? projectById,
        Dictionary<string, ModrinthVersionDto?>? latestByProject)
    {
        var items = new List<InstalledModItem>();

        foreach (var p in prepared)
        {
            ModrinthVersionDto? version = null;
            ModrinthProjectDto? project = null;
            if (hashMap is not null && hashMap.TryGetValue(p.Sha1, out var v) && v is not null)
            {
                version = v;
                if (projectById is not null)
                    projectById.TryGetValue(v.ProjectId, out project);
            }

            var item = new InstalledModItem
            {
                FullPath = p.Path,
                FileName = Path.GetFileName(p.Path),
                SizeBytes = p.Size,
                Modified = p.Modified,
                IsEnabled = p.IsEnabled,
                ModId = p.ModId,
                DisplayName = p.DisplayName.Length > 0
                    ? p.DisplayName
                    : project?.Title ?? Path.GetFileNameWithoutExtension(p.Path),
                Version = version?.VersionNumber ?? p.Version,
                Loader = p.Loader.Length > 0 ? p.Loader : version?.Loaders.FirstOrDefault() ?? "",
                ProjectId = project?.ProjectId,
                ProjectSlug = project?.Slug,
                ProjectDownloads = project?.Downloads ?? 0,
                IconUrl = project?.IconUrl ?? "",
            };

            if (project is not null && version is not null &&
                latestByProject is not null &&
                latestByProject.TryGetValue(project.ProjectId, out var latest) && latest is not null &&
                !string.Equals(latest.Id, version.Id, StringComparison.Ordinal))
            {
                item.HasUpdate = true;
                item.UpdateVersionLabel = latest.VersionNumber;
            }

            items.Add(item);
        }

        return items
            .OrderByDescending(m => m.IsEnabled)
            .ThenBy(m => m.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    [RelayCommand]
    public async Task BrowseModrinthAsync()
    {
        if (IsBrowsing)
            return;
        if (!HasGameVersion)
        {
            StatusMessage = t("Mods.NoVersionSelected");
            return;
        }

        IsBrowsing = true;
        HasBrowsed = true;
        StatusMessage = t("Mods.IsLoading");
        try
        {
            var hits = await _api.SearchAsync(
                ModrinthQuery?.Trim() ?? "",
                GetSelectedVersion(),
                SelectedLoaderOption?.Value ?? "fabric",
                limit: 30,
                index: "relevance");

            BrowseResults.Clear();
            foreach (var hit in hits)
                BrowseResults.Add(CreateResultItem(hit));

            StatusMessage = BrowseResults.Count == 0 ? t("Mods.NoResults") : t("Mods.Count", BrowseResults.Count);
            OnPropertyChanged(nameof(IsBrowseEmpty));
        }
        catch (Exception ex)
        {
            StatusMessage = t("Mods.SearchError", ex.Message);
        }
        finally
        {
            IsBrowsing = false;
        }
    }

    /// <summary>Lädt die beliebten Mods (Query wird dafür geleert).</summary>
    [RelayCommand]
    private async Task BrowsePopularAsync()
    {
        ModrinthQuery = "";
        await BrowseModrinthAsync();
    }

    [RelayCommand]
    private async Task InstallAsync(ModrinthModItem? item)
    {
        if (item is null)
            return;
        if (!HasGameVersion)
        {
            StatusMessage = t("Mods.NoVersionSelected");
            return;
        }

        await _installLock.WaitAsync();
        try
        {
            if (item.IsInstalled)
            {
                StatusMessage = t("Mods.InstallAlready", item.Title);
                return;
            }

            item.IsInstalling = true;
            StatusMessage = t("Mods.Installing");

            var loader = SelectedLoaderOption?.Value ?? "fabric";
            var gameVersion = GetSelectedVersion();

            // Vorprüfung: Hat das Projekt überhaupt eine passende Version?
            var versions = await _api.GetVersionsAsync(item.ProjectId, gameVersion, loader);
            if (versions.Count == 0)
            {
                StatusMessage = t("Mods.InstallNoVersion", item.Title, gameVersion, loader);
                return;
            }

            // Hauptmod + benötigte Abhängigkeiten auflösen und herunterladen.
            var targets = new List<DownloadTarget>();
            var missing = new List<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await CollectDownloadsAsync(item.ProjectId, gameVersion, loader, visited, targets, missing);

            if (targets.Count == 0)
            {
                StatusMessage = t("Mods.InstallError", t("Mods.UnknownMod"));
                return;
            }

            var downloaded = await DownloadTargetsAsync(targets);
            _installedProjectIds.Add(item.ProjectId);
            item.IsInstalled = true;
            item.HasUpdate = false;
            UpdateInstalledMarks();

            if (missing.Count > 0)
            {
                StatusMessage = t("Mods.InstallWithMissingDeps",
                    item.Title, missing.Count, string.Join(", ", missing));
            }
            else if (downloaded > 1)
            {
                StatusMessage = t("Mods.InstalledWithDeps", item.Title, downloaded);
            }
            else
            {
                StatusMessage = t("Mods.InstallSuccess", item.Title);
            }

            if (ShowInstalled)
                await LoadInstalledAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = t("Mods.InstallError", ex.Message);
        }
        finally
        {
            item.IsInstalling = false;
            _installLock.Release();
        }
    }

    /// <summary>Update für ein installiertes Mod aus der „Installiert“-Liste.</summary>
    [RelayCommand]
    private async Task UpdateModAsync(InstalledModItem? item)
    {
        if (item is null || string.IsNullOrEmpty(item.ProjectId))
            return;
        if (!HasGameVersion)
        {
            StatusMessage = t("Mods.NoVersionSelected");
            return;
        }

        await _installLock.WaitAsync();
        try
        {
            item.IsUpdating = true;
            StatusMessage = t("Mods.Updating");

            var loader = SelectedLoaderOption?.Value ?? "fabric";
            var versions = await _api.GetVersionsAsync(item.ProjectId, GetSelectedVersion(), loader);
            var version = versions.FirstOrDefault();
            if (version is null)
            {
                StatusMessage = t("Mods.InstallNoVersion", item.DisplayName, GetSelectedVersion(), loader);
                return;
            }

            var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault();
            if (file is null)
            {
                StatusMessage = t("Mods.UpdateError", t("Mods.UnknownMod"));
                return;
            }

            var modsDir = _settings.Current.ModsDirectory;
            Directory.CreateDirectory(modsDir);
            var destination = Path.Combine(modsDir, SanitizeFileName(file.Filename));

            if (File.Exists(item.FullPath))
                File.Delete(item.FullPath);
            var oldDisabled = item.FullPath + ".disabled";
            if (File.Exists(oldDisabled))
                File.Delete(oldDisabled);

            await _api.DownloadFileAsync(file.Url, destination);

            // Neu benötigte Abhängigkeiten der aktualisierten Version nachziehen
            // (das Hauptprojekt selbst ist bereits installiert und wird übersprungen).
            var targets = new List<DownloadTarget>();
            var missing = new List<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { item.ProjectId };
            await CollectDownloadsAsync(item.ProjectId, GetSelectedVersion(), loader, visited, targets, missing);
            await DownloadTargetsAsync(targets);

            StatusMessage = missing.Count > 0
                ? t("Mods.UpdateWithMissingDeps", item.DisplayName, missing.Count, string.Join(", ", missing))
                : t("Mods.UpdateSuccess", item.DisplayName);
            await LoadInstalledAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = t("Mods.UpdateError", ex.Message);
        }
        finally
        {
            item.IsUpdating = false;
            _installLock.Release();
        }
    }

    /// <summary>
    /// Update für ein bereits installiertes Mod, ausgelöst über den Modrinth-Bereich
    /// („Update“-Button bei bereits installierten Projekten).
    /// </summary>
    [RelayCommand]
    private async Task UpdateFromModrinthAsync(ModrinthModItem? item)
    {
        if (item is null || string.IsNullOrEmpty(item.ProjectId))
            return;

        var installed = InstalledMods.FirstOrDefault(m =>
            string.Equals(m.ProjectId, item.ProjectId, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(m.ModId) && string.Equals(m.ModId, item.Slug, StringComparison.OrdinalIgnoreCase)));
        if (installed is null)
        {
            StatusMessage = t("Mods.UpdateError", t("Mods.UnknownMod"));
            return;
        }

        item.IsUpdating = true;
        try
        {
            await UpdateModAsync(installed);
        }
        finally
        {
            item.IsUpdating = false;
        }
    }

    [RelayCommand]
    private void UninstallMod(InstalledModItem? item)
    {
        if (item is null)
            return;

        try
        {
            _modService.UninstallFile(item.FullPath);
            var disabledVariant = item.FullPath + ".disabled";
            if (File.Exists(disabledVariant))
                File.Delete(disabledVariant);

            InstalledMods.Remove(item);
            if (!string.IsNullOrEmpty(item.ProjectId))
                _installedProjectIds.Remove(item.ProjectId);
            if (!string.IsNullOrEmpty(item.ModId))
                _installedModIds.Remove(item.ModId);

            UpdateInstalledMarks();
            OnPropertyChanged(nameof(IsInstalledEmpty));
            StatusMessage = t("Mods.Uninstalled", item.DisplayName);
        }
        catch (Exception ex)
        {
            StatusMessage = t("Mods.Error", ex.Message);
        }
    }

    [RelayCommand]
    private void ToggleInstalled(InstalledModItem? item)
    {
        if (item is null)
            return;

        try
        {
            var newPath = _modService.SetEnabledByPath(item.FullPath, !item.IsEnabled);
            item.FullPath = newPath;
            item.IsEnabled = !item.IsEnabled;
            StatusMessage = item.IsEnabled
                ? t("Mods.Enabled", item.DisplayName)
                : t("Mods.DisabledMsg", item.DisplayName);
        }
        catch (Exception ex)
        {
            StatusMessage = t("Mods.Error", ex.Message);
        }
    }

    /// <summary>Eine herunterzuladende Mod-Datei (Hauptmod oder Abhängigkeit).</summary>
    private sealed record DownloadTarget(string ProjectId, string FileName, string Url);

    /// <summary>
    /// Sammelt die Hauptmod und alle benötigten („required“) Abhängigkeiten
    /// inklusive deren Abhängigkeiten. Zyklen-sicher über <paramref name="visited"/>;
    /// bereits installierte Projekte werden übersprungen. Abhängigkeiten ohne
    /// passende Version für MC-Version/Loader landen in <paramref name="missingNames"/>.
    /// </summary>
    private async Task CollectDownloadsAsync(
        string projectId,
        string gameVersion,
        string loader,
        HashSet<string> visited,
        List<DownloadTarget> targets,
        List<string> missingNames)
    {
        if (!visited.Add(projectId))
            return;

        // Bereits installierte Projekte (bzw. deren Abhängigkeiten) überspringen.
        if (_installedProjectIds.Contains(projectId))
            return;

        List<ModrinthVersionDto> versions;
        try
        {
            versions = await _api.GetVersionsAsync(projectId, gameVersion, loader);
        }
        catch
        {
            missingNames.Add(projectId);
            return;
        }

        var version = versions.FirstOrDefault();
        if (version is null)
        {
            missingNames.Add(projectId);
            return;
        }

        var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault();
        if (file is null)
        {
            missingNames.Add(projectId);
            return;
        }

        targets.Add(new DownloadTarget(projectId, file.Filename, file.Url));

        foreach (var dep in version.Dependencies)
        {
            if (dep.DependencyType != "required" || string.IsNullOrWhiteSpace(dep.ProjectId))
                continue;

            await CollectDownloadsAsync(dep.ProjectId, gameVersion, loader, visited, targets, missingNames);
        }
    }

    /// <summary>
    /// Lädt alle gesammelten Dateien in den Mods-Ordner. Bereits vorhandene
    /// Dateien (aktiv oder als .disabled) werden übersprungen. Gibt die Anzahl
    /// neu heruntergeladener Dateien zurück.
    /// </summary>
    private async Task<int> DownloadTargetsAsync(List<DownloadTarget> targets)
    {
        var modsDir = _settings.Current.ModsDirectory;
        Directory.CreateDirectory(modsDir);

        var downloaded = 0;
        foreach (var target in targets)
        {
            var destination = Path.Combine(modsDir, SanitizeFileName(target.FileName));
            if (File.Exists(destination) || File.Exists(destination + ".disabled"))
                continue;

            await _api.DownloadFileAsync(target.Url, destination);
            downloaded++;
            _installedProjectIds.Add(target.ProjectId);
        }

        return downloaded;
    }

    private ModrinthModItem CreateResultItem(ModrinthHit hit)
    {
        var item = new ModrinthModItem
        {
            ProjectId = hit.ProjectId,
            Slug = hit.Slug,
            Title = hit.Title,
            Author = hit.Author,
            Description = hit.Description,
            Downloads = hit.Downloads,
            Follows = hit.Follows,
            Loaders = hit.Categories.Where(IsKnownLoader).ToList(),
            GameVersions = hit.Versions,
            IconUrl = hit.IconUrl,
        };
        ApplyInstalledMark(item);
        _ = item.LoadIconAsync(_api);
        return item;
    }

    /// <summary>Aktualisiert Installiert-/Update-Status aller Modrinth-Ergebnisse.</summary>
    private void UpdateInstalledMarks()
    {
        foreach (var item in BrowseResults)
            ApplyInstalledMark(item);
    }

    /// <summary>
    /// Setzt den „Installiert“- und „Update verfügbar“-Status eines Modrinth-Ergebnisses:
    /// exakte Zuordnung über die Projekt-Id (SHA-1-Erkennung) oder als Fallback über
    /// die ModId der lokalen .jar gegen den Slug des Projekts.
    /// </summary>
    private void ApplyInstalledMark(ModrinthModItem item)
    {
        var byProject = _installedProjectIds.Contains(item.ProjectId);
        var byModId = !string.IsNullOrWhiteSpace(item.Slug) && _installedModIds.Contains(item.Slug);
        item.IsInstalled = byProject || byModId;

        item.HasUpdate = byProject
            && _latestVersionById.TryGetValue(item.ProjectId, out var latest)
            && _installedVersionById.TryGetValue(item.ProjectId, out var installed)
            && !string.Equals(latest, installed, StringComparison.Ordinal);
    }

    private static bool IsKnownLoader(string category) => KnownLoaders.Contains(category);

    private string GetSelectedVersion() => _settings.Current.SelectedVersionId ?? "";

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var result = new string(chars).Trim();
        return result.Length == 0 ? "mod.jar" : result;
    }

    private sealed class PreparedFile
    {
        public required string Path { get; init; }
        public required bool IsEnabled { get; init; }
        public required long Size { get; init; }
        public required DateTime Modified { get; init; }
        public required string ModId { get; init; }
        public required string DisplayName { get; init; }
        public required string Version { get; init; }
        public required string Loader { get; init; }
        public required string Sha1 { get; init; }
    }
}
