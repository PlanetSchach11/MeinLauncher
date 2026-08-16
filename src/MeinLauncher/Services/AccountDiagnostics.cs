using System;
using System.IO;

namespace MeinLauncher.Services;

/// <summary>
/// Diagnose-Log für den Microsoft-Konto- und Session-Zustand.
///
/// Schreibt ausschließlich Zustandsinformationen (Schritte, Erfolgs-Flags, Benutzername,
/// Vorhandensein der XUID) – NIE Tokens, Refresh-Tokens, Codes oder andere Geheimnisse.
/// Ziel: Den Ablauf „Login → Speichern → Wiederherstellen → Spielen“ lückenlos nachvollziehen.
/// </summary>
public static class AccountDiagnostics
{
    private const long MaxLogBytes = 2 * 1024 * 1024;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MeinLauncher",
        "account.log");

    private static readonly object Sync = new();

    /// <summary>Schreibt eine Zeile in das Diagnose-Log (best effort, wirft nie).</summary>
    public static void Log(string message)
    {
        try
        {
            lock (Sync)
            {
                var directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                // Log am Anfang kappen, damit es nicht unbegrenzt wächst.
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxLogBytes)
                    File.WriteAllText(LogPath, "");

                File.AppendAllText(
                    LogPath,
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnose darf den Launcher niemals stören.
        }
    }

    /// <summary>Leert das Diagnose-Log (z. B. vor einem neuen Testlauf).</summary>
    public static void Clear()
    {
        try
        {
            lock (Sync)
            {
                if (File.Exists(LogPath))
                    File.Delete(LogPath);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}
