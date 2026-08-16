namespace MeinLauncher.ViewModels;

/// <summary>
/// Loader-Auswahl (fabric / forge / neoforge / quilt / liteloader) mit
/// lokalisiertem Anzeigetext und dem API-Wert für Modrinth.
/// </summary>
public sealed class ModLoaderItem : LocalizedItem
{
    /// <summary>Wert für die Modrinth-API (z. B. "neoforge").</summary>
    public string Value { get; }

    public ModLoaderItem(string value)
        : base(KeyFor(value))
    {
        Value = value;
    }

    private static string KeyFor(string value) => value switch
    {
        "neoforge" => "Mods.LoaderNeoForge",
        "liteloader" => "Mods.LoaderLiteLoader",
        _ => "Mods.Loader" + (value.Length > 0 ? char.ToUpperInvariant(value[0]) + value[1..] : value),
    };
}
