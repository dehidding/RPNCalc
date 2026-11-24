using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using RPNCalc.Engine;

namespace RPNCalc.Droid;

/// <summary>Full-screen overlay pages (Help, About, editor) and a modal message card.</summary>
internal static class Overlays
{
    internal static readonly FontFamily MonoFont = new("Droid Sans Mono, monospace, DejaVu Sans Mono, Courier New");
    private static readonly IBrush BarBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3F, 0x46));

    internal static string AppVersion
    {
        get
        {
            var asm = typeof(Overlays).Assembly;
            return asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
                   ?? asm.GetName().Version?.ToString() ?? "1.2.0";
        }
    }

    internal static Bitmap? AppImage()
    {
        try
        {
            string asm = typeof(Overlays).Assembly.GetName().Name!;
            return new Bitmap(AssetLoader.Open(new Uri($"avares://{asm}/Assets/rpncalc.png")));
        }
        catch { return null; }
    }

    internal static Button BarButton(string text, Action onClick)
    {
        var b = new Button
        {
            Content = text, FontSize = 22, FontWeight = FontWeight.Bold, Width = 52, Height = 52,
            Background = Brushes.Transparent, Foreground = Brushes.White, Focusable = false, IsTabStop = false,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    /// <summary>A page with a top bar (← back + title) covering the calculator. Back gesture also closes it.</summary>
    internal static Control Page(string title, Control body, Action close)
    {
        var back = BarButton("←", close);
        Avalonia.Automation.AutomationProperties.SetName(back, "Back");
        var bar = new DockPanel { Background = BarBrush, Height = 52 };
        DockPanel.SetDock(back, Dock.Left);
        bar.Children.Add(back);
        bar.Children.Add(new TextBlock
        {
            Text = title, FontSize = 20, FontWeight = FontWeight.Bold, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var dock = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF4)) };
        DockPanel.SetDock(bar, Dock.Top);
        dock.Children.Add(bar);
        dock.Children.Add(body);
        return dock;
    }

    internal static Control HelpBody(string text) => new Border
    {
        Background = Brushes.White,
        Child = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new SelectableTextBlock { Text = text, FontFamily = MonoFont, FontSize = 12, Margin = new Thickness(12) },
        },
    };

    internal static Control AboutBody()
    {
        var img = new Image { Source = AppImage(), Width = 96, Height = 96, Margin = new Thickness(0, 8, 0, 8) };
        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(24), Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 480,
                Children =
                {
                    img,
                    new TextBlock { Text = "RPN Calculator", FontSize = 22, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = "Version " + AppVersion, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = "Copyright © David Hidding 2026", HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock
                    {
                        Text = "HP-style Reverse Polish Notation calculator for Android, reconstructed from the 1997 Delphi RPNCalc. "
                               + "Settings, stack and conversions are kept in the app's private storage.",
                        TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 8, 0, 0),
                    },
                },
            },
        };
    }

    /// <summary>Error message card on a dimmed background (Back, Esc or OK closes it).</summary>
    internal static Control Message(string message, Func<bool> close)
    {
        var ok = new Button { Content = "OK", MinWidth = 96, HorizontalAlignment = HorizontalAlignment.Right, Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Center };
        ok.Click += (_, _) => close();
        var card = new Border
        {
            Background = Brushes.White, CornerRadius = new CornerRadius(8), Padding = new Thickness(20, 16),
            Margin = new Thickness(24), MaxWidth = 420, VerticalAlignment = VerticalAlignment.Center,
            BoxShadow = BoxShadows.Parse("0 4 16 0 #60000000"),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                    ok,
                },
            },
        };
        var shade = new Panel { Background = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)), Children = { card } };
        shade.PointerPressed += (_, e) => { if (ReferenceEquals(e.Source, shade)) close(); };
        return shade;
    }
}

