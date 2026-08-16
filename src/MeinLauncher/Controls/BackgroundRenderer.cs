using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using MeinLauncher.Models;

namespace MeinLauncher.Controls;

/// <summary>
/// Zeichnet den dekorativen, animierten Hintergrund (Elemente wie Schachfiguren,
/// Kreise, Quadrate, Dreiecke, Partikel …). Die Konfiguration wird live aus einer
/// geteilten <see cref="BackgroundConfig"/> gelesen – Änderungen im Einstellungs-
/// Editor sind ohne Neustart sichtbar.
///
/// Der Renderer ist bewusst nicht fokussierbar und niemals klickbar
/// (<see cref="IsHitTestVisible"/> = false) und liegt im Fenster immer unterhalb
/// aller UI-Elemente.
/// </summary>
public sealed class BackgroundRenderer : Control
{
    public static readonly StyledProperty<BackgroundConfig?> ConfigProperty =
        AvaloniaProperty.Register<BackgroundRenderer, BackgroundConfig?>(nameof(Config));

    /// <summary>Die live zu zeichnende Konfiguration (geteiltes Objekt, wird mutiert).</summary>
    public BackgroundConfig? Config
    {
        get => GetValue(ConfigProperty);
        set => SetValue(ConfigProperty, value);
    }

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Typeface _glyphTypeface =
        new(new FontFamily("Segoe UI Symbol, Segoe UI Emoji, Segoe UI"));

    private Element[] _elements = [];
    private string _signature = "";

    private SolidColorBrush? _brush;
    private string _brushSignature = "";

    /// <summary>Geometrie-Cache (Dreieck, Raute, Sechseck, Stern) – unabhängig von der Farbe.</summary>
    private readonly Dictionary<string, Geometry> _shapeCache = [];

    /// <summary>Glyphen-Cache (Schachfiguren) – wird bei Farb-/Transparenzänderung geleert.</summary>
    private readonly Dictionary<string, FormattedText> _glyphCache = [];

    public BackgroundRenderer()
    {
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = true;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ConfigProperty)
        {
            _signature = "";
            _elements = [];
            InvalidateVisual();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        // Nichts tun, wenn unsichtbar, deaktiviert oder ohne Elemente – die Uhr
        // bleibt stehen, die Performance wird nicht belastet.
        if (!IsVisible || !IsEffectivelyVisible)
            return;

        var cfg = Config;
        if (cfg is null || !cfg.Enabled || cfg.Kinds.Count == 0)
            return;

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var cfg = Config;
        if (cfg is null || !cfg.Enabled || cfg.Kinds.Count == 0)
            return;

        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
            return;

        EnsureElements(cfg, w, h);
        EnsureBrush(cfg);

        var t = _clock.Elapsed.TotalSeconds;

        foreach (var el in _elements)
        {
            var region = GetRegion(cfg.Placement, el, w, h);
            var (px, py, rot) = ComputePosition(el, cfg, t, region);

            // Rotate um das eigene Zentrum, danach an die Zielposition verschieben.
            var matrix = Matrix.CreateRotation(rot) * Matrix.CreateTranslation(px, py);
            using (context.PushTransform(matrix))
                DrawElement(context, cfg, el);
        }
    }

    // ---------------------------------------------------------------- Elementaufbau

    private void EnsureElements(BackgroundConfig cfg, double w, double h)
    {
        var effective = Math.Max(1, Math.Min(300, (int)Math.Round(cfg.Count * cfg.Density)));
        var kinds = cfg.Kinds;
        var sig = $"{effective}|{string.Join(",", kinds)}|{cfg.Placement}|{cfg.Size:0.#}|{cfg.Spacing:0.##}";
        if (sig == _signature && _elements.Length == effective)
            return;

        _signature = sig;
        _shapeCache.Clear();
        _glyphCache.Clear();

        var rng = new Random(20260816); // fester Seed: beim Regenerieren keine Sprünge bei jeder Änderung
        var placedX = new double[effective];
        var placedY = new double[effective];

        _elements = new Element[effective];
        for (var i = 0; i < effective; i++)
        {
            var kind = kinds[rng.Next(kinds.Count)];
            var size = Math.Max(4, cfg.Size * (0.7 + rng.NextDouble() * 0.6));

            var minDist = size * (1.0 + cfg.Spacing * 1.6);
            var sx = rng.NextDouble();
            var sy = rng.NextDouble();
            for (var attempt = 0; attempt < 40; attempt++)
            {
                var tx = rng.NextDouble();
                var ty = rng.NextDouble();
                var ok = true;
                for (var j = 0; j < i; j++)
                {
                    var dx = (tx - placedX[j]) * w;
                    var dy = (ty - placedY[j]) * h;
                    if (dx * dx + dy * dy < minDist * minDist)
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    sx = tx;
                    sy = ty;
                    break;
                }
            }

            placedX[i] = sx;
            placedY[i] = sy;

            var rotSpeed = cfg.RotationSpeed * (0.4 + rng.NextDouble() * 1.2)
                           * (rng.Next(2) == 0 ? -1 : 1)
                           * Math.PI / 180.0;

            _elements[i] = new Element(
                kind,
                sx,
                sy,
                size,
                rng.NextDouble() * Math.PI * 2,
                DirectionAngle(cfg.Direction, rng),
                rotSpeed);
        }
    }

