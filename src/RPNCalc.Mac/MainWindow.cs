using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using RPNCalc.Engine;
using RPNCalc.Shared;

namespace RPNCalc.Mac;

/// <summary>
/// Main calculator window - same layout, menu and keyboard behaviour as the Windows (WinForms) version.
/// Avalonia lays out in device-independent units and renders through Skia at the screen's backing
/// scale factor, so it is crisp on Retina / High-DPI displays without any manual scaling.
/// </summary>
public sealed class MainWindow : Window
{
    internal const string AppTitle = "RPN Calculator";
    private const double DefaultWidth = 240, MinLogicalWidth = 200, ButtonHeight = 26, Gap = 4, Margin4 = 4;   // even gap: whole-pixel margins
    internal static readonly FontFamily RegisterFont = new("Arial, Helvetica, Liberation Sans, DejaVu Sans");

    private readonly RpnCalculator _calc = new();
    private readonly AppSettings _settings;
    private readonly string? _settingsPath;

    private readonly StackPanel _root = new() { Orientation = Orientation.Vertical };
    private readonly Grid _pStack = new(), _pMain = new(), _pKeypad = new();
    private readonly TextBlock _regT = RegText(), _regZ = RegText(), _regY = RegText(), _regX = RegText();
    private readonly List<Border> _regBorders = [];
    private readonly List<Button> _buttons = [];
    private Grid _xKeyRow = null!;        // Enter | +/- | Last X | <  (hidden together with the keypad)

    private readonly ContextMenu _menu = new();
    private MenuItem _miShowStack = null!, _miShowKeypad = null!, _miStayOnTop = null!, _miFixed = null!, _miScientific = null!;
    private readonly MenuItem[] _miDecimals = new MenuItem[10];
    private readonly MenuItem _convMenu = new() { Header = "Conversions" };
    private ConversionTable _conversions = new([], [], "");

    /// <summary>conversions.csv, next to settings.json.</summary>
    internal string ConversionsPath => Conversions.GetPath(_settingsPath);
    internal ConversionTable LoadedConversions => _conversions;

    private HelpWindow? _help;
    private bool _saved;

    /// <summary>Shows calculator errors; replaceable for tests.</summary>
    internal Func<string, Task> ErrorHandler { get; set; }

    public MainWindow() : this(null) { }

