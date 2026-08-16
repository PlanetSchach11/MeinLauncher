using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeinLauncher.Models;
using MeinLauncher.Services;

namespace MeinLauncher.ViewModels;

/// <summary>
/// Auswählbarer Elementtyp im Hintergrund-Editor (Kästchen-Steuerung).
/// </summary>
public sealed partial class BackgroundKindItem : ObservableObject
{
    public BackgroundElementKind Kind { get; }

    public string Name => LocalizationManager.Instance.Get("Background.Kind." + Kind);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Wird ausgelöst, wenn die Auswahl geändert wird.</summary>
    public event Action? SelectionChanged;

    partial void OnIsSelectedChanged(bool value) => SelectionChanged?.Invoke();

    public BackgroundKindItem(BackgroundElementKind kind, bool isSelected)
    {
        Kind = kind;
        IsSelected = isSelected;
        LocalizationManager.Instance.LanguageChanged += () => OnPropertyChanged(nameof(Name));
    }
}

/// <summary>
/// Farbfeld im Hintergrund-Editor (Farbwert + lokalisierter Name).
/// </summary>
public sealed partial class BackgroundColorItem : ObservableObject
{
    /// <summary>Lokalisierungs-Schlüssel (z. B. "Accent.White").</summary>
    public string Key { get; }

    public Color Color { get; }

    public SolidColorBrush ColorBrush { get; }

    public string Name => LocalizationManager.Instance.Get(Key);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public BackgroundColorItem(string key, Color color)
    {
        Key = key;
        Color = color;
        ColorBrush = new SolidColorBrush(color);
        LocalizationManager.Instance.LanguageChanged += () => OnPropertyChanged(nameof(Name));
    }
}

/// <summary>
/// Editor für den dekorativen Hintergrund. Schreibt alle Änderungen live in die
/// geteilte <see cref="BackgroundConfig"/> (Einstellungen), damit Hauptfenster
/// und Vorschau sofort reagieren. Gespeichert wird über den normalen
/// „Einstellungen speichern"-Knopf.
/// </summary>
public partial class BackgroundSettingsViewModel : ViewModelBase
{
    private readonly LauncherSettings _settings;
    private readonly Action? _backgroundChanged;
    private readonly Action? _backgroundTweaked;

    /// <summary>Die geteilte, live gelesene Konfiguration (für Vorschau-Renderer).</summary>
    public BackgroundConfig Config => _settings.Background;

    public ObservableCollection<BackgroundKindItem> Kinds { get; } = [];

    public ObservableCollection<BackgroundColorItem> ColorOptions { get; } = [];

    // ---------------------------------------------------------------- Observable Werte

    [ObservableProperty]
    public partial bool Enabled { get; set; }

    [ObservableProperty]
    public partial int Count { get; set; }

    [ObservableProperty]
    public partial double Density { get; set; }

    [ObservableProperty]
    public partial double Opacity { get; set; }

    [ObservableProperty]
    public partial double Size { get; set; }

    [ObservableProperty]
    public partial double Spacing { get; set; }

    [ObservableProperty]
    public partial bool Animate { get; set; }

    [ObservableProperty]
    public partial double Speed { get; set; }

    [ObservableProperty]
    public partial bool Rotate { get; set; }

    [ObservableProperty]
    public partial double RotationSpeed { get; set; }

    [ObservableProperty]
    public partial double Intensity { get; set; }

    [ObservableProperty]
    public partial string ImagePath { get; set; } = "";

    [ObservableProperty]
    public partial string ImageName { get; set; } = "";

    // ---------------------------------------------------------------- Labels

    public string CountLabel => $"{Count}";
    public string SizeLabel => $"{Size:0} px";
    public string DensityLabel => $"{Density * 100:0} %";
    public string OpacityLabel => $"{Opacity * 100:0} %";
    public string SpacingLabel => $"{Spacing * 100:0} %";
    public string SpeedLabel => $"{Speed:0.0}×";
    public string RotationSpeedLabel => $"{RotationSpeed:0} °/s";
    public string IntensityLabel => $"{Intensity * 100:0} %";

    // ---------------------------------------------------------------- Live-Schreibzugriffe

    partial void OnEnabledChanged(bool value) { Config.Enabled = value; _backgroundTweaked?.Invoke(); }
    partial void OnCountChanged(int value) { Config.Count = value; OnPropertyChanged(nameof(CountLabel)); _backgroundTweaked?.Invoke(); }
    partial void OnDensityChanged(double value) { Config.Density = value; OnPropertyChanged(nameof(DensityLabel)); _backgroundTweaked?.Invoke(); }
    partial void OnOpacityChanged(double value) { Config.Opacity = value; OnPropertyChanged(nameof(OpacityLabel)); _backgroundTweaked?.Invoke(); }
    partial void OnSizeChanged(double value) { Config.Size = value; OnPropertyChanged(nameof(SizeLabel)); _backgroundTweaked?.Invoke(); }
    partial void OnSpacingChanged(double value) { Config.Spacing = value; OnPropertyChanged(nameof(SpacingLabel)); _backgroundTweaked?.Invoke(); }
    partial void OnAnimateChanged(bool value) { Config.Animate = value; _backgroundTweaked?.Invoke(); }
    partial void OnSpeedChanged(double value) { Config.Speed = value; OnPropertyChanged(nameof(SpeedLabel)); _backgroundTweaked?.Invoke(); }
    partial void OnRotateChanged(bool value) { Config.Rotate = value; _backgroundTweaked?.Invoke(); }
    partial void OnRotationSpeedChanged(double value) { Config.RotationSpeed = value; OnPropertyChanged(nameof(RotationSpeedLabel)); _backgroundTweaked?.Invoke(); }
    partial void OnIntensityChanged(double value) { Config.Intensity = value; OnPropertyChanged(nameof(IntensityLabel)); _backgroundTweaked?.Invoke(); }

