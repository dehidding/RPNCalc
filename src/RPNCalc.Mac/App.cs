using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace RPNCalc.Mac;

/// <summary>Code-only Avalonia application (no XAML).</summary>
public sealed class App : Application
{
    public override void Initialize()
    {
        Name = "RPNCalc";                         // shown as the macOS application menu title
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var main = new MainWindow();
            desktop.MainWindow = main;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            // Cmd+Q / app-menu Quit / logout: save even if the window close is skipped.
            desktop.ShutdownRequested += (_, _) => main.SaveSettings();
            desktop.Exit += (_, _) => main.SaveSettings();
            BuildAppMenu(main);
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// macOS application menu ("RPNCalc"): About and Help are added here; macOS/Avalonia add
    /// the standard Hide / Quit (Cmd+Q) items automatically. Ignored on other platforms.
    /// </summary>
    private void BuildAppMenu(MainWindow main)
    {
        var about = new NativeMenuItem("About RPNCalc");
        about.Click += (_, _) => main.ShowAbout();
        var help = new NativeMenuItem("RPNCalc Help") { Gesture = new KeyGesture(Key.OemQuestion, KeyModifiers.Meta | KeyModifiers.Shift) };
        help.Click += (_, _) => main.ShowHelp();
        NativeMenu.SetMenu(this, new NativeMenu { about, new NativeMenuItemSeparator(), help });
    }
}