    private static double DirectionAngle(BackgroundDirection direction, Random rng) => direction switch
    {
        BackgroundDirection.Up => -Math.PI / 2,
        BackgroundDirection.Down => Math.PI / 2,
        BackgroundDirection.Left => Math.PI,
        BackgroundDirection.Right => 0,
        BackgroundDirection.Diagonal => rng.Next(2) == 0 ? Math.PI / 4 : -Math.PI / 4,
        _ => rng.NextDouble() * Math.PI * 2,
    };

    private void EnsureBrush(BackgroundConfig cfg)
    {
        var hex = string.IsNullOrWhiteSpace(cfg.ColorHex) ? "#FFFFFF" : cfg.ColorHex;
        var sig = $"{hex}|{cfg.Opacity:0.###}";
        if (_brush is not null && sig == _brushSignature)
            return;

        _brushSignature = sig;
        _glyphCache.Clear();

        Color color;
        try
        {
            color = Color.Parse(hex);
        }
        catch
        {
            color = Colors.White;
        }

        var alpha = (byte)Math.Round(255 * Math.Clamp(cfg.Opacity, 0, 1));
        _brush = new SolidColorBrush(new Color(alpha, color.R, color.G, color.B));
    }

    // ---------------------------------------------------------------- Bewegung

    private (double X, double Y, double Rotation) ComputePosition(
        Element el, BackgroundConfig cfg, double t, RegionRect region)
    {
        var insetX = Math.Min(region.Width * 0.25, cfg.Spacing * el.Size * 2.5);
        var insetY = Math.Min(region.Height * 0.25, cfg.Spacing * el.Size * 2.5);
        var availW = region.Width - 2 * insetX;
        var availH = region.Height - 2 * insetY;
        var baseX = region.X + insetX + el.X * availW;
        var baseY = region.Y + insetY + el.Y * availH;

        // Grundgeschwindigkeit: Richtungsdrift für Float, Durchzug für Glide/Drift.
        var speed = (0.35 + cfg.Intensity * 0.9) * cfg.Speed * 70 * (el.Size / 24.0);
        var amp = cfg.Intensity * el.Size * (1.0 + el.X * 0.5);

        double px;
        double py;
        var movement = cfg.Animate ? cfg.Movement : BackgroundMovement.Float;

        switch (movement)
        {
            case BackgroundMovement.Glide:
            {
                var dx = Math.Cos(el.BaseAngle);
                var dy = Math.Sin(el.BaseAngle);
                px = baseX + dx * speed * t;
                py = baseY + dy * speed * t;
                break;
            }

            case BackgroundMovement.Drift:
            {
                var angle = el.BaseAngle + Math.Sin(t * 0.5 * cfg.Speed + el.Phase) * 1.3;
                px = baseX + Math.Cos(angle) * speed * t;
                py = baseY + Math.Sin(angle) * speed * t;
                break;
            }

            default: // Float / ohne Animation: sanftes Schweben
            {
                var dx = Math.Cos(el.BaseAngle) * 0.15;
                var dy = Math.Sin(el.BaseAngle) * 0.15;
                px = baseX + dx * speed * t
                     + Math.Sin(t * cfg.Speed * 1.2 + el.Phase) * amp;
                py = baseY + dy * speed * t
                     + Math.Sin(t * cfg.Speed * 0.9 + el.Phase * 1.7) * amp * 0.8;
                break;
            }
        }

        // Innerhalb des Bereichs umlaufen lassen (Gleiten/Drift laufen weiter).
        px = region.X + insetX + Mod(px - (region.X + insetX), availW);
        py = region.Y + insetY + Mod(py - (region.Y + insetY), availH);

        var rot = cfg.Rotate ? el.RotSpeed * t : 0;
        return (px, py, rot);
    }

    private static double Mod(double value, double modulus)
    {
        var m = Math.Abs(modulus);
        var r = value % m;
        return r < 0 ? r + m : r;
    }

    // ---------------------------------------------------------------- Zeichnen

    private void DrawElement(DrawingContext context, BackgroundConfig cfg, Element el)
    {
        var s = el.Size;
        var half = s / 2.0;
        var brush = _brush;

        switch (el.Kind)
        {
            case BackgroundElementKind.Circle:
                context.DrawEllipse(brush, null, new Point(0, 0), half, half);
                break;

            case BackgroundElementKind.Square:
                context.DrawRectangle(brush, null, new Rect(-half, -half, s, s));
                break;

            case BackgroundElementKind.Ring:
            {
                var pen = new Pen(brush, Math.Max(1, s * 0.16));
                context.DrawEllipse(null, pen, new Point(0, 0), half, half);
                break;
            }

            case BackgroundElementKind.Particle:
                context.DrawEllipse(brush, null, new Point(0, 0), half * 0.35, half * 0.35);
                break;

            case BackgroundElementKind.Triangle:
            case BackgroundElementKind.Diamond:
            case BackgroundElementKind.Hexagon:
            case BackgroundElementKind.Star:
                context.DrawGeometry(brush, null, GetShape(el.Kind, s));
                break;

            default: // Schachfiguren (Glyphen)
            {
                var glyph = GetGlyph(el.Kind, s);
                context.DrawText(glyph, new Point(-glyph.Width / 2.0, -glyph.Height / 2.0));
                break;
            }
        }
    }

