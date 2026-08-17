using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MeinLauncher.Models;

namespace MeinLauncher.Services;

/// <summary>Ergebnis eines Startversuchs – Meldung über Lokalisierungs-Schlüssel + Platzhalter.</summary>
public sealed record GameLaunchResult(bool Success, string MessageKey, object[] Args);

/// <summary>
/// Startet Minecraft: Java Edition direkt über die installierte Java-Runtime –
/// ohne den offiziellen Launcher und ohne dessen gespeicherte Anmeldedaten.
///
/// Es werden ausschließlich die eigenen Einstellungen und die selbst angemeldete
/// Microsoft-Session verwendet. Die Versionsdateien (jar/json) kommen aus dem
/// Kulka Client-Spielverzeichnis; Libraries, Assets und Java-Runtime werden bei
/// Bedarf aus der vorhandenen offiziellen Installation (.minecraft) ergänzt.
/// </summary>
public sealed class GameLauncherService
{
    private const string LauncherName = "kulkaclient";
    private const string LauncherVersion = "0.1.0";

    /// <summary>App-Familienname der Store-Version des offiziellen Launchers (nur Pfad-Auflösung).</summary>
    private const string StoreFamily = "Microsoft.4297127D64EC6_8wekyb3d8bbwe";

    private readonly MicrosoftAccountService _accountService;

    public GameLauncherService(MicrosoftAccountService accountService)
        => _accountService = accountService;

