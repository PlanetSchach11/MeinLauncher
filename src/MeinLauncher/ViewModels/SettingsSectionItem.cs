using CommunityToolkit.Mvvm.ComponentModel;
using MeinLauncher.Services;

namespace MeinLauncher.ViewModels;

/// <summary>
/// Eine Einstellungs-Kategorie (Design, Oberfläche, Minecraft, Mods, Konto,
/// Allgemein). Titel und Untertitel sind lokalisiert und aktualisieren sich
/// bei einem Sprachwechsel automatisch.
/// </summary>
public sealed class SettingsSectionItem : ObservableObject
{
    /// <summary>Stabiler Schlüssel (z. B. "design").</summary>
    public string Key { get; }

    /// <summary>Anzeige-Emoji auf der Kategorie-Karte.</summary>
    public string Emoji { get; }

    public string TitleKey { get; }

    public string SubtitleKey { get; }

    public string Title => LocalizationManager.Instance.Get(TitleKey);

    public string Subtitle => LocalizationManager.Instance.Get(SubtitleKey);

    public SettingsSectionItem(string key, string emoji, string titleKey, string subtitleKey)
    {
        Key = key;
        Emoji = emoji;
        TitleKey = titleKey;
        SubtitleKey = subtitleKey;

        LocalizationManager.Instance.LanguageChanged += () =>
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Subtitle));
        };
    }
}
