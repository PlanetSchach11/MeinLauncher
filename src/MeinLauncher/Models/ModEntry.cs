using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MeinLauncher.Services;

namespace MeinLauncher.Models;

/// <summary>
/// Eine gefundene Mod-Datei im Mods-Ordner (.jar bzw. .jar.disabled).
/// </summary>
public partial class ModEntry : ObservableObject
{
    public LocalizationManager L => LocalizationManager.Instance;

    public string FullPath { get; set; } = "";

    public string Name { get; init; } = "";

    public long SizeBytes { get; init; }

    public DateTime Modified { get; init; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    public string SizeLabel
    {
        get
        {
            string[] units = ["B", "KB", "MB", "GB"];
            double size = SizeBytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return $"{size:0.##} {units[unit]}";
        }
    }

    public string ModifiedLabel => Modified.ToString("dd.MM.yyyy HH:mm");
}
