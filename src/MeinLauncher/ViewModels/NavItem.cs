using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MeinLauncher.Services;

namespace MeinLauncher.ViewModels;

/// <summary>
/// Ein Eintrag in der Seitenleiste. Der Anzeigetitel ist lokalisiert und wird
/// bei einem Sprachwechsel automatisch aktualisiert; die Navigation erfolgt
/// über den stabilen Schlüssel <see cref="Key"/>.
/// </summary>
public sealed partial class NavItem : ObservableObject
{
    /// <summary>Stabiler Lokalisierungs-Schlüssel (z. B. "Nav.Versions").</summary>
    public string Key { get; }

    public string GlyphData { get; }

    public Func<ViewModelBase> Factory { get; }

    public Geometry Glyph => StreamGeometry.Parse(GlyphData);

    [ObservableProperty]
    public partial string Title { get; set; }

    /// <summary>
    /// Ungelesener Status (z. B. neuer News-Upload): steuert den roten Punkt in der
    /// Seitenleiste und wird lokal als „gesehen“ gespeichert, sobald die Seite geöffnet wird.
    /// </summary>
    [ObservableProperty]
    public partial bool HasUnread { get; set; }

    public NavItem(string key, string glyphData, Func<ViewModelBase> factory)
    {
        Key = key;
        GlyphData = glyphData;
        Factory = factory;
        Title = LocalizationManager.Instance.Get(key);
    }

    public void RefreshTitle() => Title = LocalizationManager.Instance.Get(Key);
}

/// <summary>
/// Pfaddaten für die Navigations-Icons (Material Design Icons, Apache 2.0).
/// </summary>
public static class Icons
{
    public const string Home =
        "M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z";

    public const string Profile =
        "M3 5v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2zm12 4c0 1.66-1.34 3-3 3s-3-1.34-3-3 1.34-3 3-3 3 1.34 3 3zm-9 8c0-2 4-3.1 6-3.1s6 1.1 6 3.1v1H6v-1z";

    public const string Settings =
        "M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z";

    public const string News =
        "M5,3H19C20.1,3 21,3.9 21,5V19C21,20.1 20.1,21 19,21H5C3.9,21 3,20.1 3,19V5C3,3.9 3.9,3 5,3M7.18,15C6.38,15 5.75,15.63 5.75,16.43C5.75,17.22 6.38,17.85 7.18,17.85C7.97,17.85 8.6,17.22 8.6,16.43C8.6,15.63 7.97,15 7.18,15M5.5,12.5C8.75,12.5 11.5,15.25 11.5,18.5H13.5C13.5,14.08 9.92,10.5 5.5,10.5V12.5M5.5,8C11.28,8 16,12.72 16,18.5H18C18,11.08 11.92,5 5.5,5V8Z";

    public const string Play =
        "M8,5V19L19,12Z";

    public const string Close =
        "M19,6.41L17.59,5 12,10.59 6.41,5 5,6.41 10.59,12 5,17.59 6.41,19 12,13.41 17.59,19 19,17.59 13.41,12Z";

    public const string Alert =
        "M11,15H13V17H11V15M11,7H13V13H11V7M12,2C6.47,2 2,6.5 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20Z";
}
