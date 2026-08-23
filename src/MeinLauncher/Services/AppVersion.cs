using System;
using System.Reflection;

namespace MeinLauncher.Services;

/// <summary>
/// Zentrale Versionsquelle. Liest den Wert aus dem .csproj (&lt;Version&gt;),
/// der über AssemblyInformationalVersionAttribute zur Laufzeit verfügbar ist.
/// </summary>
internal static class AppVersion
{
    /// <summary>Aktuelle Version, z.B. "0.2.0".</summary>
    public static string Current { get; } = ReadVersion();

    /// <summary>User-Agent-Fragment, z.B. "KulkaClient/0.2.0".</summary>
    public static string UserAgent { get; } = $"KulkaClient/{Current}";

    private static string ReadVersion()
    {
        var attr = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var version = attr?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(version))
            return "0.0.0";

        // "0.2.0+abc123" → "0.2.0"
        var plus = version.IndexOf('+');
        return plus > 0 ? version[..plus] : version;
    }
}
