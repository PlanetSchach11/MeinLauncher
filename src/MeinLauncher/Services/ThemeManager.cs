using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using MeinLauncher.Models;

namespace MeinLauncher.Services;

/// <summary>
/// Wendet Theme (Dunkel/Hell), Akzentfarbe und Transparenz auf die gesamte
/// Anwendung an. Alle UI-Farben laufen über DynamicResource, dadurch werden
/// Änderungen sofort sichtbar, ohne die Views neu aufbauen zu müssen.
/// </summary>
public static class ThemeManager
{
    /// <summary>Verfügbare Akzentfarben (Schlüssel wird in den Einstellungen gespeichert).</summary>
    public static readonly IReadOnlyDictionary<string, Color> Accents = new Dictionary<string, Color>
    {
        ["Green"] = Color.Parse("#7BC043"),
        ["Red"] = Color.Parse("#E5484D"),
        ["Blue"] = Color.Parse("#3B82F6"),
        ["Orange"] = Color.Parse("#F76B15"),
        ["Purple"] = Color.Parse("#8B5CF6"),
        ["Cyan"] = Color.Parse("#22D3EE"),
        ["Yellow"] = Color.Parse("#F0C000"),
        ["White"] = Color.Parse("#E8EAED"),
        ["Black"] = Color.Parse("#1C1E22"),
        ["Pink"] = Color.Parse("#EC4899"),
    };

    /// <summary>Die zuletzt angewendeten Einstellungen (für Fenster-Transparenz).</summary>
    public static LauncherSettings? Current { get; private set; }

    /// <summary>Wird nach jedem Neu-Anwenden des Themes ausgelöst (z. B. fürs Fenster).</summary>
    public static event Action? Applied;

    public static void Apply(LauncherSettings settings)
    {
        Current = settings;
        var app = Application.Current;
        if (app is null)
            return;

        var dark = !string.Equals(settings.Theme, "Light", StringComparison.OrdinalIgnoreCase);
        app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;

        var accent = Accents.TryGetValue(settings.Accent, out var c) ? c : Accents["Green"];

        var alpha = settings.Transparency switch
        {
            "Strong" => 0.72,
            "Light" => 0.88,
            _ => 1.0,
        };

        var bgWindow = dark ? Color.Parse("#14161A") : Color.Parse("#F2F4F7");
        var bgSidebar = dark ? Color.Parse("#0F1114") : Color.Parse("#E9ECEF");
        var bgCard = dark ? Color.Parse("#1B1E23") : Color.Parse("#FFFFFF");
        var bgCardHover = dark ? Color.Parse("#242830") : Color.Parse("#E2E6EA");
        var bgInput = dark ? Color.Parse("#12151A") : Color.Parse("#FFFFFF");
        var borderSubtle = dark ? Color.Parse("#2A2E37") : Color.Parse("#D5DAE1");
        var borderStrong = dark ? Color.Parse("#3A404C") : Color.Parse("#B9C0CA");
        var textPrimary = dark ? Color.Parse("#E8EAED") : Color.Parse("#1B1F24");
        var textSecondary = dark ? Color.Parse("#9AA3AF") : Color.Parse("#5A6472");

        // Akzentfamilie aus der gewählten Farbe ableiten.
        var accentHover = Lighten(accent, 0.08);
        var accentPressed = Darken(accent, 0.10);
        var accentSoft = Mix(dark ? bgCard : Colors.White, accent, dark ? 0.80 : 0.86);
        var textOnAccent = Contrast(accent);

        var warning = Color.Parse("#F0A340");
        var warningSoft = dark ? Color.Parse("#33291A") : Color.Parse("#FFF3E0");
        var danger = Color.Parse("#E5484D");
        var dangerSoft = dark ? Color.Parse("#332020") : Color.Parse("#FDE8E8");
        var success = Color.Parse("#46A758");

        // Hervorhebung des aktiven Menüpunkts in der Seitenleiste. Bei sehr dunkler
        // Akzentfarbe (nur "Schwarz", Luminanz < 0.05) wäre der Akzent auf dunklem
        // Grund unlesbar – dann automatisch auf einen kontrastreichen Wert wechseln.
        // Alle anderen Akzentfarben bleiben unverändert.
        var sidebarSelectedBg = accentSoft;
        var sidebarSelectedFg = accent;
        if (Luminance(accent) < 0.05)
        {
            sidebarSelectedBg = Mix(Colors.White, accent, 0.80);
            sidebarSelectedFg = Colors.White;
        }

        void SetBrush(string key, Color color) => app.Resources[key] = new SolidColorBrush(color);

        SetBrush("BgWindow", WithAlpha(bgWindow, alpha));
        SetBrush("BgSidebar", WithAlpha(bgSidebar, alpha));
        SetBrush("BgCard", WithAlpha(bgCard, Math.Min(1, alpha + 0.06)));
        SetBrush("BgCardHover", WithAlpha(bgCardHover, Math.Min(1, alpha + 0.06)));
        SetBrush("BgInput", WithAlpha(bgInput, Math.Min(1, alpha + 0.08)));
        SetBrush("BorderSubtle", borderSubtle);
        SetBrush("BorderStrong", borderStrong);
        SetBrush("TextPrimary", textPrimary);
        SetBrush("TextSecondary", textSecondary);
        SetBrush("Accent", accent);
        SetBrush("AccentHover", accentHover);
        SetBrush("AccentPressed", accentPressed);
        SetBrush("AccentSoft", accentSoft);
        SetBrush("TextOnAccent", textOnAccent);
        SetBrush("SidebarSelectedBg", sidebarSelectedBg);
        SetBrush("SidebarSelectedFg", sidebarSelectedFg);
        SetBrush("Danger", danger);
        SetBrush("DangerSoft", dangerSoft);
        SetBrush("Warning", warning);
        SetBrush("WarningSoft", warningSoft);
        SetBrush("Success", success);

        // Akzentfarben für die Fluent-Basissteuerelemente (Slider, ProgressBar, ToggleSwitch …)
        app.Resources["SystemAccentColor"] = accent;
        app.Resources["SystemAccentColorDark1"] = Darken(accent, 0.15);
        app.Resources["SystemAccentColorDark2"] = Darken(accent, 0.25);
        app.Resources["SystemAccentColorDark3"] = Darken(accent, 0.35);
        app.Resources["SystemAccentColorLight1"] = Lighten(accent, 0.15);
        app.Resources["SystemAccentColorLight2"] = Lighten(accent, 0.30);
        app.Resources["SystemAccentColorLight3"] = Lighten(accent, 0.45);

        Applied?.Invoke();
    }

