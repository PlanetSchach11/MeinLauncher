using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MeinLauncher.Models;
using MeinLauncher.Services;

namespace MeinLauncher.ViewModels;

/// <summary>
/// Anzeige-Eintrag für die Versionsliste (mit Installations-/Auswahl-Status).
/// </summary>
public partial class VersionItem : ObservableObject
{
    public LocalizationManager L => LocalizationManager.Instance;

    public required MinecraftVersionInfo Info { get; init; }

    public string Id => Info.Id;

    public string TypeLabel => Info.TypeLabel;

    public bool IsRelease => Info.IsRelease;

    public string ReleaseDateLabel
    {
        get
        {
            if (DateTimeOffset.TryParse(Info.ReleaseTime, out var dto))
                return dto.ToLocalTime().ToString("dd.MM.yyyy");

            return "unbekannt";
        }
    }

    [ObservableProperty]
    public partial bool IsInstalled { get; set; }

    [ObservableProperty]
    public partial bool IsDefault { get; set; }
}
