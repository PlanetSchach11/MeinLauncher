using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MeinLauncher.Services;

namespace MeinLauncher.Views.Settings;

public partial class UiSettingsView : UserControl
{
    public UiSettingsView()
    {
        InitializeComponent();

        // ToggleSwitch ist kein Button - der eigene Klick-Sound wird hier im Code
        // mit handledEventsToo=true angehängt: ToggleButton markiert Pointer-Events
        // selbst als "handled", ein XAML-Handler würde sonst nie aufgerufen.
        // PointerReleased (statt Pressed): Der Sound kommt nur beim Loslassen ÜBER
        // dem Toggle - also bei einem echten Klick, nie beim Gedrückthalten.
        UiSoundToggle.AddHandler(InputElement.PointerReleasedEvent, OnUiSoundToggleReleased,
            RoutingStrategies.Bubble, true);
    }

    /// <summary>
    /// Klick-Sound für den Toggle: nur links, nur beim Loslassen ÜBER dem Toggle
    /// (echter Klick), nie beim Gedrückthalten. PlayOnce spielt unabhängig vom
    /// An/Aus-Zustand, damit auch das Ausschalten noch klickt.
    /// </summary>
    private void OnUiSoundToggleReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        if (!UiSoundToggle.Bounds.Contains(e.GetPosition(UiSoundToggle)))
            return;
        UISoundService.Instance.PlayOnce();
    }
}