    /// <summary>
    /// Startet die gewählte Version. Liefert niemals eine Exception – Fehler werden
    /// als Ergebnis mit passender Lokalisierungs-Meldung zurückgegeben.
    /// </summary>
    public async Task<GameLaunchResult> LaunchAsync(LauncherSettings settings, CancellationToken cancellationToken = default)
    {
        var versionId = settings.SelectedVersionId.Trim();
        if (string.IsNullOrWhiteSpace(versionId))
            return Fail("Home.NoVersionSelected");

        var install = ResolveInstallation(settings, versionId);
        if (install is null)
            return Fail("Home.VersionNotInstalled", versionId);

        var session = await _accountService.RestoreAsync(settings.MicrosoftClientId);
        if (session is null)
        {
            AccountDiagnostics.Log("LaunchAsync: Keine Session vorhanden – Start abgebrochen (Home.NotSignedIn).");
            return Fail("Home.NotSignedIn");
        }

        AccountDiagnostics.Log(
            $"LaunchAsync: Session für den Start vorhanden ({session.MinecraftUsername}, " +
            $"Xuid: {(string.IsNullOrEmpty(session.Xuid) ? "leer" : "vorhanden")}).");

        var versionJson = LoadVersionJson(install.VersionDirectory, versionId);
        if (versionJson is null || string.IsNullOrWhiteSpace(versionJson.MainClass))
            return Fail("Home.VersionDataError", versionId);

        var javaPath = ResolveJavaPath(settings.JavaPath, versionJson.JavaVersion?.MajorVersion ?? 0);
        if (javaPath is null)
            return Fail("Home.JavaNotFound", (versionJson.JavaVersion?.MajorVersion ?? 0) > 0
                ? versionJson.JavaVersion!.MajorVersion.ToString()
                : "?");

        var nativesDirectory = PrepareNativesDirectory(install.GameDirectory, versionId);

        var classpath = BuildClasspath(install, versionId, versionJson, out var missingLibraries);
        if (missingLibraries > 0)
            return Fail("Home.MissingLibraries", versionId, missingLibraries);

        var commandLine = BuildCommandLine(
            settings, install, versionId, versionJson, session, javaPath, nativesDirectory, classpath);

        try
        {
            var javaw = Path.Combine(Path.GetDirectoryName(javaPath) ?? "", "javaw.exe");
            var executable = File.Exists(javaw) ? javaw : javaPath;

            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = commandLine,
                WorkingDirectory = install.GameDirectory,
                UseShellExecute = false,
            };

            if (Process.Start(psi) is null)
                return Fail("Home.GameLaunchFailed", "Prozess konnte nicht gestartet werden.");

            return new GameLaunchResult(true, "Home.GameStarted", [versionId]);
        }
        catch (Exception ex)
        {
            return Fail("Home.GameLaunchFailed", ex.Message);
        }
    }

    private static GameLaunchResult Fail(string messageKey, params object[] args)
        => new(false, messageKey, args);

    // ------------------------------------------------------------ Installation

    private sealed record GameInstallation(
        string GameDirectory,
        string VersionDirectory,
        string LibrariesDirectory,
        string AssetsDirectory);

    /// <summary>
    /// Sucht die Versionsdateien zuerst im Kulka Client-Spielverzeichnis und
    /// danach in der offiziellen Installation (%APPDATA%\.minecraft).
    /// Libraries/Assets werden pro Ordner aufgelöst (Kulka Client → .minecraft).
    /// Als Spielverzeichnis (Mods, Logs, Welten) dient das Instanz-Verzeichnis
    /// des aktiven Profils – Versionen/Libraries/Assets bleiben global.
    /// </summary>
    private static GameInstallation? ResolveInstallation(LauncherSettings settings, string versionId)
    {
        var root = settings.GameDirectory;
        var ownVersion = Path.Combine(root, "versions", versionId);
        var official = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

        var versionDirectory = File.Exists(Path.Combine(ownVersion, versionId + ".json"))
            ? ownVersion
            : Directory.Exists(Path.Combine(official, "versions", versionId))
                ? Path.Combine(official, "versions", versionId)
                : null;

        if (versionDirectory is null)
            return null;

        // Libraries/Assets aus dem Spielverzeichnis, falls vorhanden – sonst aus .minecraft.
        var rootForAssets = Directory.Exists(Path.Combine(root, "libraries"))
            ? root
            : Directory.Exists(Path.Combine(official, "libraries"))
                ? official
                : root;

        var librariesDirectory = Directory.Exists(Path.Combine(rootForAssets, "libraries"))
            ? Path.Combine(rootForAssets, "libraries")
            : Path.Combine(root, "libraries");

        var assetsDirectory = Directory.Exists(Path.Combine(root, "assets"))
            ? Path.Combine(root, "assets")
            : Path.Combine(official, "assets");

        return new GameInstallation(settings.InstanceDirectory, versionDirectory, librariesDirectory, assetsDirectory);
    }

    private static VersionJson? LoadVersionJson(string versionDirectory, string versionId)
    {
        try
        {
            var path = Path.Combine(versionDirectory, versionId + ".json");
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<VersionJson>(json);
        }
        catch
        {
            return null;
        }
    }

    // ------------------------------------------------------------ Java

    /// <summary>
    /// Sucht eine java.exe: zuerst den bevorzugten Pfad aus den Einstellungen,
    /// danach die Microsoft-Runtimes des offiziellen Launchers. Die benötigte
    /// Hauptversion (z. B. 25) wird bevorzugt, sonst eine beliebige Runtime.
    /// </summary>
    private static string? ResolveJavaPath(string? preferredPath, int requiredMajor)
    {
        if (!string.IsNullOrWhiteSpace(preferredPath) && File.Exists(preferredPath))
        {
            if (requiredMajor <= 0 || ParseMajor(JavaService.ReadVersion(preferredPath)) == requiredMajor)
                return preferredPath;
        }

        var candidates = new List<string>();

        var roots = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages", StoreFamily, "LocalCache", "Local", "runtime"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Mojang", "runtime"),
        };

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            // <root>/<name>/windows-x64/<runtime>/bin/java.exe
            foreach (var vendor in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
            foreach (var platform in Directory.EnumerateDirectories(vendor, "*", SearchOption.TopDirectoryOnly))
            foreach (var runtime in Directory.EnumerateDirectories(platform, "*", SearchOption.TopDirectoryOnly))
            {
                var java = Path.Combine(runtime, "bin", "java.exe");
                if (File.Exists(java))
                    candidates.Add(java);
            }
        }

        if (requiredMajor > 0)
        {
            var matching = candidates.FirstOrDefault(c => ParseMajor(JavaService.ReadVersion(c)) == requiredMajor);
            if (matching is not null)
                return matching;
        }

        return candidates.Count > 0 ? candidates[0] : null;
    }

    /// <summary>Ermittelt die Hauptversion aus einer Versionszeichenkette ("25.0.1" → 25, "1.8.0" → 8).</summary>
    private static int ParseMajor(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return 0;

        var text = version.Trim();
        if (text.StartsWith("1.", StringComparison.Ordinal))
        {
            var legacy = text.Split('.');
            if (legacy.Length >= 2 && int.TryParse(legacy[1], out var major))
                return major;
            return 0;
        }

        var first = text.Split('.', ' ', '+', '-')[0];
        return int.TryParse(first, out var value) ? value : 0;
    }

    // ------------------------------------------------------------ Classpath & Natives

    private static string BuildClasspath(
        GameInstallation install, string versionId, VersionJson versionJson, out int missingLibraries)
    {
        var paths = new List<string>();
        missingLibraries = 0;

        var clientJar = Path.Combine(install.VersionDirectory, versionId + ".jar");
        if (File.Exists(clientJar))
            paths.Add(clientJar);
        else
            missingLibraries++;

        foreach (var library in versionJson.Libraries)
        {
            if (!RulesAllow(library.Rules))
                continue;

            var downloads = library.Downloads;
            if (downloads?.Artifact is { } artifact && !string.IsNullOrEmpty(artifact.Path))
            {
                var path = Path.Combine(install.LibrariesDirectory, artifact.Path);
                if (File.Exists(path))
                    paths.Add(path);
                else
                    missingLibraries++;
                continue;
            }

            // Natives-Classifier für Windows: wird seit 1.17 als JAR mit auf den
            // Classpath gelegt (LWJGL extrahiert die DLLs zur Laufzeit selbst).
            if (downloads?.Classifiers is not null && TryGetWindowsNative(downloads.Classifiers, out var nativePath))
            {
                var path = Path.Combine(install.LibrariesDirectory, nativePath);
                if (File.Exists(path))
                    paths.Add(path);
                else
                    missingLibraries++;
            }
        }

        return string.Join(Path.PathSeparator.ToString(), paths);
    }

    private static bool TryGetWindowsNative(Dictionary<string, DownloadArtifact> classifiers, out string path)
    {
        path = "";
        foreach (var key in new[] { "natives-windows", "natives-windows-x86_64" })
        {
            if (classifiers.TryGetValue(key, out var artifact) && !string.IsNullOrEmpty(artifact.Path))
            {
                path = artifact.Path;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Legt das Natives-Verzeichnis für die JVM-Argumente an
    /// (java.library.path, jna.tmpdir, SharedLibraryExtractPath, netty.workdir).
    /// Die DLLs selbst lädt LWJGL aus den Classpath-JARs.
    /// </summary>
    private static string PrepareNativesDirectory(string gameDirectory, string versionId)
    {
        var nativesDirectory = Path.Combine(
            gameDirectory, "bin", "meinlauncher-natives", versionId);

        foreach (var sub in new[] { "", "java", "jna", "lwjgl", "netty" })
            Directory.CreateDirectory(Path.Combine(nativesDirectory, sub));

        return nativesDirectory;
    }

    // ------------------------------------------------------------ Argumente

    private static string BuildCommandLine(
        LauncherSettings settings,
        GameInstallation install,
        string versionId,
        VersionJson versionJson,
        MicrosoftSession session,
        string javaPath,
        string nativesDirectory,
        string classpath)
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["${auth_player_name}"] = session.MinecraftUsername,
            ["${version_name}"] = versionId,
            ["${game_directory}"] = install.GameDirectory,
            ["${assets_root}"] = install.AssetsDirectory,
            ["${assets_index_name}"] = versionJson.AssetIndex?.Id ?? "legacy",
            ["${auth_uuid}"] = session.MinecraftUuid,
            ["${auth_access_token}"] = session.AccessToken,
            ["${clientid}"] = settings.MicrosoftClientId,
            ["${auth_xuid}"] = session.Xuid ?? "",
            ["${version_type}"] = versionJson.Type,
            ["${natives_directory}"] = nativesDirectory,
            ["${launcher_name}"] = LauncherName,
            ["${launcher_version}"] = LauncherVersion,
            ["${classpath}"] = classpath,
            ["${path}"] = ResolveLoggingPath(install, versionJson),
        };

        var parts = new List<string>();

        if (settings.MaxRamMb > 0)
            parts.Add($"-Xmx{settings.MaxRamMb}M");

        // Logging (log4j2) separat, damit bei fehlender Datei kein kaputtes Argument entsteht.
        if (versionJson.Logging?.Client is { } logging &&
            !string.IsNullOrEmpty(logging.Argument) &&
            !string.IsNullOrWhiteSpace(tokens["${path}"]))
        {
            parts.Add(ReplaceTokens(logging.Argument, tokens));
        }

        foreach (var argument in versionJson.Arguments?.Jvm ?? [])
        {
            if (TryResolveArgument(argument, tokens, out var resolved))
                parts.AddRange(resolved);
        }

        parts.Add(versionJson.MainClass);

        var gameResolved = false;
        foreach (var argument in versionJson.Arguments?.Game ?? [])
        {
            if (TryResolveArgument(argument, tokens, out var resolved))
            {
                gameResolved = true;
                parts.AddRange(resolved);
            }
        }

        // Legacy-Argumente (vor 1.13, "minecraftArguments"-Feld).
        if (!gameResolved && !string.IsNullOrWhiteSpace(versionJson.MinecraftArguments))
            parts.AddRange(SplitArguments(ReplaceTokens(versionJson.MinecraftArguments, tokens)));

        return string.Join(" ", parts.Select(Quote));
    }

    /// <summary>Log4j-Konfigurationspfad (assets\log_configs\&lt;id&gt;), falls vorhanden.</summary>
    private static string ResolveLoggingPath(GameInstallation install, VersionJson versionJson)
    {
        var id = versionJson.Logging?.Client?.File?.Id;
        if (string.IsNullOrWhiteSpace(id))
            return "";

        var path = Path.Combine(install.AssetsDirectory, "log_configs", id);
        return File.Exists(path) ? path : "";
    }

    private static bool TryResolveArgument(JsonElement element, IReadOnlyDictionary<string, string> tokens, out List<string> resolved)
    {
        resolved = new List<string>();

        if (element.ValueKind == JsonValueKind.String)
        {
            resolved.Add(ReplaceTokens(element.GetString() ?? "", tokens));
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            if (!element.TryGetProperty("value", out var value))
                return false;

            if (!RulesAllow(element.TryGetProperty("rules", out var rules) ? rules : null))
                return false;

            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    resolved.Add(ReplaceTokens(value.GetString() ?? "", tokens));
                    return true;

                case JsonValueKind.Array:
                    foreach (var item in value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                            resolved.Add(ReplaceTokens(item.GetString() ?? "", tokens));
                    }
                    return true;
            }
        }

        return false;
    }

    // ------------------------------------------------------------ Regeln

    private static bool RulesAllow(JsonElement? rulesElement)
    {
        if (rulesElement is not JsonElement rules || rules.ValueKind != JsonValueKind.Array || rules.GetArrayLength() == 0)
            return true;

        try
        {
            var entries = JsonSerializer.Deserialize<List<RuleEntry>>(rules.GetRawText());
            return RulesAllow(entries);
        }
        catch
        {
            return false;
        }
    }

    private static bool RulesAllow(IReadOnlyList<RuleEntry>? rules)
    {
        if (rules is null || rules.Count == 0)
            return true;

        var allowed = false;
        foreach (var rule in rules)
        {
            if (RuleMatches(rule))
                allowed = rule.Action.Equals("allow", StringComparison.OrdinalIgnoreCase);
        }

        return allowed;
    }

    private static bool RuleMatches(RuleEntry rule)
    {
        if (rule.Os is { } os)
        {
            if (!string.IsNullOrEmpty(os.Name) &&
                !os.Name.Equals("windows", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(os.Arch))
            {
                var is64 = Environment.Is64BitOperatingSystem;
                var matches = (os.Arch.Equals("x86", StringComparison.OrdinalIgnoreCase) && !is64) ||
                              (os.Arch.Equals("x86_64", StringComparison.OrdinalIgnoreCase) && is64) ||
                              (os.Arch.Equals("amd64", StringComparison.OrdinalIgnoreCase) && is64);
                if (!matches)
                    return false;
            }
        }

        // Features aktivieren wir nie (Demo, Custom-Resolution, QuickPlay …).
        if (rule.Features is { Count: > 0 })
            return false;

        return true;
    }

    // ------------------------------------------------------------ Token & Zitat

    private static string ReplaceTokens(string value, IReadOnlyDictionary<string, string> tokens)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOf("${", StringComparison.Ordinal) < 0)
            return value;

        var result = new StringBuilder(value);
        foreach (var pair in tokens)
            result.Replace(pair.Key, pair.Value);

        return result.ToString();
    }

    /// <summary>Wichtig: Leerzeichen in Pfaden müssen für den Startprozess maskiert werden.</summary>
    private static string Quote(string argument)
    {
        if (string.IsNullOrEmpty(argument) ||
            (argument[0] == '"' && argument[^1] == '"') ||
            !argument.Any(c => c == ' ' || c == '\t'))
        {
            return argument;
        }

        return "\"" + argument.Replace("\"", "\\\"") + "\"";
    }

    private static IEnumerable<string> SplitArguments(string arguments)
    {
        foreach (var part in arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            yield return part.Trim();
    }
}
