using System;
using System.Globalization;
using Avalonia.Data.Converters;
using MeinLauncher.Services;

namespace MeinLauncher.Converters;

/// <summary>
/// Wandelt einen Lokalisierungs-Schlüssel in den aktuell übersetzten Text um.
/// Für feste, nicht-bindbare Stellen (z. B. ComboBox-Einträge in der Versionsliste).
/// </summary>
public sealed class LocConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key)
            return value;

        return key switch
        {
            "Alle" => LocalizationManager.Instance.Get("Versions.FilterAll"),
            "Release" => LocalizationManager.Instance.Get("Versions.Release"),
            "Snapshot" => LocalizationManager.Instance.Get("Versions.Snapshot"),
            _ => LocalizationManager.Instance.Get(key),
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value;
}
