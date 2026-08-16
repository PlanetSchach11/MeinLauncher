using CommunityToolkit.Mvvm.ComponentModel;
using MeinLauncher.Services;

namespace MeinLauncher.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    /// <summary>Zentrale Lokalisierung – für bindbare XAML-Texte.</summary>
    public LocalizationManager L => LocalizationManager.Instance;

    /// <summary>Übersetzter Text für Meldungen aus ViewModels.</summary>
    protected string t(string key, params object[] args) => LocalizationManager.Instance.Get(key, args);
}
