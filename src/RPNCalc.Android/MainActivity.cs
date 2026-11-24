using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;

namespace RPNCalc.Droid;

[Activity(
    Label = "RPNCalc",
    Theme = "@style/Theme.AppCompat.Light.NoActionBar",
    Icon = "@mipmap/appicon",
    MainLauncher = true,
    Exported = true,
    WindowSoftInputMode = Android.Views.SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                         | ConfigChanges.SmallestScreenSize | ConfigChanges.UiMode | ConfigChanges.Keyboard
                         | ConfigChanges.KeyboardHidden | ConfigChanges.Navigation)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // App-private storage (/data/data/com.dehidding.rpncalc/files): no permissions needed, removed on uninstall.
        App.SettingsPath ??= System.IO.Path.Combine(FilesDir!.AbsolutePath, "settings.json");
        base.OnCreate(savedInstanceState);
    }

    protected override void OnPause()
    {
        Save();
        base.OnPause();
    }

    protected override void OnStop()
    {
        Save();
        base.OnStop();
    }

    private static void Save()
    {
        try { App.View?.SaveSettings(); }
        catch (Exception ex) { Android.Util.Log.Warn("RPNCalc", "Saving settings failed: " + ex.Message); }
    }
}