    partial void OnImagePathChanged(string value)
    {
        _settings.BackgroundImagePath = value.Trim();
        ImageName = string.IsNullOrWhiteSpace(value)
            ? ""
            : Path.GetFileName(value.Trim());
        _backgroundChanged?.Invoke();
    }

    public BackgroundSettingsViewModel(
        LauncherSettings settings,
        Action? backgroundChanged,
        Action? backgroundTweaked = null)
    {
        _settings = settings;
        _backgroundChanged = backgroundChanged;
        _backgroundTweaked = backgroundTweaked ?? backgroundChanged;

        foreach (BackgroundElementKind kind in Enum.GetValues<BackgroundElementKind>())
            Kinds.Add(new BackgroundKindItem(kind, isSelected: false));

        foreach (var (key, color) in Palette)
            ColorOptions.Add(new BackgroundColorItem(key, color));

        LoadFromConfig();
    }

    private static readonly (string Key, Color Color)[] Palette =
    [
        ("Accent.White", Color.Parse("#FFFFFF")),
        ("Accent.Silver", Color.Parse("#C0C6CF")),
        ("Accent.Green", Color.Parse("#7BC043")),
        ("Accent.Red", Color.Parse("#E5484D")),
        ("Accent.Blue", Color.Parse("#3B82F6")),
        ("Accent.Orange", Color.Parse("#F76B15")),
        ("Accent.Purple", Color.Parse("#8B5CF6")),
        ("Accent.Cyan", Color.Parse("#22D3EE")),
        ("Accent.Yellow", Color.Parse("#F0C000")),
        ("Accent.Pink", Color.Parse("#EC4899")),
        ("Accent.Black", Color.Parse("#14161A")),
    ];

    /// <summary>Übernimmt die gespeicherte Konfiguration in die Oberfläche.</summary>
    public void LoadFromConfig()
    {
        var c = _settings.Background;

        Enabled = c.Enabled;
        Count = c.Count;
        Density = c.Density;
        Opacity = c.Opacity;
        Size = c.Size;
        Spacing = c.Spacing;
        Animate = c.Animate;
        Speed = c.Speed;
        Rotate = c.Rotate;
        RotationSpeed = c.RotationSpeed;
        Intensity = c.Intensity;
        ImagePath = _settings.BackgroundImagePath;
        ImageName = string.IsNullOrWhiteSpace(_settings.BackgroundImagePath)
            ? ""
            : Path.GetFileName(_settings.BackgroundImagePath);

        // Typen: Auswahl passend zur Konfiguration setzen.
        foreach (var item in Kinds)
        {
            item.SelectionChanged -= OnKindSelectionChanged;
            item.IsSelected = c.Kinds.Contains(item.Kind);
            item.SelectionChanged += OnKindSelectionChanged;
        }

        var colorItem = ColorOptions.FirstOrDefault(o =>
            string.Equals(o.Color.ToString(), c.ColorHex, StringComparison.OrdinalIgnoreCase))
            ?? ColorOptions.First();
        foreach (var item in ColorOptions)
            item.IsSelected = item == colorItem;

        // Labels nach der initialen Übernahme aktualisieren.
        OnPropertyChanged(nameof(CountLabel));
        OnPropertyChanged(nameof(SizeLabel));
        OnPropertyChanged(nameof(DensityLabel));
        OnPropertyChanged(nameof(OpacityLabel));
        OnPropertyChanged(nameof(SpacingLabel));
        OnPropertyChanged(nameof(SpeedLabel));
        OnPropertyChanged(nameof(RotationSpeedLabel));
        OnPropertyChanged(nameof(IntensityLabel));
    }

    private void OnKindSelectionChanged()
    {
        Config.Kinds = Kinds.Where(k => k.IsSelected).Select(k => k.Kind).ToList();
        _backgroundTweaked?.Invoke();
    }

    /// <summary>Wählt ein Farbfeld aus und schreibt die Farbe in die Konfiguration.</summary>
    public void SelectColor(BackgroundColorItem? item)
    {
        if (item is null)
            return;

        foreach (var color in ColorOptions)
            color.IsSelected = color == item;

        Config.ColorHex = item.Color.ToString();
        _backgroundTweaked?.Invoke();
    }

    /// <summary>Stellt den Standard-Hintergrund wieder her (dekorative Elemente).</summary>
    [RelayCommand]
    private void ResetDefaults()
    {
        var d = BackgroundConfig.CreateDefault();
        var c = _settings.Background;

        c.Enabled = d.Enabled;
        c.Kinds = new System.Collections.Generic.List<BackgroundElementKind>(d.Kinds);
        c.Count = d.Count;
        c.Density = d.Density;
        c.Opacity = d.Opacity;
        c.Size = d.Size;
        c.Spacing = d.Spacing;
        c.Placement = d.Placement;
        c.ColorHex = d.ColorHex;
        c.Animate = d.Animate;
        c.Speed = d.Speed;
        c.Movement = d.Movement;
        c.Direction = d.Direction;
        c.Rotate = d.Rotate;
        c.RotationSpeed = d.RotationSpeed;
        c.Intensity = d.Intensity;

        LoadFromConfig();
        _backgroundTweaked?.Invoke();
    }
}