/// <summary>
/// In-app editor for conversions.csv: plain CSV text with live validation (skipped lines are listed),
/// Save, Reset to defaults, and Import / Export through the system file picker (Storage Access Framework).
/// </summary>
internal sealed class ConversionsEditor : DockPanel
{
    private readonly TextBox _text;
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13, Margin = new Thickness(0, 4) };
    private readonly Func<IStorageProvider?> _storage;

    public event Action<string>? Saved;
    public event Action? Cancelled;

    internal string Text => _text.Text ?? "";

    public ConversionsEditor(string csv, Func<IStorageProvider?> storage)
    {
        _storage = storage;
        _text = new TextBox
        {
            Text = csv, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, FontFamily = Overlays.MonoFont, FontSize = 13,
            VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_text, ScrollBarVisibility.Auto);
        _text.TextChanged += (_, _) => Validate();

        Button B(string text, Action a)
        {
            var b = new Button { Content = text, Focusable = false, Margin = new Thickness(0, 0, 8, 8), MinHeight = 40 };
            b.Click += (_, _) => a();
            return b;
        }
        var buttons = new WrapPanel
        {
            Children =
            {
                B("Save", () => Saved?.Invoke(Text)),
                B("Cancel", () => Cancelled?.Invoke()),
                B("Reset to defaults", () => _text.Text = Conversions.DefaultCsv),
                B("Import...", () => _ = Import()),
                B("Export...", () => _ = Export()),
            },
        };
        var hint = new TextBlock
        {
            Text = "One conversion per line: From,To,Factor  (X × Factor). Use \".\" as decimal point; "
                   + "quote names that contain a comma; # starts a comment.",
            TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = Brushes.DimGray,
        };
        var top = new StackPanel { Margin = new Thickness(12, 12, 12, 0), Children = { buttons, hint, _status } };
        DockPanel.SetDock(top, Dock.Top);
        Children.Add(top);
        Children.Add(new Border { Margin = new Thickness(12, 4, 12, 12), Child = _text });
        Validate();
    }

    internal void ShowStatus(string message, bool error)
    {
        _status.Text = message;
        _status.Foreground = error ? Brushes.Firebrick : Brushes.DarkGreen;
    }

    private void Validate()
    {
        var (items, warnings) = Conversions.Parse(Text);
        if (warnings.Count == 0)
            ShowStatus($"✓ {items.Count} conversion(s), no problems.", error: false);
        else
        {
            var sb = new StringBuilder($"⚠ {items.Count} conversion(s); {warnings.Count} line(s) will be skipped:");
            foreach (var w in warnings.Take(8)) sb.Append('\n').Append(w);
            if (warnings.Count > 8) sb.Append($"\n… and {warnings.Count - 8} more.");
            ShowStatus(sb.ToString(), error: true);
        }
    }

    private static readonly FilePickerFileType CsvType = new("CSV file")
    {
        Patterns = ["*.csv", "*.txt"],
        MimeTypes = ["text/csv", "text/comma-separated-values", "text/plain"],
    };

    private async Task Import()
    {
        try
        {
            if (_storage() is not { CanOpen: true } sp) { ShowStatus("File picker not available.", true); return; }
            var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import conversions", AllowMultiple = false, FileTypeFilter = [CsvType, FilePickerFileTypes.All],
            });
            if (files.Count == 0) return;
            await using var s = await files[0].OpenReadAsync();
            using var r = new StreamReader(s, Encoding.UTF8);
            _text.Text = await r.ReadToEndAsync();      // validated live; nothing is saved until Save
        }
        catch (Exception ex) { ShowStatus("Import failed: " + ex.Message, true); }
    }

    private async Task Export()
    {
        try
        {
            if (_storage() is not { CanSave: true } sp) { ShowStatus("File picker not available.", true); return; }
            var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export conversions", SuggestedFileName = Conversions.FileName, DefaultExtension = "csv",
                FileTypeChoices = [CsvType], ShowOverwritePrompt = true,
            });
            if (file is null) return;
            await using var s = await file.OpenWriteAsync();
            if (s.CanSeek) { try { s.SetLength(0); } catch (NotSupportedException) { } }
            var bytes = new UTF8Encoding(false).GetBytes(Text);
            await s.WriteAsync(bytes);
            ShowStatus("Exported to " + file.Name, false);
        }
        catch (Exception ex) { ShowStatus("Export failed: " + ex.Message, true); }
    }
}
