using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RPNCalc.Engine;

namespace RPNCalc.Mac;

/// <summary>Closes on Esc and Cmd+W (Ctrl+W elsewhere).</summary>
public abstract class DialogBase : Window
{
    protected DialogBase()
    {
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF4));
        AddHandler(KeyDownEvent, (_, e) =>
        {
            bool cmd = (e.KeyModifiers & (KeyModifiers.Meta | KeyModifiers.Control)) != 0;
            if (e.Key == Key.Escape || (cmd && e.Key == Key.W) || ExtraCloseKey(e)) { e.Handled = true; Close(); }
        }, RoutingStrategies.Tunnel);
    }

    protected virtual bool ExtraCloseKey(KeyEventArgs e) => false;

    protected static Button OkButton(string text, Action onClick)
    {
        var b = new Button { Content = text, MinWidth = 85, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
        b.Click += (_, _) => onClick();
        return b;
    }

    internal static Bitmap? AppImage()
    {
        try { return new Bitmap(AssetLoader.Open(new Uri("avares://RPNCalc/Assets/rpncalc.png"))); }
        catch { return null; }
    }
}

/// <summary>Modal error box (Avalonia has no built-in message box).</summary>
public sealed class MessageWindow : DialogBase
{
    public MessageWindow(string message)
    {
        Title = MainWindow.AppTitle;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        var ok = OkButton("OK", Close);
        ok.HorizontalAlignment = HorizontalAlignment.Right;
        Content = new StackPanel
        {
            Margin = new Thickness(20, 16), Spacing = 16, MinWidth = 220,
            Children =
            {
                new TextBlock { Text = message, FontSize = 14, FontWeight = FontWeight.SemiBold },
                ok,
            },
        };
        Opened += (_, _) => ok.Focus();
    }

    protected override bool ExtraCloseKey(KeyEventArgs e) => e.Key == Key.Enter;

    public static Task Show(Window owner, string message) => new MessageWindow(message).ShowDialog(owner);
}

/// <summary>About box: Product Name, Version, Copyright, Comments and OK (as in the original).</summary>
public sealed class AboutWindow : DialogBase
{
    public AboutWindow()
    {
        var asm = Assembly.GetExecutingAssembly();
        string version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
                         ?? asm.GetName().Version?.ToString() ?? "1.1.1";
        Title = "About RPNCalc";
        Width = 380;
        SizeToContent = SizeToContent.Height;
        CanResize = false;

        var text = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "RPN Calculator", FontSize = 16, FontWeight = FontWeight.Bold },
                new TextBlock { Text = "Version " + version },
                new TextBlock { Text = "Copyright: original Delphi version 1997;\nre-implementation 2026", TextWrapping = TextWrapping.Wrap },
                new TextBlock
                {
                    Text = "HP-style Reverse Polish Notation calculator for macOS. Settings and stack are saved in "
                           + SettingsStore.DefaultPath.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "~"),
                    TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray,
                },
            },
        };
        var ok = OkButton("OK", Close);
        ok.HorizontalAlignment = HorizontalAlignment.Right;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(16) };
        var img = new Image { Source = AppImage(), Width = 48, Height = 48, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 16, 0) };
        RenderOptions.SetBitmapInterpolationMode(img, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
        grid.Children.Add(img);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        Content = new StackPanel { Children = { grid, new Border { Padding = new Thickness(16, 0, 16, 16), Child = ok } } };
        Opened += (_, _) => ok.Focus();
    }

    protected override bool ExtraCloseKey(KeyEventArgs e) => e.Key == Key.Enter;
}

/// <summary>Modeless Help window (F1, Cmd+?, right-click → Help, or app menu → RPNCalc Help).</summary>
public sealed class HelpWindow : DialogBase
{
    public HelpWindow()
    {
        Title = "RPN Calculator Help";
        Width = 620; Height = 620;
        MinWidth = 360; MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var close = OkButton("Close", Close);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        var body = new SelectableTextBlock
        {
            Text = HelpText.Build(),
            FontFamily = new FontFamily("Menlo, Monaco, Consolas, DejaVu Sans Mono, monospace"),
            FontSize = 12.5,
            Margin = new Thickness(12),
        };
        var dock = new DockPanel();
        var bottom = new Border { Padding = new Thickness(8), Child = close };
        DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(bottom);
        dock.Children.Add(new Border
        {
            Background = Brushes.White,
            Child = new ScrollViewer
            {
                Content = body,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            },
        });
        Content = dock;
    }

    protected override bool ExtraCloseKey(KeyEventArgs e) => e.Key == Key.F1;
}
