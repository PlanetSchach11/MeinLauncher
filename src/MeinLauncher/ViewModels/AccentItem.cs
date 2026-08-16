using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MeinLauncher.Services;

namespace MeinLauncher.ViewModels;

/// <summary>
/// Ein Auswahl-Farbfeld für die Akzentfarbe (Farbwert + lokalisierter Name).
/// </summary>
public sealed partial class AccentItem : ObservableObject
{
    /// <summary>Schlüssel aus ThemeManager.Accents (z. B. "Green").</summary>
    public string Key { get; }

    public Color Color { get; }

    public SolidColorBrush ColorBrush { get; }

    public string Name => LocalizationManager.Instance.Get("Accent." + Key);

    public LocalizationManager L => LocalizationManager.Instance;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public AccentItem(string key)
    {
        Key = key;
        Color = ThemeManager.Accents[key];
        ColorBrush = new SolidColorBrush(Color);
        LocalizationManager.Instance.LanguageChanged += () => OnPropertyChanged(nameof(Name));
    }
}