    /// <summary>
    /// Wendet die konfigurierte Transparenz auf ein Fenster an (AcrylicBlur/Blur).
    /// Nur sinnvoll, wenn der Fensterhintergrund nicht vollständig deckend ist.
    /// </summary>
    public static void ApplyWindow(Window window)
    {
        var s = Current;
        if (s is null)
            return;

        window.TransparencyLevelHint = s.Transparency is "Light" or "Strong"
            ? [WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent]
            : [];
    }

    private static Color WithAlpha(Color c, double alpha)
    {
        var a = (byte)Math.Clamp(Math.Round(255 * alpha), 0, 255);
        return new Color(a, c.R, c.G, c.B);
    }

    private static Color Lighten(Color c, double factor)
        => Mix(Colors.White, c, 1.0 - factor);

    private static Color Darken(Color c, double factor)
        => Mix(Colors.Black, c, 1.0 - factor);

    private static Color Mix(Color from, Color to, double weight)
    {
        // weight 1 = nur "to", 0 = nur "from"
        byte MixChannel(byte a, byte b) => (byte)Math.Round(a * (1 - weight) + b * weight);

        return new Color(
            (byte)Math.Round(from.A * (1 - weight) + to.A * weight),
            MixChannel(from.R, to.R),
            MixChannel(from.G, to.G),
            MixChannel(from.B, to.B));
    }

    /// <summary>Textfarbe mit ausreichendem Kontrast zur Akzentfarbe.</summary>
    private static Color Contrast(Color accent)
        => Luminance(accent) > 0.45 ? Color.Parse("#10151B") : Colors.White;

    /// <summary>Relative Luminanz nach Rec. 709 (0 = schwarz, 1 = weiß).</summary>
    private static double Luminance(Color c)
    {
        double L(double v) => v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        return 0.2126 * L(c.R / 255.0) + 0.7152 * L(c.G / 255.0) + 0.0722 * L(c.B / 255.0);
    }
}
