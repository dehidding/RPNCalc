using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace RPNCalc.Mac.Tests;

public class LayoutFitTests
{
    [AvaloniaTheory]
    [InlineData(16, true, true)]
    [InlineData(24, true, true)]
    [InlineData(8, false, true)]
    [InlineData(16, true, false)]
    [InlineData(36, false, false)]
    public void WindowHeightFitsContentExactly(int fontSize, bool stack, bool keypad)
    {
        string path = Path.Combine(Path.GetTempPath(), "rpncalc_fit_" + Guid.NewGuid().ToString("N") + ".json");
        RPNCalc.Engine.SettingsStore.Save(new RPNCalc.Engine.AppSettings { FontSize = fontSize, ShowStack = stack, ShowKeypad = keypad }, path);
        var w = new MainWindow(path);
        w.Show();
        Dispatcher.UIThread.RunJobs();
        var root = (Control)w.Content!;
        root.Measure(new Size(w.Width, double.PositiveInfinity));
        Assert.Equal(Math.Ceiling(root.DesiredSize.Height), w.Height);
        Assert.Equal(w.Height, w.ClientSize.Height);
        // the lowest visible button is fully inside the client area
        var last = keypad ? w.Buttons[^1] : w.Buttons[3];
        var bottom = last.TranslatePoint(new Point(0, last.Bounds.Height), w)!.Value.Y;
        Assert.True(bottom <= w.ClientSize.Height - 2, $"button bottom {bottom} vs client {w.ClientSize.Height}");
        w.Close();
        File.Delete(path);
    }
}
