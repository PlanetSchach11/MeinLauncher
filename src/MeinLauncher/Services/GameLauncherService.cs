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
    private static readonly string LauncherVersion = AppVersion.Current;
    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>App-Familienname der Store-Version des offiziellen Launchers (nur Pfad-Auflösung).</summary>
    private const string StoreFamily = "Microsoft.4297127D64EC6_8wekyb3d8bbwe";

    private readonly MicrosoftAccountService _accountService;

    public GameLauncherService(MicrosoftAccountService accountService)
        => _accountService = accountService;

    /// <summary>
    /// Startet die gewählte Version. Liefert niemals eine Exception – Fehler werden
    /// als Ergebnis mit passender Lokalisierungs-Meldung zurückgegeben.
    /// </summary>
    public async Task<GameLaunchResult> LaunchAsync(LauncherSettings settings, LauncherProfile? activeProfile = null, CancellationToken cancellationToken = default)
    {
        var versionId = settings.SelectedVersionId.Trim();
        if (string.IsNullOrWhiteSpace(versionId))
            return Fail("Home.NoVersionSelected");

        // Bei Fabric: Prüfen ob die Fabric-Loader-Version existiert, sonst automatisch installieren.
        if (string.Equals(settings.ModLoader, "fabric", StringComparison.OrdinalIgnoreCase))
        {
            var versionsDir = Path.Combine(settings.GameDirectory, "versions");
            var librariesDir = Path.Combine(settings.GameDirectory, "libraries");
            var fabricDir = await FabricInstallerService.EnsureFabricLoaderInstalledAsync(
                versionId, versionsDir, librariesDir);
            if (fabricDir is null && FindVersionDirectory(settings.GameDirectory, GetOfficialMinecraftDir(), versionId, "fabric") is null)
            {
                AccountDiagnostics.Log($"[FABRIC] Installation fehlgeschlagen oder nicht verfügbar für {versionId}.");
                // Nicht abbrechen – resolveInstallation prüft nochmal.
            }
        }

        var install = ResolveInstallation(settings, versionId, activeProfile);
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

        var versionJson = LoadVersionJson(install.VersionDirectory, versionId, settings.GameDirectory);
        if (versionJson is null || string.IsNullOrWhiteSpace(versionJson.MainClass))
            return Fail("Home.VersionDataError", versionId);

        // Fehlende Libraries aus Maven-Repositories herunterladen (z.B. inheritsFrom-Parent).
        var libsDownloaded = await EnsureLibrariesInstalledAsync(versionJson.Libraries, install.LibrariesDirectory);
        if (libsDownloaded > 0)
            AccountDiagnostics.Log($"[LAUNCH] {libsDownloaded} fehlende Libraries heruntergeladen.");

        var javaPath = ResolveJavaPath(settings.JavaPath, versionJson.JavaVersion?.MajorVersion ?? 0);
        if (javaPath is null)
            return Fail("Home.JavaNotFound", (versionJson.JavaVersion?.MajorVersion ?? 0) > 0
                ? versionJson.JavaVersion!.MajorVersion.ToString()
                : "?");

        var nativesDirectory = PrepareNativesDirectory(install.GameDirectory, versionId);

        var classpath = BuildClasspath(install, versionId, versionJson, out var missingLibraries);
        if (missingLibraries > 0)
            AccountDiagnostics.Log($"[LAUNCH] {missingLibraries} fehlende Libraries für {versionId} (Start wird trotzdem versucht).");

        var commandLine = BuildCommandLine(
            settings, install, versionId, versionJson, session, javaPath, nativesDirectory, classpath);

        // Kulka Client Mod automatisch in den Profil-Mods-Ordner deployen (MC 1.21+ und 26.2+).
        if (IsKulkaModCompatible(versionId))
            EnsureKulkaModInstalled(Path.Combine(install.GameDirectory, "mods"), versionId);
        else
            RemoveAllKulkaMods(Path.Combine(install.GameDirectory, "mods"));

        // Kulka theme config for the Minecraft mod
        WriteKulkaThemeConfig(settings, install.GameDirectory);

        try
        {
            var javaw = Path.Combine(Path.GetDirectoryName(javaPath) ?? "", "javaw.exe");
            var executable = File.Exists(javaw) ? javaw : javaPath;

            AccountDiagnostics.Log($"[LAUNCH] Starte: {executable} in {install.GameDirectory}");

            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = commandLine,
                WorkingDirectory = install.GameDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var stdoutBuilder = new System.Text.StringBuilder();
            var stderrBuilder = new System.Text.StringBuilder();

            AccountDiagnostics.Log("[LAUNCH] Process.Start() wird aufgerufen...");
            var process = Process.Start(psi);
            if (process is null)
            {
                AccountDiagnostics.Log("[LAUNCH] FEHLER: Process.Start() gab null zurück!");
                return Fail("Home.GameLaunchFailed", "Prozess konnte nicht gestartet werden.");
            }

            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdoutBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderrBuilder.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            AccountDiagnostics.Log($"[LAUNCH] Prozess gestartet! PID={process.Id}");

            // Bis zu 15 Sekunden warten – wenn Minecraft danach noch läuft, ist es ein Success.
            for (var i = 0; i < 15; i++)
            {
                await Task.Delay(1000, cancellationToken);
                if (process.HasExited)
                    break;
            }

            var stdout = stdoutBuilder.ToString();
            var stderr = stderrBuilder.ToString();

            if (!process.HasExited)
            {
                AccountDiagnostics.Log($"[LAUNCH] Prozess läuft nach 15s (PID={process.Id}). Erfolg!");
                return new GameLaunchResult(true, "Home.GameStarted", [versionId]);
            }

            // Prozess hat sich beendet – Fehler analysieren.
            var exitCode = process.ExitCode;
            AccountDiagnostics.Log($"[LAUNCH] Prozess beendet. ExitCode={exitCode}");

            if (!string.IsNullOrEmpty(stderr))
                AccountDiagnostics.Log($"[LAUNCH] --- stderr (komplett) ---\n{stderr}");
            if (!string.IsNullOrEmpty(stdout))
                AccountDiagnostics.Log($"[LAUNCH] --- stdout (komplett) ---\n{stdout}");

            // Crash-Reports und latest.log suchen.
            var gameDir = install.GameDirectory;
            var crashDir = Path.Combine(gameDir, "crash-reports");
            if (Directory.Exists(crashDir))
            {
                var latestCrash = new DirectoryInfo(crashDir)
                    .GetFiles("*.txt")
                    .OrderByDescending(f => f.LastWriteTime)
                    .FirstOrDefault();
                if (latestCrash is not null)
                    AccountDiagnostics.Log($"[LAUNCH] Neuester Crash-Report: {latestCrash.FullName}");
            }

            var latestLog = Path.Combine(gameDir, "logs", "latest.log");
            if (File.Exists(latestLog))
                AccountDiagnostics.Log($"[LAUNCH] latest.log: {latestLog}");

            if (exitCode != 0)
            {
                // Erste Exception/NoClassDef im stderr extrahieren.
                var firstError = ExtractFirstError(stderr) ?? ExtractFirstError(stdout);
                AccountDiagnostics.Log($"[LAUNCH] Erster Fehler: {firstError ?? "(keiner gefunden)"}");
                return Fail("Home.GameLaunchFailed", $"ExitCode {exitCode}: {firstError ?? "siehe account.log"}");
            }

            return new GameLaunchResult(true, "Home.GameStarted", [versionId]);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            AccountDiagnostics.Log($"[LAUNCH] Win32Exception: {ex.Message}");
            AccountDiagnostics.Log($"[LAUNCH] ErrorCode: {ex.NativeErrorCode}");
            AccountDiagnostics.Log($"[LAUNCH] Full: {ex}");
            return Fail("Home.GameLaunchFailed", ex.Message);
        }
        catch (Exception ex)
        {
            AccountDiagnostics.Log($"[LAUNCH] Exception: {ex}");
            return Fail("Home.GameLaunchFailed", ex.Message);
        }
    }

    /// <summary>Sucht die erste Exception/Error-Zeile in einem Textblock.</summary>
    private static string? ExtractFirstError(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Exception in thread", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Caused by:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("NoClassDefFoundError", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("ClassNotFoundException", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("Could not find", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("cannot be cast to", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }
        }
        return null;
    }

    private static GameLaunchResult Fail(string messageKey, params object[] args)
        => new(false, messageKey, args);

    private static string GetOfficialMinecraftDir()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

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
    private static GameInstallation? ResolveInstallation(LauncherSettings settings, string versionId, LauncherProfile? activeProfile)
    {
        var root = settings.GameDirectory;
        var official = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

        // Version-Verzeichnis: bei Fabric zuerst Fabric-Version suchen, sonst eigene Version.
        var versionDirectory = FindVersionDirectory(root, official, versionId, settings.ModLoader);

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

        // Instanz-Verzeichnis: Profil-Verzeichnis wenn Profil vorhanden, sonst Fallback.
        var instanceDirectory = activeProfile is { } profile
            ? Path.Combine(root, "profiles", LauncherSettings.SanitizeFolderName(profile.Name))
            : settings.InstanceDirectory;

        return new GameInstallation(instanceDirectory, versionDirectory, librariesDirectory, assetsDirectory);
    }

    /// <summary>
    /// Sucht das Version-Verzeichnis. Bei Fabric wird zuerst die Fabric-Version
    /// (fabric-loader-*-{versionId}) gesucht, dann eigene Version, dann .minecraft.
    /// </summary>
    private static string? FindVersionDirectory(string root, string official, string versionId, string? modLoader)
    {
        var isFabric = string.Equals(modLoader, "fabric", StringComparison.OrdinalIgnoreCase);
        var versionsDir = Path.Combine(root, "versions");

        // Bei Fabric: ZUERST nach Fabric-Version suchen.
        if (isFabric && Directory.Exists(versionsDir))
        {
            var suffix = $"-{versionId}";
            foreach (var dir in Directory.EnumerateDirectories(versionsDir))
            {
                var name = Path.GetFileName(dir);
                if (name.StartsWith("fabric-loader-", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(Path.Combine(dir, name + ".json")))
                {
                    return dir;
                }
            }
        }

        // Eigene Version (z.B. games/versions/26.2/26.2.json)
        var ownVersion = Path.Combine(root, "versions", versionId);
        if (File.Exists(Path.Combine(ownVersion, versionId + ".json")))
            return ownVersion;

        // Fabric-Version als Fallback (falls oben nicht gefunden)
        if (!isFabric && Directory.Exists(versionsDir))
        {
            var suffix = $"-{versionId}";
            foreach (var dir in Directory.EnumerateDirectories(versionsDir))
            {
                var name = Path.GetFileName(dir);
                if (name.StartsWith("fabric-loader-", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(Path.Combine(dir, name + ".json")))
                {
                    return dir;
                }
            }
        }

        // .minecraft Fallback
        var officialVersion = Path.Combine(official, "versions", versionId);
        if (Directory.Exists(officialVersion))
            return officialVersion;

        return null;
    }

    private static VersionJson? LoadVersionJson(string versionDirectory, string versionId, string gameRoot)
    {
        try
        {
            // Zuerst mit dem eigenen Namen suchen (z.B. 26.2.json).
            var path = Path.Combine(versionDirectory, versionId + ".json");

            // Fallback: Die erste .json-Datei im Verzeichnis (z.B. fabric-loader-*.json).
            if (!File.Exists(path) && Directory.Exists(versionDirectory))
            {
                var jsonFiles = Directory.GetFiles(versionDirectory, "*.json");
                if (jsonFiles.Length > 0)
                    path = jsonFiles[0];
            }

            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            var versionJson = JsonSerializer.Deserialize<VersionJson>(json);
            if (versionJson is null)
                return null;

            // inheritsFrom auflösen: Eltern-Version laden und Libraries mergen.
            if (!string.IsNullOrEmpty(versionJson.InheritsFrom))
            {
                var parentJson = LoadParentVersionJson(versionJson.InheritsFrom, gameRoot);
                if (parentJson is not null)
                {
                    // Eltern-Libraries zuerst, dann Fabric-Libs.
                    // Deduplizierung nach group:artifact (ohne Version), damit z.B.
                    // asm-9.6 (Vanilla) durch asm-9.10.1 (Fabric) ersetzt wird.
                    var mergedLibraries = new List<LibraryEntry>(parentJson.Libraries);
                    foreach (var childLib in versionJson.Libraries)
                    {
                        var childArtifact = GetMavenArtifactId(childLib.Name);
                        if (string.IsNullOrEmpty(childArtifact))
                        {
                            mergedLibraries.Add(childLib);
                            continue;
                        }

                        // Entferne Parent-Library mit gleichem group:artifact (ältere Version).
                        for (var i = mergedLibraries.Count - 1; i >= 0; i--)
                        {
                            var existingArtifact = GetMavenArtifactId(mergedLibraries[i].Name);
                            if (!string.IsNullOrEmpty(existingArtifact) && existingArtifact == childArtifact)
                            {
                                mergedLibraries.RemoveAt(i);
                            }
                        }
                        mergedLibraries.Add(childLib);
                    }
                    versionJson.Libraries = mergedLibraries;

                    // Felder vom Elternteil übernehmen, falls nicht vorhanden.
                    versionJson.AssetIndex ??= parentJson.AssetIndex;
                    versionJson.Logging ??= parentJson.Logging;
                    versionJson.JavaVersion ??= parentJson.JavaVersion;

                    // Argumente mergen: Parent-JVM-Args zuerst (-cp, -D, etc.), dann Child-Args.
                    if (parentJson.Arguments is { } parentArgs)
                    {
                        versionJson.Arguments ??= new ArgumentsSection();

                        var mergedJvm = new List<JsonElement>(parentArgs.Jvm);
                        mergedJvm.AddRange(versionJson.Arguments.Jvm);
                        versionJson.Arguments.Jvm = mergedJvm;

                        var mergedGame = new List<JsonElement>(parentArgs.Game);
                        mergedGame.AddRange(versionJson.Arguments.Game);
                        versionJson.Arguments.Game = mergedGame;
                    }
                }
            }

            return versionJson;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Lädt die Eltern-Version-JSON für inheritsFrom (zuerst Game-Verzeichnis, dann .minecraft).</summary>
    private static VersionJson? LoadParentVersionJson(string parentId, string gameRoot)
    {
        // Zuerst im Game-Verzeichnis suchen (MeinLauncher/game versions).
        var gameDirPath = Path.Combine(gameRoot, "versions", parentId, parentId + ".json");
        if (File.Exists(gameDirPath))
        {
            var json = File.ReadAllText(gameDirPath);
            return JsonSerializer.Deserialize<VersionJson>(json);
        }

        // Fallback: Offizielle Installation (.minecraft).
        var officialPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft", "versions", parentId, parentId + ".json");

        if (!File.Exists(officialPath))
            return null;

        var officialJson = File.ReadAllText(officialPath);
        return JsonSerializer.Deserialize<VersionJson>(officialJson);
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

        // Fallback: Client-JAR im Game-Verzeichnis oder .minecraft suchen
        // (Fabric-Versionen haben kein eigenes .jar – die JAR kommt aus der Parent-Version).
        if (!File.Exists(clientJar))
        {
            var gameRoot = Path.GetDirectoryName(install.LibrariesDirectory);
            if (!string.IsNullOrEmpty(gameRoot))
            {
                var gameRootJar = Path.Combine(gameRoot, "versions", versionId, versionId + ".jar");
                if (File.Exists(gameRootJar))
                    clientJar = gameRootJar;
            }
        }

        if (!File.Exists(clientJar))
        {
            var officialJar = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".minecraft", "versions", versionId, versionId + ".jar");
            if (File.Exists(officialJar))
                clientJar = officialJar;
        }

        if (File.Exists(clientJar))
            paths.Add(clientJar);
        else
        {
            missingLibraries++;
            AccountDiagnostics.Log($"[CLASSPATH] Client-JAR fehlt: gesucht in {install.VersionDirectory}, GameRoot, .minecraft");
        }

        foreach (var library in versionJson.Libraries)
        {
            if (!RulesAllow(library.Rules))
                continue;

            var downloads = library.Downloads;

            // 1. Versuche downloads.artifact.path (Mojang-Format).
            if (downloads?.Artifact is { } artifact && !string.IsNullOrEmpty(artifact.Path))
            {
                var path = Path.Combine(install.LibrariesDirectory, artifact.Path);
                if (File.Exists(path))
                {
                    paths.Add(path);
                    continue;
                }

                // Fallback: .minecraft libraries
                var officialArtifact = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    ".minecraft", "libraries", artifact.Path);
                if (File.Exists(officialArtifact))
                {
                    paths.Add(officialArtifact);
                    continue;
                }

                // Fallback: Maven-Name-basierte Auflösung
                if (!string.IsNullOrEmpty(library.Name))
                {
                    var mavenPath = MavenNameToPath(library.Name);
                    if (!string.IsNullOrEmpty(mavenPath))
                    {
                        var mavenFull = Path.Combine(install.LibrariesDirectory, mavenPath);
                        if (File.Exists(mavenFull))
                        {
                            paths.Add(mavenFull);
                            continue;
                        }

                        var mavenOfficial = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            ".minecraft", "libraries", mavenPath);
                        if (File.Exists(mavenOfficial))
                        {
                            paths.Add(mavenOfficial);
                            continue;
                        }
                    }
                }

                missingLibraries++;
                AccountDiagnostics.Log($"[CLASSPATH] Fehlt (artifact): {artifact.Path} | name={library.Name}");
                continue;
            }

            // 2. Maven-Name-basierte Auflösung (für Fabric/Forge-Bibliotheken ohne downloads.artifact).
            if (!string.IsNullOrEmpty(library.Name))
            {
                var mavenPath = MavenNameToPath(library.Name);
                if (!string.IsNullOrEmpty(mavenPath))
                {
                    var fullPath = Path.Combine(install.LibrariesDirectory, mavenPath);
                    if (File.Exists(fullPath))
                    {
                        paths.Add(fullPath);
                        continue;
                    }

                    // Fallback: Im .minecraft libraries suchen.
                    var officialLib = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        ".minecraft", "libraries", mavenPath);
                    if (File.Exists(officialLib))
                    {
                        paths.Add(officialLib);
                        continue;
                    }

                    // Still add it to classpath even if missing — let Java report the exact error.
                    // Only count as missing for critical check if we have more than 1.
                    AccountDiagnostics.Log($"[CLASSPATH] Fehlt (maven): {library.Name} → {mavenPath}");
                }
            }

            // 3. Natives-Classifier für Windows.
            if (downloads?.Classifiers is not null && TryGetWindowsNative(downloads.Classifiers, out var nativePath))
            {
                var path = Path.Combine(install.LibrariesDirectory, nativePath);
                if (File.Exists(path))
                {
                    paths.Add(path);
                    continue;
                }

                // Fallback: .minecraft
                var officialNative = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    ".minecraft", "libraries", nativePath);
                if (File.Exists(officialNative))
                {
                    paths.Add(officialNative);
                    continue;
                }

                missingLibraries++;
                AccountDiagnostics.Log($"[CLASSPATH] Fehlt (native): {nativePath}");
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
    /// Extrahiert "group:artifact" aus einem Maven-Namen wie "org.ow2.asm:asm:9.6"
    /// → "org.ow2.asm:asm". Gibt null zurück, wenn das Format nicht stimmt.
    /// </summary>
    private static string? GetMavenArtifactId(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        var parts = name.Split(':');
        return parts.Length >= 2 ? $"{parts[0]}:{parts[1]}" : null;
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

    // ------------------------------------------------------------ Maven

    /// <summary>
    /// Stellt sicher, dass alle fehlenden Libraries aus den Maven-Repositories
    /// heruntergeladen werden. Prüft zuerst die lokale Bibliothek, dann .minecraft,
    /// und lädt bei Bedarf von der Registry herunter.
    /// </summary>
    private static async Task<int> EnsureLibrariesInstalledAsync(List<LibraryEntry> libraries, string librariesDirectory)
    {
        var downloaded = 0;
        Directory.CreateDirectory(librariesDirectory);

        foreach (var library in libraries)
        {
            if (string.IsNullOrEmpty(library.Name))
                continue;

            // Nur Libraries ohne downloads.artifact (Maven-basiert) – andere haben eigene Pfade.
            if (library.Downloads?.Artifact is { } art && !string.IsNullOrEmpty(art.Path))
            {
                var artPath = Path.Combine(librariesDirectory, art.Path);
                if (File.Exists(artPath))
                    continue;

                // Von downloads.artifact.url herunterladen.
                if (!string.IsNullOrEmpty(art.Url))
                {
                    try
                    {
                        var dir = Path.GetDirectoryName(artPath);
                        if (dir is not null) Directory.CreateDirectory(dir);
                        var data = await Http.GetByteArrayAsync(art.Url);
                        await File.WriteAllBytesAsync(artPath, data);
                        downloaded++;
                    }
                    catch { /* Best effort */ }
                }
                continue;
            }

            var mavenPath = MavenNameToPath(library.Name);
            if (string.IsNullOrEmpty(mavenPath))
                continue;

            var fullPath = Path.Combine(librariesDirectory, mavenPath);
            if (File.Exists(fullPath))
                continue;

            // Bereits in .minecraft?
            var officialLib = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".minecraft", "libraries", mavenPath);
            if (File.Exists(officialLib))
                continue;

            // Von Repository herunterladen.
            var baseUrl = !string.IsNullOrEmpty(library.Url)
                ? library.Url
                : "https://libraries.minecraft.net/";

            if (!baseUrl.EndsWith("/"))
                baseUrl += "/";

            var downloadUrl = baseUrl + mavenPath;
            try
            {
                var dir = Path.GetDirectoryName(fullPath);
                if (dir is not null) Directory.CreateDirectory(dir);
                var data = await Http.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(fullPath, data);
                downloaded++;
            }
            catch
            {
                // Fallback: Versuche Maven Central.
                if (!baseUrl.Contains("repo1.maven.org"))
                {
                    try
                    {
                        var fallbackUrl = "https://repo1.maven.org/maven2/" + mavenPath;
                        var dir = Path.GetDirectoryName(fullPath);
                        if (dir is not null) Directory.CreateDirectory(dir);
                        var data = await Http.GetByteArrayAsync(fallbackUrl);
                        await File.WriteAllBytesAsync(fullPath, data);
                        downloaded++;
                    }
                    catch { /* skip */ }
                }
            }
        }

        return downloaded;
    }

    /// <summary>Konvertiert Maven-Koordinaten in einen Dateipfad.</summary>
    /// <example>"org.ow2.asm:asm:9.8" → "org/ow2/asm/asm/9.8/asm-9.8.jar"</example>
    private static string MavenNameToPath(string name)
    {
        var parts = name.Split(':');
        if (parts.Length < 3)
            return "";

        var groupId = parts[0].Replace('.', '/');
        var artifactId = parts[1];
        var version = parts[2];
        var classifier = parts.Length > 3 ? $"-{parts[3]}" : "";

        return $"{groupId}/{artifactId}/{version}/{artifactId}-{version}{classifier}.jar";
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

    // ------------------------------------------------------------ Kulka Mod Auto-Deploy

    private const string KulkaModResourceName_26_2 = "kulka-client-1.0.0.jar";
    private const string KulkaModFileName_26_2 = "kulka-client-1.0.0.jar";
    private const string KulkaModResourceName_1_21 = "kulka-client-1.0.0-1.21.jar";
    private const string KulkaModFileName_1_21 = "kulka-client-1.0.0-1.21.jar";

    /// <summary>
    /// Prüft ob die Kulka Client Mod mit der gegebenen MC-Version kompatibel ist.
    /// Unterstützt MC 1.21.x und MC 26.2+.
    /// </summary>
    private static bool IsKulkaModCompatible(string versionId)
    {
        if (!int.TryParse(versionId.Split('.')[0], out var major))
            return false;
        // MC 1.21.x (major == 1, minor >= 21) or MC 26+ (new versioning)
        if (major == 1)
        {
            if (versionId.Split('.').Length >= 2 && int.TryParse(versionId.Split('.')[1], out var minor))
                return minor >= 21;
            return false;
        }
        return major >= 26;
    }

    /// <summary>
    /// Selects the appropriate Kulka mod resource and filename for the given MC version.
    /// </summary>
    private static (string ResourceName, string FileName) GetKulkaModForVersion(string versionId)
    {
        if (!int.TryParse(versionId.Split('.')[0], out var major))
            return (KulkaModResourceName_26_2, KulkaModFileName_26_2);

        if (major == 1)
            return (KulkaModResourceName_1_21, KulkaModFileName_1_21);

        return (KulkaModResourceName_26_2, KulkaModFileName_26_2);
    }

    /// <summary>
    /// Entfernt ALLE Kulka Mods aus dem Mods-Ordner (sowohl 1.21 als auch 26.2).
    /// </summary>
    private static void RemoveAllKulkaMods(string modsDirectory)
    {
        foreach (var fileName in new[] { KulkaModFileName_26_2, KulkaModFileName_1_21 })
        {
            var path = Path.Combine(modsDirectory, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
                AccountDiagnostics.Log($"[Kulka] Mod entfernt (incompatible mit dieser MC-Version): {path}");
            }
        }
    }

    private static void EnsureKulkaModInstalled(string modsDirectory, string versionId)
    {
        try
        {
            var (resourceName, fileName) = GetKulkaModForVersion(versionId);

            Directory.CreateDirectory(modsDirectory);
            var targetPath = Path.Combine(modsDirectory, fileName);

            // Remove the OTHER version's JAR if present (can't have both)
            var otherFileName = fileName == KulkaModFileName_26_2 ? KulkaModFileName_1_21 : KulkaModFileName_26_2;
            var otherPath = Path.Combine(modsDirectory, otherFileName);
            if (File.Exists(otherPath))
            {
                File.Delete(otherPath);
                AccountDiagnostics.Log($"[Kulka] Alte Mod-Version entfernt: {otherPath}");
            }

            // Prüfen ob die Mod bereits vorhanden und aktuell ist (Größe als schneller Check).
            using var stream = typeof(GameLauncherService).Assembly
                .GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                AccountDiagnostics.Log($"[Kulka] EmbeddedResource '{resourceName}' nicht gefunden – Mod wird nicht deployed.");
                return;
            }

            var resourceLength = stream.Length;

            if (File.Exists(targetPath))
            {
                var fileInfo = new FileInfo(targetPath);
                if (fileInfo.Length == resourceLength)
                    return; // Bereits aktuell.

                AccountDiagnostics.Log($"[Kulka] Mod veraltet (Launcher: {resourceLength}, Profil: {fileInfo.Length}) – aktualisiere.");
            }
            else
            {
                AccountDiagnostics.Log($"[Kulka] Mod fehlt im Profil – installiere {fileName} ({resourceLength} bytes).");
            }

            // JAR in den Mods-Ordner schreiben.
            using var fileStream = File.Create(targetPath);
            stream.CopyTo(fileStream);
            stream.Position = 0; // Reset für den Fall, dass noch jemand liest.

            AccountDiagnostics.Log($"[Kulka] Mod erfolgreich deployed: {targetPath}");
        }
        catch (Exception ex)
        {
            AccountDiagnostics.Log($"[Kulka] Fehler beim Deploy: {ex.Message}");
        }
    }

    // ------------------------------------------------------------ Kulka Theme

    /// <summary>
    /// Writes kulka-theme.json to the game directory so the Minecraft mod can
    /// read the launcher's design settings (background, accent, text colors).
    /// </summary>
    private static void WriteKulkaThemeConfig(LauncherSettings settings, string gameDirectory)
    {
        try
        {
            var dark = !string.Equals(settings.Theme, "Light", StringComparison.OrdinalIgnoreCase);

            // Accent color from the same lookup used by ThemeManager
            var accentHex = ThemeManager.Accents.TryGetValue(settings.Accent, out var accentColor)
                ? $"#{accentColor.R:X2}{accentColor.G:X2}{accentColor.B:X2}"
                : "#7BC043";

            // Background colors (same logic as ThemeManager.Apply)
            var bgWindow = dark ? "#14161A" : "#F2F4F7";
            var bgCard = dark ? "#1B1E23" : "#FFFFFF";
            var textPrimary = dark ? "#E8EAED" : "#1B1F24";
            var textSecondary = dark ? "#9AA3AF" : "#5A6472";

            // Transparency alpha (same as ThemeManager.Apply)
            var alpha = settings.Transparency switch
            {
                "Strong" => 0.72,
                "Light" => 0.88,
                _ => 1.0,
            };

            // Background shapes config (from launcher settings)
            var bg = settings.Background;
            var bgConfig = bg?.Enabled == true ? new
            {
                enabled = true,
                kinds = bg.Kinds.Select(k => k.ToString()).ToList(),
                count = Math.Clamp(bg.Count, 1, 200),
                density = Math.Clamp(bg.Density, 0.05, 1.0),
                opacity = Math.Clamp(bg.Opacity, 0.0, 1.0),
                size = Math.Clamp(bg.Size, 4.0, 120.0),
                spacing = Math.Clamp(bg.Spacing, 0.0, 1.0),
                placement = bg.Placement.ToString(),
                color = string.IsNullOrWhiteSpace(bg.ColorHex) ? accentHex : bg.ColorHex,
                animate = bg.Animate,
                speed = Math.Clamp(bg.Speed, 0.0, 5.0),
                intensity = Math.Clamp(bg.Intensity, 0.0, 1.0),
                direction = bg.Direction.ToString(),
                rotate = bg.Rotate,
                rotationSpeed = Math.Clamp(bg.RotationSpeed, 0.0, 360.0),
            } : null;

            var themeJson = JsonSerializer.Serialize(new
            {
                theme = dark ? "dark" : "light",
                accentName = settings.Accent,
                accent = accentHex,
                bgWindow = bgWindow,
                bgCard = bgCard,
                textPrimary = textPrimary,
                textSecondary = textSecondary,
                transparency = alpha,
                background = bgConfig,
            }, new JsonSerializerOptions { WriteIndented = true });

            var themePath = Path.Combine(gameDirectory, "kulka-theme.json");
            File.WriteAllText(themePath, themeJson);
            AccountDiagnostics.Log($"[THEME] kulka-theme.json geschrieben: {themePath}");
        }
        catch (Exception ex)
        {
            AccountDiagnostics.Log($"[THEME] Fehler beim Schreiben von kulka-theme.json: {ex.Message}");
        }
    }
}
