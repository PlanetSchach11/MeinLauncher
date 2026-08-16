using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using MeinLauncher.Services;

namespace MeinLauncher.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) => ThemeManager.ApplyWindow(this);
        ThemeManager.Applied += () => ThemeManager.ApplyWindow(this);

        // Dezenter UI-Klick-Sound: echte Klicks (links) auf Buttons (inkl. Farbauswahl)
        // und Navigation. Autorepeat-Elemente wie der Slider-Track oder Scrollbar-Pfeile
        // (RepeatButton) spielen KEINEN Sound - sie feuern ClickEvent dauerhaft, solange
        // die Maustaste gehalten wird. Der Debounce im UISoundService verhindert zudem
        // Mehrfach-Sound, wenn ein Klick mehrere Ereignisse auslöst.
        AddHandler(Button.ClickEvent, OnUiClick, RoutingStrategies.Bubble, true);
        SidebarList.AddHandler(InputElement.TappedEvent, OnUiClick, RoutingStrategies.Bubble, true);
    }

    private static void OnUiClick(object? sender, RoutedEventArgs e)
    {
        // RepeatButton = Autorepeat (Slider-Track, Scrollbar-Pfeile): kein Sound.
        if (e.Source is RepeatButton)
            return;
        UISoundService.Instance.Play();
    }
}