    /// <param name="settingsPath">Settings file; null = platform default (~/Library/Application Support/RPNCalc on macOS).</param>
    public MainWindow(string? settingsPath)
    {
        _settingsPath = settingsPath;
        _settings = SettingsStore.Load(settingsPath);
        _calc.SetState(_settings.X, _settings.Y, _settings.Z, _settings.T, _settings.LastX, _settings.StackLift);
        ErrorHandler = msg => MessageWindow.Show(this, msg);

        Title = AppTitle;
        Icon = LoadIcon();
        CanResize = true;
        SizeToContent = SizeToContent.Manual;
        Width = Math.Max(MinLogicalWidth, _settings.Width ?? DefaultWidth);
        MinWidth = MinLogicalWidth;
        MaxWidth = 2000;
        Topmost = _settings.StayOnTop;
        Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xEC));
        Focusable = true;

        BuildStack();
        BuildMain();
        BuildKeypad();
        _root.Children.AddRange([_pStack, _pMain, _pKeypad]);
        Content = _root;

        BuildMenu();
        ContextMenu = _menu;

        // Tunnel handlers see keys/clicks before any child control, so buttons can never swallow them.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);

        if (_settings.Left is int l && _settings.Top is int t)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(l, t);
        }
        else WindowStartupLocation = WindowStartupLocation.CenterScreen;

        ApplyLayout();
        UpdateDisplay();
        Opened += (_, _) => { ApplyLayout(); EnsureOnScreen(); Focus(); };   // re-measure with layout rounding of the real screen
    }

    // ------------------------------------------------------------------ test accessors
    internal string XText => _regX.Text ?? "";
    internal string YText => _regY.Text ?? "";
    internal string ZText => _regZ.Text ?? "";
    internal string TText => _regT.Text ?? "";
    internal RpnCalculator Calculator => _calc;
    internal AppSettings Settings => _settings;
    internal ContextMenu Menu => _menu;
    internal bool StackVisible => _pStack.IsVisible;
    internal bool KeypadVisible => _pKeypad.IsVisible;
    internal bool KeyRowVisible => _xKeyRow.IsVisible;
    internal IReadOnlyList<Button> Buttons => _buttons;

    // ------------------------------------------------------------------ construction

    private static TextBlock RegText() => new()
    {
        FontFamily = RegisterFont, FontWeight = FontWeight.Bold, Foreground = Brushes.Black,
        TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
    };

    private Control RegisterRow(string tag, TextBlock text)
    {
        var tagBlock = new TextBlock
        {
            Text = tag, Width = 14, FontFamily = RegisterFont, FontWeight = FontWeight.Bold, FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        // Viewbox(DownOnly) shrinks long numbers so they always fit, like the Windows version.
        var box = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
            BorderThickness = new Thickness(1.5, 1.5, 0.5, 0.5),   // sunken look
            Padding = new Thickness(4, 0, 4, 0),
            Child = new Viewbox
            {
                Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Right, Child = text,
            },
        };
        ToolTip.SetTip(box, tag + " register");
        _regBorders.Add(box);
        var row = new DockPanel { Margin = new Thickness(Margin4, 0, Margin4, Gap) };
        DockPanel.SetDock(tagBlock, Dock.Left);
        row.Children.Add(tagBlock);
        row.Children.Add(box);
        return row;
    }

    private void BuildStack()
    {
        var p = new StackPanel { Margin = new Thickness(0, Margin4, 0, 0) };
        p.Children.AddRange([RegisterRow("T", _regT), RegisterRow("Z", _regZ), RegisterRow("Y", _regY)]);
        _pStack.Children.Add(p);
    }

    /// <summary>Key label size: about 62% of the 26 px key height.</summary>
    internal const double ButtonFontSize = 16;

    /// <summary>
    /// Shows a text label at the button's (large, bold) font, scaled down only when the key is too narrow
    /// for it, so labels like "Last X" never clip on a narrow window.
    /// </summary>
    private static readonly IDataTemplate FittedLabel = new FuncDataTemplate<string>((text, _) => new Viewbox
    {
        Stretch = Stretch.Uniform,
        StretchDirection = StretchDirection.DownOnly,
        Margin = new Thickness(2, 1),            // just clear of the 1 px border: long labels shrink as little as possible
        Child = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.NoWrap,      // never break "X<>Y" / "y^x" onto two lines
            TextTrimming = TextTrimming.None,
            VerticalAlignment = VerticalAlignment.Center,
        },
    });

    private Button MakeButton(string text, CalcCommand cmd, string tip)
    {
        var b = new Button
        {
            Content = text,
            Focusable = false,                 // buttons never take keyboard focus
            IsTabStop = false,
            Height = ButtonHeight,
            Padding = new Thickness(0),
            Margin = new Thickness(Gap / 2),
            FontFamily = RegisterFont,
            FontWeight = FontWeight.Bold,
            FontSize = ButtonFontSize,         // large bold labels (was 11); roll arrows are sized separately
            ContentTemplate = FittedLabel,
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Tag = cmd,
        };
        b.Click += (_, _) => Run(cmd);
        ToolTip.SetTip(b, tip);
        _buttons.Add(b);
        return b;
    }

    /// <summary>Bold arrow outline in a 10 x 12 unit box: wide head, thick shaft (same shape as on Windows).</summary>
    internal const string ArrowUpData = KeyMapper.ArrowUpData;
    internal const string ArrowDownData = KeyMapper.ArrowDownData;

    /// <summary>
    /// Replaces the button text with a vector arrow (crisp at any Retina scale). The accessible name keeps
    /// the text, and the fill follows the button's text colour (pressed/disabled states).
    /// </summary>
    private static void SetArrowFace(Button b, string name, string pathData)
    {
        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(pathData),
            Stretch = Stretch.Uniform,
            Height = Math.Round(ButtonHeight * 0.58),
            Width = Math.Round(ButtonHeight * 0.58 * 10 / 12),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = Brushes.Black,
        };
        path.Bind(Avalonia.Controls.Shapes.Shape.FillProperty, new Binding("Foreground")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ContentPresenter) },
        });
        b.ContentTemplate = null;          // the arrow keeps its own size; no text label template
        b.Content = path;
        AutomationProperties.SetName(b, name);
    }

    private void BuildMain()
    {
        var p = new StackPanel { Margin = new Thickness(0, Margin4, 0, Margin4) };
        p.Children.Add(RegisterRow("X", _regX));
        // Enter | +/- | Last X | <  with relative widths 1.8 : 1 : 1.4 : 0.8 (as on Windows).
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.8*,1*,1.4*,0.8*"),
            Margin = new Thickness(Margin4 - Gap / 2, 0, Margin4 - Gap / 2, 0),
        };
        Button[] bs =
        [
            MakeButton("Enter", CalcCommand.Enter, "Enter (Return key)"),
            MakeButton("+/-", CalcCommand.ChangeSign, "Change sign (S)"),
            MakeButton("Last X", CalcCommand.LastX, "Last X (L)"),
            MakeButton("<", CalcCommand.Backspace, "Delete last digit (Backspace / delete)"),
        ];
        for (int i = 0; i < bs.Length; i++) { Grid.SetColumn(bs[i], i); row.Children.Add(bs[i]); }
        p.Children.Add(row);
        _xKeyRow = row;
        _pMain.Children.Add(p);
    }

    private void BuildKeypad()
    {
        var g = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"),
            Margin = new Thickness(Margin4 - Gap / 2, 0, Margin4 - Gap / 2, Margin4),
        };
        void Add(string text, CalcCommand cmd, string tip, int row, int col, int span = 1, string? arrow = null)
        {
            var b = MakeButton(text, cmd, tip);
            if (arrow is not null) SetArrowFace(b, text, arrow);
            Grid.SetRow(b, row); Grid.SetColumn(b, col); Grid.SetColumnSpan(b, span);
            g.Children.Add(b);
        }

        Add("1/x", CalcCommand.Reciprocal, "Reciprocal 1/x", 0, 0);
        Add("y^x", CalcCommand.Power, "Power: Y raised to X", 0, 1);
        Add("x²", CalcCommand.Square, "Square", 0, 2);
        Add("√x", CalcCommand.SquareRoot, "Square root", 0, 3);
        Add("π", CalcCommand.Pi, "Pi (P)", 0, 4);

        Add("−", CalcCommand.Subtract, "Subtract (-)", 1, 0);
        Add("+", CalcCommand.Add, "Add (+)", 2, 0);
        Add("×", CalcCommand.Multiply, "Multiply (*)", 3, 0);
        Add("÷", CalcCommand.Divide, "Divide (/)", 4, 0);

        string[] digits = ["7", "8", "9", "4", "5", "6", "1", "2", "3"];
        for (int i = 0; i < 9; i++)
            Add(digits[i], CalcCommand.D0 + int.Parse(digits[i], CultureInfo.InvariantCulture), digits[i], 1 + i / 3, 1 + i % 3);
        Add("0", CalcCommand.D0, "0", 4, 1, 2);
        Add(".", CalcCommand.Decimal, "Decimal point (.)", 4, 3);

        Add("X<>Y", CalcCommand.Swap, "Exchange X and Y (X)", 1, 4);
        Add("Roll Up", CalcCommand.RollUp, "Roll Up (Up Arrow)", 2, 4, arrow: ArrowUpData);
        Add("Roll Down", CalcCommand.RollDown, "Roll Down (Down Arrow)", 3, 4, arrow: ArrowDownData);
        _pKeypad.Children.Add(g);
    }

    private void BuildMenu()
    {
        MenuItem Item(string header, Action onClick, MenuItemToggleType toggle = MenuItemToggleType.None, string? group = null)
        {
            var mi = new MenuItem { Header = header, ToggleType = toggle };
            if (group is not null) mi.GroupName = group;
            mi.Click += (_, _) => { onClick(); SyncMenuChecks(); };
            return mi;
        }

        _miShowStack = Item("Show Stack", () => { _settings.ShowStack = !_settings.ShowStack; ApplyLayout(); }, MenuItemToggleType.CheckBox);
        _miShowKeypad = Item("Show Keypad", () => { _settings.ShowKeypad = !_settings.ShowKeypad; ApplyLayout(); }, MenuItemToggleType.CheckBox);
        _miStayOnTop = Item("Stay on Top", () => { _settings.StayOnTop = !_settings.StayOnTop; Topmost = _settings.StayOnTop; }, MenuItemToggleType.CheckBox);

        // Decimal Places: two columns 0-4 | 5-9 (UniformGrid fills row by row, so interleave).
        var decMenu = new MenuItem
        {
            Header = "Decimal Places",
            ItemsPanel = new FuncTemplate<Panel?>(() => new UniformGrid { Columns = 2 }),
        };
        for (int i = 0; i < 10; i++)
        {
            int d = i;
            _miDecimals[i] = Item(i.ToString(CultureInfo.InvariantCulture), () => { _settings.Decimals = d; UpdateDisplay(); },
                                  MenuItemToggleType.Radio, "decimals");
        }
        for (int r = 0; r < 5; r++) { decMenu.Items.Add(_miDecimals[r]); decMenu.Items.Add(_miDecimals[r + 5]); }

        _miFixed = Item("Fixed", () => { _settings.Format = FormatType.Fixed; UpdateDisplay(); }, MenuItemToggleType.Radio, "format");
        _miScientific = Item("Scientific", () => { _settings.Format = FormatType.Scientific; UpdateDisplay(); }, MenuItemToggleType.Radio, "format");
        var fmtMenu = new MenuItem { Header = "Format Type", Items = { _miFixed, _miScientific } };

        var fontMenu = new MenuItem
        {
            Header = "Font Size",
            Items = { Item("Larger", () => ChangeFontSize(+2)), Item("Smaller", () => ChangeFontSize(-2)) },
        };

        RebuildConversionsMenu();
        var convMenu = _convMenu;

        var help = Item("Help", ShowHelp);
        help.InputGesture = new KeyGesture(Key.F1);
        _menu.Items.Add(_miShowStack);
        _menu.Items.Add(_miShowKeypad);
        _menu.Items.Add(_miStayOnTop);
        _menu.Items.Add(decMenu);
        _menu.Items.Add(fmtMenu);
        _menu.Items.Add(fontMenu);
        _menu.Items.Add(convMenu);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(help);
        _menu.Items.Add(Item("About", ShowAbout));
        _menu.Opening += (_, _) => { RebuildConversionsMenu(); SyncMenuChecks(); };
        SyncMenuChecks();
    }

    /// <summary>Re-reads conversions.csv so edits show up the next time the menu opens.</summary>
    internal void RebuildConversionsMenu()
    {
        _conversions = Conversions.Load(ConversionsPath);
        _convMenu.Items.Clear();
        foreach (var conv in _conversions.Items)
        {
            var c = conv;
            var mi = new MenuItem { Header = c.MenuText };
            ToolTip.SetTip(mi, "× " + c.Factor.ToString("R", CultureInfo.InvariantCulture));
            mi.Click += (_, _) => Report(_calc.Convert(c.Factor));
            _convMenu.Items.Add(mi);
        }
        if (_conversions.Items.Count == 0)
            _convMenu.Items.Add(new MenuItem { Header = "(no conversions in the file)", IsEnabled = false });
        _convMenu.Items.Add(new Separator());
        if (_conversions.Warnings.Count > 0)
        {
            var warn = new MenuItem { Header = $"⚠ {_conversions.Warnings.Count} line(s) skipped (see Help)" };
            warn.Click += (_, _) => { _help?.Close(); ShowHelp(); };
            _convMenu.Items.Add(warn);
        }
        var edit = new MenuItem { Header = "Edit Conversions..." };
        edit.Click += (_, _) => EditConversions();
        _convMenu.Items.Add(edit);
    }

    private void EditConversions()
    {
        try
        {
            if (!File.Exists(ConversionsPath)) Conversions.Load(ConversionsPath);   // creates the default file
            if (OperatingSystem.IsMacOS())
                System.Diagnostics.Process.Start("open", ["-t", ConversionsPath]);   // default text editor (TextEdit)
            else
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ConversionsPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _ = ErrorHandler($"Could not open {ConversionsPath}: {ex.Message}");
        }
    }

    private void SyncMenuChecks()
    {
        _miShowStack.IsChecked = _settings.ShowStack;
        _miShowKeypad.IsChecked = _settings.ShowKeypad;
        _miStayOnTop.IsChecked = _settings.StayOnTop;
        for (int i = 0; i < 10; i++) _miDecimals[i].IsChecked = _settings.Decimals == i;
        _miFixed.IsChecked = _settings.Format == FormatType.Fixed;
        _miScientific.IsChecked = _settings.Format == FormatType.Scientific;
    }

    private static WindowIcon? LoadIcon()
    {
        try { return new WindowIcon(AssetLoader.Open(new Uri("avares://RPNCalc/Assets/rpncalc.png"))); }
        catch { return null; }
    }

    // ------------------------------------------------------------------ actions

    internal void Run(CalcCommand cmd) => Report(_calc.Execute(cmd));

    private void Report(string? error)
    {
        UpdateDisplay();
        if (error is not null) _ = ErrorHandler(error);
    }

    private void ChangeFontSize(int delta)
    {
        _settings.FontSize = Math.Clamp(_settings.FontSize + delta, AppSettings.MinFontSize, AppSettings.MaxFontSize);
        ApplyLayout();
    }

    public void ShowHelp()
    {
        if (_help is null || !_help.IsVisible)
        {
            _conversions = Conversions.Load(ConversionsPath);
            _help = new HelpWindow(_conversions);
            _help.Closed += (_, _) => _help = null;
            _help.Show(this);
        }
        else
        {
            if (_help.WindowState == WindowState.Minimized) _help.WindowState = WindowState.Normal;
            _help.Activate();
        }
    }

    public void ShowAbout() => _ = new AboutWindow().ShowDialog(this);

    internal async Task CopyX()
    {
        if (Clipboard is { } cb) await cb.SetTextAsync(_calc.X.ToString("R", CultureInfo.InvariantCulture));
    }

    internal async Task PasteX()
    {
        string text = "";
        try
        {
#pragma warning disable CS0618 // GetTextAsync is fine for plain text
            if (Clipboard is { } cb) text = (await cb.GetTextAsync())?.Trim().Replace(",", "") ?? "";
#pragma warning restore CS0618
        }
        catch { /* clipboard unavailable */ }
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && double.IsFinite(v))
        {
            _calc.PushValue(v);
            UpdateDisplay();
        }
    }

    // ------------------------------------------------------------------ keyboard

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (HandleKey(e.Key, e.KeyModifiers, e.KeySymbol)) e.Handled = true;
    }

    /// <summary>
    /// Keyboard mapping shared by all platforms. Cmd (Meta) or Ctrl + C/V copies/pastes X,
    /// Cmd+Q quits (saving). Character keys use the layout-aware KeySymbol, so '+', '*' etc. work on
    /// any keyboard layout; numeric-keypad keys are matched by key code so they work even without a symbol.
    /// </summary>
    internal bool HandleKey(Key key, KeyModifiers mods, string? symbol)
    {
        bool cmdOrCtrl = (mods & (KeyModifiers.Meta | KeyModifiers.Control)) != 0;
        if (cmdOrCtrl)
        {
            switch (key)
            {
                case Key.C: case Key.Insert when (mods & KeyModifiers.Control) != 0: _ = CopyX(); return true;
                case Key.V: _ = PasteX(); return true;
                case Key.Q when (mods & KeyModifiers.Meta) != 0: Close(); return true;
                case Key.W when (mods & KeyModifiers.Meta) != 0: Close(); return true;
                case Key.OemQuestion when (mods & KeyModifiers.Meta) != 0: ShowHelp(); return true;
            }
            return false;
        }
        if (key == Key.Insert && mods == KeyModifiers.Shift) { _ = PasteX(); return true; }
        if ((mods & KeyModifiers.Alt) != 0) return false;

        if (key == Key.F1) { ShowHelp(); return true; }
        if (KeyMapper.IsSwallowed(key)) return true;   // never move focus / press buttons
        CalcCommand cmd = KeyMapper.Map(key, mods, symbol);
        if (cmd == CalcCommand.None) return false;
        Run(cmd);
        return true;
    }

    /// <summary>On macOS, Ctrl+click opens the context menu (right-click and two-finger click work natively).</summary>
    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Control) && OperatingSystem.IsMacOS())
        {
            e.Handled = true;
            _menu.Open(this);
        }
    }

    // ------------------------------------------------------------------ display & layout

    private string Fmt(double v) => NumberFormatter.Format(v, _settings.Decimals, _settings.Format);

    internal void UpdateDisplay()
    {
        _regX.Text = _calc.IsEntering ? _calc.EntryText! : Fmt(_calc.X);
        _regY.Text = Fmt(_calc.Y);
        _regZ.Text = Fmt(_calc.Z);
        _regT.Text = Fmt(_calc.T);
    }

    /// <summary>Applies visibility and font size, then fixes the window height to fit the content exactly.</summary>
    internal void ApplyLayout()
    {
        _pStack.IsVisible = _settings.ShowStack;
        _pKeypad.IsVisible = _settings.ShowKeypad;
        _xKeyRow.IsVisible = _settings.ShowKeypad;      // Show Keypad off hides ALL keys, incl. the top row

        double px = _settings.FontSize * 96.0 / 72.0;     // points -> device-independent pixels
        double rowH = Math.Ceiling(px * 1.25) + 4;
        foreach (var t in new[] { _regT, _regZ, _regY, _regX }) t.FontSize = px;
        foreach (var b in _regBorders) b.Height = rowH;

        // Every row has an explicit whole-pixel height and margin, so the content height is exact and
        // width-independent (verified against the measured layout in LayoutFitTests).
        double h = Margin4 + rowH + Gap + Margin4;                                       // X register
        if (_settings.ShowKeypad) h += ButtonHeight + Gap;                               // Enter row
        if (_settings.ShowStack) h += Margin4 + 3 * (rowH + Gap);
        if (_settings.ShowKeypad) h += 5 * (ButtonHeight + Gap) + Margin4;
        MinHeight = 0;
        MaxHeight = double.PositiveInfinity;
        Height = h;
        MinHeight = MaxHeight = h;                       // fixed height, width remains resizable
    }

    /// <summary>If the saved position is off every screen (monitor removed), centre on the primary screen.</summary>
    private void EnsureOnScreen()
    {
        if (Screens is not { } screens || screens.All.Count == 0) return;
        double scale = RenderScaling;
        var titleBar = new PixelRect(Position.X + 20, Position.Y, Math.Max(40, (int)(Bounds.Width * scale) - 40), 24);
        bool visible = screens.All.Any(s =>
        {
            var r = s.WorkingArea.Intersect(titleBar);
            return r.Width >= 40 && r.Height >= 10;
        });
        if (visible) return;
        var primary = screens.Primary ?? screens.All[0];
        var wa = primary.WorkingArea;
        int w = (int)(Bounds.Width * primary.Scaling), hh = (int)(Bounds.Height * primary.Scaling);
        Position = new PixelPoint(wa.X + Math.Max(0, (wa.Width - w) / 2), wa.Y + Math.Max(0, (wa.Height - hh) / 2));
    }

    // ------------------------------------------------------------------ persistence

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel) SaveSettings();
    }

    /// <summary>Writes settings + stack to the JSON file. Safe to call more than once (close, Cmd+Q, exit).</summary>
    public void SaveSettings()
    {
        if (_saved && !IsVisible) return;
        _settings.Left = Position.X;
        _settings.Top = Position.Y;
        _settings.Width = (int)Math.Round(Math.Max(MinLogicalWidth, double.IsNaN(Width) ? Bounds.Width : Width));
        bool lift = _calc.StackLift || _calc.IsEntering;   // a number being typed counts as complete
        _settings.X = _calc.X; _settings.Y = _calc.Y; _settings.Z = _calc.Z; _settings.T = _calc.T;
        _settings.LastX = _calc.LastX; _settings.StackLift = lift;
        SettingsStore.Save(_settings, _settingsPath);
        _saved = true;
    }
}