    private Geometry GetShape(BackgroundElementKind kind, double size)
    {
        var key = $"{kind}|{size:0}";
        if (_shapeCache.TryGetValue(key, out var cached))
            return cached;

        var geometry = BuildShape(kind, size);
        _shapeCache[key] = geometry;
        return geometry;
    }

    private static Geometry BuildShape(BackgroundElementKind kind, double size)
    {
        var h = size / 2.0;
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();

        switch (kind)
        {
            case BackgroundElementKind.Triangle:
                ctx.BeginFigure(new Point(0, -h), true);
                ctx.LineTo(new Point(h, h * 0.7));
                ctx.LineTo(new Point(-h, h * 0.7));
                break;

            case BackgroundElementKind.Diamond:
                ctx.BeginFigure(new Point(0, -h), true);
                ctx.LineTo(new Point(h, 0));
                ctx.LineTo(new Point(0, h));
                ctx.LineTo(new Point(-h, 0));
                break;

            case BackgroundElementKind.Hexagon:
                for (var i = 0; i < 6; i++)
                {
                    var a = Math.PI / 3 * i - Math.PI / 2;
                    var p = new Point(Math.Cos(a) * h, Math.Sin(a) * h);
                    if (i == 0)
                        ctx.BeginFigure(p, true);
                    else
                        ctx.LineTo(p);
                }

                break;

            case BackgroundElementKind.Star:
                for (var i = 0; i < 10; i++)
                {
                    var r = i % 2 == 0 ? h : h * 0.45;
                    var a = Math.PI / 5 * i - Math.PI / 2;
                    var p = new Point(Math.Cos(a) * r, Math.Sin(a) * r);
                    if (i == 0)
                        ctx.BeginFigure(p, true);
                    else
                        ctx.LineTo(p);
                }

                break;
        }

        ctx.EndFigure(true);
        return geometry;
    }

    private FormattedText GetGlyph(BackgroundElementKind kind, double size)
    {
        var key = $"{kind}|{size:0}";
        if (_glyphCache.TryGetValue(key, out var cached))
            return cached;

        var glyphChar = kind switch
        {
            BackgroundElementKind.Pawn => '\u265F',
            BackgroundElementKind.Knight => '\u265E',
            BackgroundElementKind.Bishop => '\u265D',
            BackgroundElementKind.Rook => '\u265C',
            BackgroundElementKind.Queen => '\u265B',
            _ => '\u265A', // King
        };

        var text = new FormattedText(
            glyphChar.ToString(),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            _glyphTypeface,
            size * 1.15,
            _brush);

        _glyphCache[key] = text;
        return text;
    }

    // ---------------------------------------------------------------- Bereich

    private readonly record struct RegionRect(double X, double Y, double Width, double Height);

    /// <summary>
    /// Berechnet den Platzierungsbereich. Für "Corners" wird pro Element eine der
    /// vier Ecken gewählt – dafür wird der RNG-Anteil des Elements als Seed genutzt.
    /// </summary>
    private static RegionRect GetRegion(BackgroundPlacement placement, Element el, double w, double h)
    {
        switch (placement)
        {
            case BackgroundPlacement.Top:
                return new RegionRect(0, 0, w, h * 0.5);
            case BackgroundPlacement.Bottom:
                return new RegionRect(0, h * 0.5, w, h * 0.5);
            case BackgroundPlacement.Left:
                return new RegionRect(0, 0, w * 0.5, h);
            case BackgroundPlacement.Right:
                return new RegionRect(w * 0.5, 0, w * 0.5, h);
            case BackgroundPlacement.Center:
                return new RegionRect(w * 0.15, h * 0.15, w * 0.7, h * 0.7);
            case BackgroundPlacement.Corners:
            {
                var corner = (int)(el.X * 4) % 4; // deterministisch pro Element
                return corner switch
                {
                    0 => new RegionRect(0, 0, w * 0.4, h * 0.4),
                    1 => new RegionRect(w * 0.6, 0, w * 0.4, h * 0.4),
                    2 => new RegionRect(0, h * 0.6, w * 0.4, h * 0.4),
                    _ => new RegionRect(w * 0.6, h * 0.6, w * 0.4, h * 0.4),
                };
            }

            default:
                return new RegionRect(0, 0, w, h);
        }
    }

    private readonly record struct Element(
        BackgroundElementKind Kind,
        double X,
        double Y,
        double Size,
        double Phase,
        double BaseAngle,
        double RotSpeed);
}
