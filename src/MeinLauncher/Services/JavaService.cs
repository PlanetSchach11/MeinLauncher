using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MeinLauncher.Services;

/// <summary>Eine gefundene Java-Installation.</summary>
public sealed record JavaRuntimeInfo(string Path, string Version, string Display);

/// <summary>
/// Durchsucht typische Installationspfade nach Java-Runtimes und ermittelt
/// deren Versionsnummer (über „java -version“).
/// </summary>
public sealed class JavaService
{
    public List<JavaRuntimeInfo> DetectAll(string? preferredPath)
    {
        var results = new List<JavaRuntimeInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!File.Exists(path))
                return;

            if (!seen.Add(path))
                return;

            var version = ReadVersion(path);
            var display = string.IsNullOrEmpty(version)
                ? path
                : $"{version} – {path}";

            results.Add(new JavaRuntimeInfo(path, version, display));
        }

        Add(preferredPath);

        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
            Add(Path.Combine(javaHome, "bin", "java.exe"));

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                continue;

            foreach (var vendor in Directory
                         .EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly)
                         .Where(d => d.Contains("Java", StringComparison.OrdinalIgnoreCase) ||
                                     d.Contains("Adoptium", StringComparison.OrdinalIgnoreCase) ||
                                     d.Contains("Temurin", StringComparison.OrdinalIgnoreCase) ||
                                     d.Contains("OpenJDK", StringComparison.OrdinalIgnoreCase) ||
                                     d.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)))
            {
                Add(Path.Combine(vendor, "bin", "java.exe"));

                foreach (var sub in Directory.EnumerateDirectories(vendor, "*", SearchOption.TopDirectoryOnly))
                    Add(Path.Combine(sub, "bin", "java.exe"));
            }
        }

        return results;
    }

    /// <summary>Liest die Versionsnummer einer java.exe („java -version“) mit Timeout.</summary>
    public static string ReadVersion(string javaExe)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = javaExe,
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null)
                return string.Empty;

            if (!proc.WaitForExit(4000))
            {
                try { proc.Kill(); }
                catch { /* bereits beendet */ }
                return string.Empty;
            }

            var firstLine = proc.StandardError.ReadLine() ?? proc.StandardOutput.ReadLine() ?? string.Empty;

            var quoted = Regex.Match(firstLine, "\"(?<v>[^\"]+)\"");
            if (quoted.Success)
                return quoted.Groups["v"].Value.Trim();

            var text = firstLine.Trim();
            return text.Length > 0 ? text : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
