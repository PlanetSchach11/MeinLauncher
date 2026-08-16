using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeinLauncher.Models;
using MeinLauncher.Services;

namespace MeinLauncher.ViewModels;

/// <summary>
/// Versionsseite: Liste aller Minecraft-Versionen, suchen/filtern, installieren.
/// </summary>
public partial class VersionsViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly MojangVersionService _versionService;
    private readonly ProfileService _profileService;

    private List<MinecraftVersionInfo> _all = [];

    public ObservableCollection<VersionItem> Versions { get; } = [];

    public List<string> Filters { get; } = ["Alle", "Release", "Snapshot"];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Filter { get; set; } = "Alle";

    [ObservableProperty]
    public partial VersionItem? SelectedVersion { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsInstalling { get; set; }

    [ObservableProperty]
    public partial double InstallProgress { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    /// <summary>True, solange geladen oder installiert wird.</summary>
    public bool IsBusy => IsLoading || IsInstalling;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsBusy));

    partial void OnIsInstallingChanged(bool value) => OnPropertyChanged(nameof(IsBusy));

    public VersionsViewModel(SettingsService settings, MojangVersionService versionService, ProfileService profileService)
    {
        _settings = settings;
        _versionService = versionService;
        _profileService = profileService;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        StatusMessage = t("Versions.Loading");
        try
        {
            var list = await _versionService.GetVersionsAsync();
            _all = list.OrderByDescending(v => v.ReleaseTime).ToList();
            ApplyFilter();
            StatusMessage = _all.Count == 0
                ? t("Versions.NoneFound")
                : t("Versions.Loaded", _all.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = t("Versions.LoadError", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = SearchText?.Trim() ?? string.Empty;
        var selectedId = SelectedVersion?.Id;

        Versions.Clear();

        foreach (var info in _all)
        {
            if (query.Length > 0 && !info.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;

            if (Filter == "Release" && !info.IsRelease)
                continue;

            if (Filter == "Snapshot" && info.IsRelease)
                continue;

            Versions.Add(new VersionItem
            {
                Info = info,
                IsInstalled = _versionService.IsInstalled(info, _settings.Current.VersionsDirectory),
                IsDefault = info.Id == _settings.Current.SelectedVersionId,
            });
        }

        if (selectedId is not null)
            SelectedVersion = Versions.FirstOrDefault(v => v.Id == selectedId);

        if (Versions.Count > 0 && SelectedVersion is null)
            SelectedVersion = Versions.FirstOrDefault(v => v.Id == _settings.Current.SelectedVersionId);

        if (Versions.Count > 0 && SelectedVersion is null)
            SelectedVersion = Versions[0];
    }

    partial void OnSelectedVersionChanged(VersionItem? value)
    {
        if (value is null)
            return;

        var settings = _settings.Current;
        if (settings.SelectedVersionId != value.Id)
        {
            // Write-through: globale Auswahl + aktives Profil.
            _profileService.SyncVersion(value.Id);
            _settings.Save();
        }

        foreach (var item in Versions)
            item.IsDefault = item.Id == value.Id;
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (SelectedVersion is not { } version)
        {
            StatusMessage = t("Versions.SelectFirst");
            return;
        }

        if (IsInstalling)
            return;

        IsInstalling = true;
        InstallProgress = 0;
        try
        {
            var progress = new Progress<double>(p => InstallProgress = p);
            var status = new Progress<string>(m => StatusMessage = m);
            await _versionService.DownloadVersionAsync(
                version.Info,
                _settings.Current.VersionsDirectory,
                progress,
                status);

            version.IsInstalled = true;
            StatusMessage = t("Versions.InstallSuccess", version.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = t("Versions.InstallFailed", ex.Message);
        }
        finally
        {
            IsInstalling = false;
        }
    }
}
