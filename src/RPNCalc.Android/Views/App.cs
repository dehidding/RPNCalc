using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace RPNCalc.Droid;

/// <summary>Code-only Avalonia application for Android (single-view lifetime, no XAML).</summary>
public sealed class App : Application
{
    /// <summary>settings.json in app-private storage; set by MainActivity before the app starts.</summary>
    public static string? SettingsPath { get; set; }

    public static CalculatorView? View { get; private set; }

    public override void Initialize()
    {
        Name = "RPNCalc";
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            string path = SettingsPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
                "settings.json");
            single.MainView = View = new CalculatorView(path);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
