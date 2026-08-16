using CommunityToolkit.Mvvm.ComponentModel;
using MeinLauncher.Services;

namespace MeinLauncher.ViewModels;

/// <summary>
/// Eine erkannte Java-Installation in der Einstellungs-Liste.
/// </summary>
public sealed partial class JavaRuntimeItem : ObservableObject
{
    public string Path { get; }

    public string Version { get; }

    public string VersionLabel => string.IsNullOrEmpty(Version)
        ? LocalizationManager.Instance.Get("Java.UnknownVersion")
        : Version;

    public string Display => string.IsNullOrEmpty(Version) ? Path : $"{Version} – {Path}";

    /// <summary>true, wenn diese Runtime aktuell als Java-Pfad gewählt ist.</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    public LocalizationManager L => LocalizationManager.Instance;

    public JavaRuntimeItem(string path, string version)
    {
        Path = path;
        Version = version;
    }
}
