using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using MeinLauncher.Models;

namespace MeinLauncher.Services;

/// <summary>
/// Sehr dezenter UI-Klick-Sound (kurzer, leiser „Click“). Das WAV wird einmalig
/// programmatisch erzeugt und über WinMM asynchron abgespielt – die UI wird nie
/// blockiert. An/Aus und Lautstärke kommen aus den <see cref="LauncherSettings"/>.
/// Falls Audio nicht verfügbar ist, passiert schlicht nichts (kein Absturz).
/// </summary>
public sealed class UISoundService
{
    public static UISoundService Instance { get; } = new();

    private LauncherSettings? _settings;
    private readonly Stopwatch _lastPlay = Stopwatch.StartNew();
    private string? _wavPath;
    private int _wavVolume = -1;

    /// <summary>
    /// Verhindert Sound-Spam: Mehrere auslösende Ereignisse pro Klick
    /// (Button-Click + Tapped) führen zu genau einem Sound.
    /// </summary>
    private const long DebounceMs = 45;

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);

    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndFileName = 0x00020000;

    private UISoundService()
    {
    }

    /// <summary>Verdrahtet das Live-Einstellungsobjekt (wird bei jeder Änderung ausgelesen).</summary>
    public void Configure(LauncherSettings settings) => _settings = settings;

    /// <summary>Spielt den Klick-Sound, falls aktiviert (asynchron, nie blockierend).</summary>
    public void Play()
    {
        try
        {
            var settings = _settings;
            if (settings is null || !settings.SoundEnabled || settings.SoundVolume <= 0)
                return;

            // Debounce gegen mehrfaches Abspielen innerhalb eines einzigen Klicks.
            if (_lastPlay.ElapsedMilliseconds < DebounceMs)
                return;

            EnsureWav(settings.SoundVolume);
            if (_wavPath is null)
                return;

            PlaySound(_wavPath, IntPtr.Zero, SndAsync | SndNoDefault | SndFileName);
            _lastPlay.Restart();
        }
        catch
        {
            // Audio darf die UI nie zu Fall bringen.
        }
    }

    /// <summary>
    /// Spielt den Klick-Sound unabhängig vom An/Aus-Zustand (für den Toggle selbst,
    /// damit auch das Einschalten einen Klick macht). Respektiert Lautstärke + Debounce.
    /// </summary>
    public void PlayOnce()
    {
        try
        {
            var settings = _settings;
            if (settings is null || settings.SoundVolume <= 0)
                return;

            if (_lastPlay.ElapsedMilliseconds < DebounceMs)
                return;

            EnsureWav(settings.SoundVolume);
            if (_wavPath is null)
                return;

            PlaySound(_wavPath, IntPtr.Zero, SndAsync | SndNoDefault | SndFileName);
            _lastPlay.Restart();
        }
        catch
        {
            // Audio darf die UI nie zu Fall bringen.
        }
    }

    private void EnsureWav(int volume)
    {
        if (_wavPath is not null && _wavVolume == volume && File.Exists(_wavPath))
            return;

        var dir = Path.Combine(Path.GetTempPath(), "KulkaClient");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ui_click.wav");

        WriteClickWav(path, volume);

        _wavPath = path;
        _wavVolume = volume;
    }

    /// <summary>
    /// Erzeugt ein winziges WAV (PCM, mono, 16 Bit, ~34 ms): zwei abklingende
    /// Sinus-Partiale (1,15 kHz + 2,4 kHz) mit Attack-Rampe – ein kurzer, weicher
    /// „Click“. Die Amplitude skaliert mit der Lautstärke-Einstellung.
    /// </summary>
    private static void WriteClickWav(string path, int volume)
    {
        const int sampleRate = 44100;
        const int durationMs = 34;
        const int sampleCount = sampleRate * durationMs / 1000;
        const int dataSize = sampleCount * 2;
        const int headerSize = 44;

        using var ms = new MemoryStream(headerSize + dataSize);
        using var w = new BinaryWriter(ms);

        // RIFF/WAVE-Header
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataSize);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);             // Größe des fmt-Chunks
        w.Write((short)1);       // PCM
        w.Write((short)1);       // Mono
        w.Write(sampleRate);
        w.Write(sampleRate * 2); // Byte-Rate
        w.Write((short)2);       // Block-Align
        w.Write((short)16);      // Bits pro Sample
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataSize);

        // Sehr leise: 50 % Aussteuerung bei voller Lautstärke, darunter skaliert.
        var amp = Math.Clamp(volume, 0, 100) / 100.0 * 0.5;

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (double)sampleRate;

            var s = Math.Sin(2 * Math.PI * 1150 * t) * Math.Exp(-t / 0.009)
                  + 0.4 * Math.Sin(2 * Math.PI * 2400 * t) * Math.Exp(-t / 0.0045);

            var attack = Math.Min(1.0, t / 0.0015);
            var sample = (short)Math.Clamp(s * attack * amp * short.MaxValue, short.MinValue, short.MaxValue);

            w.Write(sample);
        }

        w.Flush();
        File.WriteAllBytes(path, ms.ToArray());
    }
}
