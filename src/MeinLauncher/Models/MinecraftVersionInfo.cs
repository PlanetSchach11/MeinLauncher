namespace MeinLauncher.Models;

/// <summary>
/// Ein Eintrag der Mojang-Versionsliste (Version Manifest).
/// </summary>
public sealed record MinecraftVersionInfo(
    string Id,
    string Type,
    string ReleaseTime,
    string Url)
{
    public bool IsRelease => Type == "release";

    public string TypeLabel => IsRelease ? "Release" : "Snapshot";
}
