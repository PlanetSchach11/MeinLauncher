using CommunityToolkit.Mvvm.ComponentModel;
using MeinLauncher.Services;

namespace MeinLauncher.ViewModels;

/// <summary>
/// Ein Auswahleintrag (z. B. Theme, Transparenz, Sprache), dessen Anzeigetext
/// zentral lokalisiert ist und sich bei einem Sprachwechsel automatisch aktualisiert.
/// </summary>
public class LocalizedItem : ObservableObject
{
    /// <summary>Lokalisierungs-Schlüssel (z. B. "Settings.Dark").</summary>
    public string Key { get; }

    public string Text => LocalizationManager.Instance.Get(Key);

    public LocalizedItem(string key)
    {
        Key = key;
        LocalizationManager.Instance.LanguageChanged += () => OnPropertyChanged(nameof(Text));
    }
}
