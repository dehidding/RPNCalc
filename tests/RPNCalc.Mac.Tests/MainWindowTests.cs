using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RPNCalc.Engine;
using RPNCalc.Mac;

[assembly: AvaloniaTestApplication(typeof(RPNCalc.Mac.Tests.TestAppBuilder))]

namespace RPNCalc.Mac.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>Same theme as the real App, without creating the real main window.</summary>
public sealed class TestApp : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
    }
}

public sealed class MainWindowTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rpncalc_mac_" + Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_dir, "settings.json");
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    private (MainWindow W, List<string> Errors) Open()
    {
        var w = new MainWindow(SettingsPath);
        var errors = new List<string>();
        w.ErrorHandler = m => { errors.Add(m); return Task.CompletedTask; };
        w.Show();
        Dispatcher.UIThread.RunJobs();
        return (w, errors);
    }

    /// <summary>Types characters the way a Mac keyboard reports them (key + layout symbol).</summary>
    private static void Type(Window w, string text)
    {
        foreach (char c in text)
        {
            Key key = c switch
            {
                >= '0' and <= '9' => Key.D0 + (c - '0'),
                '.' => Key.OemPeriod, '+' => Key.OemPlus, '-' => Key.OemMinus, '*' => Key.D8, '/' => Key.OemQuestion,
                _ => Enum.Parse<Key>(char.ToUpperInvariant(c).ToString()),
            };
            var mods = c is '+' or '*' ? RawInputModifiers.Shift : RawInputModifiers.None;
            w.KeyPress(key, mods, PhysicalKey.None, c.ToString());
            w.KeyRelease(key, mods, PhysicalKey.None, c.ToString());
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window w, Key key, RawInputModifiers mods = RawInputModifiers.None)
    {
        w.KeyPress(key, mods, PhysicalKey.None, null);
        w.KeyRelease(key, mods, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void ClickMenu(MenuItem mi)
    {
        mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static MenuItem Find(ItemsControl parent, string header) =>
        parent.Items.OfType<MenuItem>().Single(m => (string?)m.Header == header);

    [AvaloniaFact]
    public void KeyboardDrivesCalculator()
    {
        var (w, _) = Open();
        Type(w, "3");
        Press(w, Key.Enter);
        Type(w, "4+5*");
        Assert.Equal("35.0000", w.XText);
        Assert.Equal("0.0000", w.YText);
    }

    [AvaloniaFact]
    public void NumericKeypadWorksWithoutSymbols()
    {
        var (w, _) = Open();
        Press(w, Key.NumPad1); Press(w, Key.Decimal); Press(w, Key.NumPad5);
        Press(w, Key.Enter);
        Press(w, Key.NumPad3);
        Press(w, Key.Multiply);
        Assert.Equal("4.5000", w.XText);
        Press(w, Key.NumPad9); Press(w, Key.Add);
        Assert.Equal("13.5000", w.XText);
        Press(w, Key.NumPad2); Press(w, Key.Divide);
        Assert.Equal("6.7500", w.XText);
        Press(w, Key.NumPad1); Press(w, Key.Subtract);
        Assert.Equal("5.7500", w.XText);
    }

    [AvaloniaFact]
    public void LetterShortcutsAndArrows()
    {
        var (w, _) = Open();
        Type(w, "1"); Press(w, Key.Enter); Type(w, "2"); Press(w, Key.Enter); Type(w, "3"); Press(w, Key.Enter); Type(w, "4");
        Press(w, Key.Down);   // roll down: X=3 Y=2 Z=1 T=4
        Assert.Equal(("3.0000", "2.0000", "1.0000", "4.0000"), (w.XText, w.YText, w.ZText, w.TText));
        Press(w, Key.Up);
        Assert.Equal("4.0000", w.XText);
        Type(w, "x");
        Assert.Equal(("3.0000", "4.0000"), (w.XText, w.YText));
        Type(w, "s");
        Assert.Equal("-3.0000", w.XText);
        Type(w, "p");
        Assert.Equal("3.1416", w.XText);
        Type(w, "+l");
        Assert.Equal("3.1416", w.XText);   // Last X
    }

    [AvaloniaFact]
    public void BackspaceEscapeAndEntryDisplay()
    {
        var (w, _) = Open();
        Type(w, "12.5");
        Assert.Equal("12.5", w.XText);       // raw entry shown while typing
        Press(w, Key.Back);
        Assert.Equal("12.", w.XText);
        Press(w, Key.Escape);
        Assert.Equal("0.0000", w.XText);
        Type(w, "7"); Press(w, Key.Delete);
        Assert.Equal("0.0000", w.XText);
    }

    [AvaloniaFact]
    public void ButtonsDoNotTakeFocusAndReturnStillEnters()
    {
        var (w, _) = Open();
        Assert.All(w.Buttons, b => Assert.False(b.Focusable));
        var seven = w.Buttons.Single(b => b.Content as string == "7");
        var p = seven.TranslatePoint(new Point(seven.Bounds.Width / 2, seven.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("7", w.XText);
        Press(w, Key.Enter);                 // must not re-click the "7" button
        Assert.Equal("7.0000", w.XText);
        Assert.Equal("7.0000", w.YText);
        Press(w, Key.Space);                 // swallowed
        Assert.Equal("7.0000", w.XText);
        Assert.Equal("7.0000", w.YText);
    }

    [AvaloniaFact]
    public void DivideByZeroReportsError()
    {
        var (w, errors) = Open();
        Type(w, "5"); Press(w, Key.Enter); Type(w, "0/");
        Assert.Equal([RpnCalculator.ErrDivideByZero], errors);
        Assert.Equal("5.0000", w.YText);
    }

    [AvaloniaFact]
    public void ContextMenuHasAllItems()
    {
        var (w, _) = Open();
        Assert.Same(w.Menu, w.ContextMenu);
        var headers = w.Menu.Items.Select(i => i is MenuItem m ? (string?)m.Header : "---").ToArray();
        Assert.Equal(["Show Stack", "Show Keypad", "Stay on Top", "Decimal Places", "Format Type", "Font Size",
                      "Conversions", "---", "Help", "About"], headers);
        var dec = Find(w.Menu, "Decimal Places");
        Assert.Equal(["0", "5", "1", "6", "2", "7", "3", "8", "4", "9"], dec.Items.OfType<MenuItem>().Select(m => (string)m.Header!));
        Assert.True(Find(dec, "4").IsChecked);
        Assert.Equal(7, Find(w.Menu, "Conversions").Items.Count);
        Assert.Equal("Grams → lbs./M", Find(w.Menu, "Conversions").Items.OfType<MenuItem>().Last().Header);
        Assert.True(Find(w.Menu, "Show Stack").IsChecked);
        Assert.True(Find(Find(w.Menu, "Format Type"), "Fixed").IsChecked);
    }

    [AvaloniaFact]
    public void MenuActionsChangeDisplayAndLayout()
    {
        var (w, _) = Open();
        double full = w.Height;
        ClickMenu(Find(w.Menu, "Show Stack"));
        Assert.False(w.StackVisible);
        Assert.True(w.Height < full);
        ClickMenu(Find(w.Menu, "Show Keypad"));
        Assert.False(w.KeypadVisible);
        double compact = w.Height;
        Assert.True(compact < full / 2);
        ClickMenu(Find(w.Menu, "Show Stack")); ClickMenu(Find(w.Menu, "Show Keypad"));
        Assert.Equal(full, w.Height);

        Type(w, "10");
        ClickMenu(Find(w.Menu, "Conversions").Items.OfType<MenuItem>().ElementAt(1));   // in³ → mL
        Assert.Equal("163.8706", w.XText);
        ClickMenu(Find(Find(w.Menu, "Decimal Places"), "2"));
        Assert.Equal("163.87", w.XText);
        ClickMenu(Find(Find(w.Menu, "Format Type"), "Scientific"));
        Assert.Equal("1.64E+02", w.XText);
        Assert.True(Find(Find(w.Menu, "Format Type"), "Scientific").IsChecked);
        Assert.False(Find(Find(w.Menu, "Format Type"), "Fixed").IsChecked);

        ClickMenu(Find(w.Menu, "Stay on Top"));
        Assert.True(w.Topmost);
        ClickMenu(Find(Find(w.Menu, "Font Size"), "Larger"));
        Assert.Equal(18, w.Settings.FontSize);
        Assert.True(w.Height > full);
    }

    [AvaloniaFact]
    public async Task CmdCopyAndPaste()
    {
        var (w, _) = Open();
        Type(w, "2"); Press(w, Key.Enter); Type(w, "3*");
        Press(w, Key.C, RawInputModifiers.Meta);
        Dispatcher.UIThread.RunJobs();
#pragma warning disable CS0618
        Assert.Equal("6", await w.Clipboard!.GetTextAsync());
        await w.Clipboard!.SetTextAsync("1,234.5");
#pragma warning restore CS0618
        Press(w, Key.V, RawInputModifiers.Meta);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("1234.5000", w.XText);
        Assert.Equal("6.0000", w.YText);
        // Ctrl+V works too; Cmd+letter is never typed as a calculator shortcut
        Press(w, Key.S, RawInputModifiers.Meta);
        Assert.Equal("1234.5000", w.XText);
    }

    [AvaloniaFact]
    public void CmdQSavesAndRelaunchRestores()
    {
        var (w, _) = Open();
        Type(w, "42"); Press(w, Key.Enter); Type(w, "7");
        ClickMenu(Find(Find(w.Menu, "Decimal Places"), "1"));
        ClickMenu(Find(w.Menu, "Show Keypad"));
        w.KeyPress(Key.Q, RawInputModifiers.Meta, PhysicalKey.None, "q");   // window is gone afterwards: no KeyRelease
        Dispatcher.UIThread.RunJobs();
        Assert.False(w.IsVisible);
        Assert.True(File.Exists(SettingsPath));
        var s = SettingsStore.Load(SettingsPath);
        Assert.Equal((7d, 42d, 1, false), (s.X, s.Y, s.Decimals, s.ShowKeypad));
        Assert.True(s.StackLift);

        var (w2, _) = Open();
        Assert.Equal(("7.0", "42.0"), (w2.XText, w2.YText));
        Assert.False(w2.KeypadVisible);
        Type(w2, "1");                         // completed number restored -> pushes
        Assert.Equal(("1", "7.0", "42.0"), (w2.XText, w2.YText, w2.ZText));
    }

    [AvaloniaFact]
    public void CorruptSettingsFileUsesDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, "{ broken");
        var (w, _) = Open();
        Assert.Equal("0.0000", w.XText);
        Assert.True(w.StackVisible && w.KeypadVisible);
    }

    [AvaloniaFact]
    public void OffScreenPositionIsMovedOnScreen()
    {
        Directory.CreateDirectory(_dir);
        SettingsStore.Save(new AppSettings { Left = -50000, Top = -50000 }, SettingsPath);
        var (w, _) = Open();
        Assert.True(w.Position.X > -50000 && w.Position.Y > -50000);
    }

    [AvaloniaFact]
    public void RendersFrameForVisualCheck()
    {
        var (w, _) = Open();
        Type(w, "3"); Press(w, Key.Enter); Type(w, "4+");
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string outDir = Environment.GetEnvironmentVariable("RPNCALC_SNAPSHOT_DIR") ?? _dir;
        Directory.CreateDirectory(outDir);
        frame!.Save(Path.Combine(outDir, "mainwindow.png"));
        Assert.True(frame.PixelSize.Width >= 200);
    }

    [AvaloniaFact]
    public void RollButtonsShowOnlyBoldArrowsAndKeepNamesAndFunction()
    {
        var (w, _) = Open();
        var up = w.Buttons.Single(b => (CalcCommand)b.Tag! == CalcCommand.RollUp);
        var down = w.Buttons.Single(b => (CalcCommand)b.Tag! == CalcCommand.RollDown);
        foreach (var (b, name, data) in new[] { (up, "Roll Up", MainWindow.ArrowUpData), (down, "Roll Down", MainWindow.ArrowDownData) })
        {
            var path = Assert.IsType<Avalonia.Controls.Shapes.Path>(b.Content);   // no text on the face
            Assert.Equal(name, Avalonia.Automation.AutomationProperties.GetName(b));
            Assert.Equal(name + (name == "Roll Up" ? " (Up Arrow)" : " (Down Arrow)"), ToolTip.GetTip(b));
            Assert.NotNull(path.Fill);                                            // follows the text colour
            Assert.True(path.Bounds.Height >= 14 && path.Bounds.Height < b.Bounds.Height, $"{path.Bounds}");
            Assert.False(b.Focusable);
        }
        // Up arrow: tip (narrow) at the top; down arrow: tip at the bottom.
        var upGeo = Avalonia.Media.Geometry.Parse(MainWindow.ArrowUpData);
        Assert.True(upGeo.FillContains(new Point(5, 0.5)) && !upGeo.FillContains(new Point(1, 11.5)));
        var downGeo = Avalonia.Media.Geometry.Parse(MainWindow.ArrowDownData);
        Assert.True(downGeo.FillContains(new Point(5, 11.5)) && !downGeo.FillContains(new Point(1, 0.5)));

        // Buttons and keys still roll the stack.
        Type(w, "1"); Press(w, Key.Enter); Type(w, "2"); Press(w, Key.Enter); Type(w, "3");
        up.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(new double[] { 0, 3, 2, 1 }, new[] { w.Calculator.X, w.Calculator.Y, w.Calculator.Z, w.Calculator.T });
        down.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(3, w.Calculator.X);
        Press(w, Key.Down);
        Assert.Equal(2, w.Calculator.X);

        // Retina-style 2x render of the window content for a visual check.
        string? outDir = Environment.GetEnvironmentVariable("RPNCALC_SNAPSHOT_DIR");
        if (outDir is not null)
        {
            var root = (Control)w.Content!;
            using var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(
                new PixelSize((int)(root.Bounds.Width * 2), (int)(root.Bounds.Height * 2)), new Vector(192, 192));
            rtb.Render(root);
            Directory.CreateDirectory(outDir);
            rtb.Save(Path.Combine(outDir, "mainwindow@2x.png"));
        }
    }

    [AvaloniaFact]
    public void HelpTextMentionsMacKeys()
    {
        string t = HelpText.Build();
        Assert.Contains("Cmd+Q", t);
        Assert.Contains("Library/Application Support/RPNCalc/settings.json", t);
        Assert.Contains("Grams → lbs./M", t);
    }
}
