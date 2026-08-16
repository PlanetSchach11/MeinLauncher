using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MeinLauncher.Models;

/// <summary>
/// Dekorative Elementtypen für den animierten Hintergrund. Neue Typen lassen sich
/// hier ergänzen (Enum-Wert) – die Zeichen-Logik liegt im <c>BackgroundRenderer</c>,
/// die Anzeige- und Lokalisierungslogik im Einstellungs-Editor.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackgroundElementKind
{
    /// <summary>Schachfigur: Bauer.</summary>
    Pawn,

    /// <summary>Schachfigur: Springer.</summary>
    Knight,

    /// <summary>Schachfigur: Läufer.</summary>
    Bishop,

    /// <summary>Schachfigur: Turm.</summary>
    Rook,

    /// <summary>Schachfigur: Dame.</summary>
    Queen,

    /// <summary>Schachfigur: König.</summary>
    King,

    /// <summary>Gefüllter Kreis.</summary>
    Circle,

    /// <summary>Gefülltes Quadrat.</summary>
    Square,

    /// <summary>Gefülltes Dreieck.</summary>
    Triangle,

    /// <summary>Raute (gedrehtes Quadrat).</summary>
    Diamond,

    /// <summary>Sechseck.</summary>
    Hexagon,

    /// <summary>Ring (Kreislinie).</summary>
    Ring,

    /// <summary>Fünfzackiger Stern.</summary>
    Star,

    /// <summary>Kleiner Partikelpunkt.</summary>
    Particle,
}

/// <summary>Bereich der Fensterfläche, in dem Elemente platziert werden und sich bewegen.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackgroundPlacement
{
    /// <summary>Ganze Fläche.</summary>
    Full,

    /// <summary>Obere Hälfte.</summary>
    Top,

    /// <summary>Untere Hälfte.</summary>
    Bottom,

    /// <summary>Linke Hälfte.</summary>
    Left,

    /// <summary>Rechte Hälfte.</summary>
    Right,

    /// <summary>Zentrierter Bereich in der Mitte.</summary>
    Center,

    /// <summary>Die vier Ecken.</summary>
    Corners,
}

/// <summary>Bewegungstyp der Elemente.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackgroundMovement
{
    /// <summary>Sanftes Auf- und Abschweben.</summary>
    Float,

    /// <summary>Gleichmäßiges Gleiten in eine Richtung (mit Umlauf).</summary>
    Glide,

    /// <summary>Dynamische, zufällig wechselnde Bewegung (Drift).</summary>
    Drift,
}

/// <summary>Hauptbewegungsrichtung.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackgroundDirection
{
    /// <summary>Pro Element zufällig gewählt.</summary>
    Random,

    /// <summary>Aufwärts.</summary>
    Up,

    /// <summary>Abwärts.</summary>
    Down,

    /// <summary>Links.</summary>
    Left,

    /// <summary>Rechts.</summary>
    Right,

    /// <summary>Diagonal.</summary>
    Diagonal,
}

/// <summary>
/// Vollständig serialisierbare Konfiguration des dekorativen Hintergrunds.
/// Wird in <see cref="LauncherSettings"/> gespeichert und von den Renderern
/// (Hauptfenster + Vorschau) live ausgelesen – Änderungen an diesem Objekt
/// werden sofort sichtbar, ohne Neustart.
/// </summary>
public sealed class BackgroundConfig
{
    /// <summary>Gesamtschalter: deaktiviert den kompletten dekorativen Hintergrund.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Aktive Elementtypen (mehrfach wählbar).</summary>
    public List<BackgroundElementKind> Kinds { get; set; } = [BackgroundElementKind.Circle];

    /// <summary>Anzahl der Elemente (1–200).</summary>
    public int Count { get; set; } = 18;

    /// <summary>Dichte 0–1: skaliert die tatsächlich gezeichnete Anzahl (Count × Density).</summary>
    public double Density { get; set; } = 0.6;

    /// <summary>Transparenz 0–1 (0 = unsichtbar, 1 = deckend).</summary>
    public double Opacity { get; set; } = 0.30;

    /// <summary>Basisgröße eines Elements in Pixeln.</summary>
    public double Size { get; set; } = 22;

    /// <summary>Abstand/Verteilung 0–1: Randabstand und Mindestabstand zwischen Elementen.</summary>
    public double Spacing { get; set; } = 0.5;

    /// <summary>Bereich, in dem die Elemente platziert werden und sich bewegen.</summary>
    public BackgroundPlacement Placement { get; set; } = BackgroundPlacement.Full;

    /// <summary>Elementfarbe als Hex-Wert (z. B. "#FFFFFF").</summary>
    public string ColorHex { get; set; } = "#FFFFFF";

    /// <summary>Animation an/aus.</summary>
    public bool Animate { get; set; } = true;

    /// <summary>Geschwindigkeits-Multiplikator (0–5).</summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>Bewegungstyp.</summary>
    public BackgroundMovement Movement { get; set; } = BackgroundMovement.Float;

    /// <summary>Hauptbewegungsrichtung.</summary>
    public BackgroundDirection Direction { get; set; } = BackgroundDirection.Random;

    /// <summary>Rotation an/aus.</summary>
    public bool Rotate { get; set; }

    /// <summary>Rotationsgeschwindigkeit in Grad pro Sekunde (0–360).</summary>
    public double RotationSpeed { get; set; } = 30;

    /// <summary>Animationsintensität 0–1 (Schwung/Amplitude der Bewegung).</summary>
    public double Intensity { get; set; } = 0.45;

    /// <summary>Liefert die Standardkonfiguration (dezente, schwebende Kreise).</summary>
    public static BackgroundConfig CreateDefault() => new();
}
