using CommunityToolkit.Mvvm.ComponentModel;
using MeinLauncher.Models;
using MeinLauncher.Services;

namespace MeinLauncher.ViewModels;

/// <summary>
/// Eintrag in der Profil-Auswahl auf der Startseite. Das Standardprofil hat keinen
/// <see cref="Profile"/>-Bezug und zeigt den lokalisierten Namen „Standard“.
/// </summary>
public sealed partial class ProfileItem : ObservableObject
{
    public LauncherProfile? Profile { get; }

    public bool IsDefault => Profile is null;

    /// <summary>Ursprünglicher (nicht lokalisierter) Profilname.</summary>
    public string Name { get; }

    public string Display =>
        Profile is null ? LocalizationManager.Instance.Get("Home.ProfileStandard") : Name;

    /// <summary>True, wenn dieses Profil aktuell aktiv ist (Chip-Hervorhebung).</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public ProfileItem(LauncherProfile? profile)
    {
        Profile = profile;
        Name = profile?.Name ?? "";
        if (Profile is null)
            LocalizationManager.Instance.LanguageChanged += () => OnPropertyChanged(nameof(Display));
    }
}
