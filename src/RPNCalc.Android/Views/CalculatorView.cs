using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using RPNCalc.Engine;
using RPNCalc.Shared;

namespace RPNCalc.Droid;

/// <summary>
/// Full-screen calculator for Android (single-view lifetime). Same registers, keys, menu and keyboard
/// handling as the desktop versions, laid out with star-sized rows so the keys fill the screen in
/// portrait and landscape. Contains no Android APIs, so it can also be rendered headlessly.
/// </summary>
public sealed class CalculatorView : UserControl
{
    internal const string AppTitle = "RPNCalc";
    private const double Gap = 6, Pad = 8, ButtonFontSize = 22;
    internal static readonly FontFamily RegisterFont = new("Roboto, Arial, Liberation Sans, DejaVu Sans, sans-serif");

    private readonly RpnCalculator _calc = new();
    private readonly AppSettings _settings;
    private readonly string _settingsPath;

    private readonly Grid _body = new();
    private readonly Panel _overlayHost = new();
    private readonly Control _stackRows;
    private readonly Grid _keypad;
    private readonly Grid _mainRow;
    private readonly TextBlock _regT = RegText(), _regZ = RegText(), _regY = RegText(), _regX = RegText();
    private readonly List<Border> _regBorders = [];
    private readonly List<Button> _buttons = [];
    private readonly Button _menuButton;
    private readonly Panel _display = new();

    private readonly ContextMenu _menu = new() { Placement = PlacementMode.BottomEdgeAlignedRight };
    private MenuItem _miShowStack = null!, _miShowKeypad = null!, _miFixed = null!, _miScientific = null!;
    private readonly MenuItem[] _miDecimals = new MenuItem[10];
    private readonly MenuItem _convMenu = new() { Header = "Conversions" };
    private ConversionTable _conversions = new([], [], "");
    private readonly DispatcherTimer _saveTimer;
    private TopLevel? _topLevel;

    /// <summary>Shows calculator errors; replaceable for tests.</summary>
    internal Func<string, Task> ErrorHandler { get; set; }

    /// <param name="settingsPath">settings.json in app-private storage; conversions.csv lives next to it.</param>
    public CalculatorView(string settingsPath)
    {
        _settingsPath = settingsPath;
        _settings = SettingsStore.Load(settingsPath);
        _calc.SetState(_settings.X, _settings.Y, _settings.Z, _settings.T, _settings.LastX, _settings.StackLift);
        ErrorHandler = msg => { ShowOverlay(Overlays.Message(msg, CloseOverlay)); return Task.CompletedTask; };
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveSettings(); };

        Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xEC));
        Focusable = true;

        // ---- top bar: title + ⋮ menu button
        // Hamburger icon drawn as vector geometry (a "⋮" text glyph showed as a tofu box on devices whose font lacks it).
        _menuButton = new Button
        {
            Content = Icons.Sized(Icons.MenuData, 26, Brushes.White, 2.4),
            Focusable = false, IsTabStop = false,
            Width = 52, Height = 52, Padding = new Thickness(0), Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(26),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(_menuButton, "Menu");
        _menuButton.Click += (_, _) => OpenMenu(_menuButton);
        var title = new TextBlock
        {
            Text = AppTitle, FontSize = 20, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0), Foreground = Brushes.White,
        };
        var bar = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3F, 0x46)), Height = 52 };
        DockPanel.SetDock(_menuButton, Dock.Right);
        bar.Children.Add(_menuButton);
        bar.Children.Add(title);

        // ---- body: registers, main row, keypad
        _stackRows = BuildStack();
        _mainRow = BuildMainRow();
        _keypad = BuildKeypad();
        var xRow = RegisterRow("X", _regX);
        var regs = new StackPanel { Children = { _stackRows, xRow }, Margin = new Thickness(0, Pad, 0, 0) };
        _display.Children.Add(regs);
        _display.Background = Brushes.Transparent;          // hit-testable for long-press
        _display.AddHandler(Gestures.HoldingEvent, OnDisplayHolding, RoutingStrategies.Bubble);
        _display.ContextRequested += (_, e) => { e.Handled = true; OpenMenu(_display); };
        ToolTip.SetTip(_display, "Long-press for the menu");

        _body.RowDefinitions = new RowDefinitions("Auto,*,5*");
        _body.ColumnDefinitions = new ColumnDefinitions("*,0");
        Grid.SetRow(_display, 0); Grid.SetRow(_mainRow, 1); Grid.SetRow(_keypad, 2);
        _body.SizeChanged += (_, e) => SetLandscape(e.NewSize.Width > e.NewSize.Height * 1.15);
        _body.Children.AddRange([_display, _mainRow, _keypad]);
        _body.Margin = new Thickness(0, 0, 0, Pad);

        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(_body);
        var layers = new Panel { Children = { root, _overlayHost } };
        Content = layers;

        BuildMenu();
        ApplyLayout();
        UpdateDisplay();

        AttachedToVisualTree += (_, _) => OnAttached();
        DetachedFromVisualTree += (_, _) => OnDetached();
    }

    // ------------------------------------------------------------------ test / host accessors
    internal string XText => _regX.Text ?? "";
    internal string YText => _regY.Text ?? "";
    internal RpnCalculator Calculator => _calc;
    internal AppSettings Settings => _settings;
    internal ContextMenu Menu => _menu;
    internal IReadOnlyList<Button> Buttons => _buttons;
    internal bool OverlayOpen => _overlayHost.Children.Count > 0;
    internal string ConversionsPath => Conversions.GetPath(_settingsPath);

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
            Text = tag, Width = 20, FontFamily = RegisterFont, FontWeight = FontWeight.Bold, FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var box = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
            BorderThickness = new Thickness(1.5, 1.5, 0.5, 0.5),
            Padding = new Thickness(6, 0, 6, 0),
            Child = new Viewbox
            {
                Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Right, Child = text,
            },
        };
        _regBorders.Add(box);
        var row = new DockPanel { Margin = new Thickness(Pad, 0, Pad, Gap) };
        DockPanel.SetDock(tagBlock, Dock.Left);
        row.Children.Add(tagBlock);
        row.Children.Add(box);
        return row;
    }

    private Control BuildStack() => new StackPanel
    {
        Children = { RegisterRow("T", _regT), RegisterRow("Z", _regZ), RegisterRow("Y", _regY) },
    };

    private static readonly IDataTemplate FittedLabel = new FuncDataTemplate<string>((text, _) => new Viewbox
    {
        Stretch = Stretch.Uniform,
        StretchDirection = StretchDirection.DownOnly,
        Margin = new Thickness(3, 2),
        Child = new TextBlock { Text = text, TextWrapping = TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center },
    });

    private Button MakeButton(string text, CalcCommand cmd, string name)
    {
        var b = new Button
        {
            Content = text,
            Focusable = false,                 // buttons never take keyboard focus
            IsTabStop = false,
            MinHeight = 34,
            Padding = new Thickness(0),
            Margin = new Thickness(Gap / 2),
            FontFamily = RegisterFont,
            FontWeight = FontWeight.Bold,
            FontSize = ButtonFontSize,
            ContentTemplate = FittedLabel,
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)),
            BorderThickness = new Thickness(1),
            Tag = cmd,
        };
        b.Click += (_, _) => Run(cmd);
        AutomationProperties.SetName(b, name);
        _buttons.Add(b);
        return b;
    }

    private static void SetArrowFace(Button b, string pathData)
    {
        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(pathData), Stretch = Stretch.Uniform, Height = 22, Width = 18,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Fill = Brushes.Black,
        };
        path.Bind(Avalonia.Controls.Shapes.Shape.FillProperty, new Binding("Foreground")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ContentPresenter) },
        });
        b.ContentTemplate = null;
        b.Content = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = path, Margin = new Thickness(2) };
    }

    private Grid BuildMainRow()
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.8*,1*,1.4*,0.8*"),
            Margin = new Thickness(Pad - Gap / 2, 0, Pad - Gap / 2, 0),
        };
        Button[] bs =
        [
            MakeButton("Enter", CalcCommand.Enter, "Enter"),
            MakeButton("+/-", CalcCommand.ChangeSign, "Change sign"),
            MakeButton("Last X", CalcCommand.LastX, "Last X"),
            MakeButton("<", CalcCommand.Backspace, "Delete last digit"),
        ];
        for (int i = 0; i < bs.Length; i++) { Grid.SetColumn(bs[i], i); row.Children.Add(bs[i]); }
        return row;
    }

    private Grid BuildKeypad()
    {
        var g = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*"),
            RowDefinitions = new RowDefinitions("*,*,*,*,*"),
            Margin = new Thickness(Pad - Gap / 2, 0, Pad - Gap / 2, 0),
        };
        void Add(string text, CalcCommand cmd, string name, int row, int col, int span = 1, string? arrow = null)
        {
            var b = MakeButton(text, cmd, name);
            if (arrow is not null) SetArrowFace(b, arrow);
            Grid.SetRow(b, row); Grid.SetColumn(b, col); Grid.SetColumnSpan(b, span);
            g.Children.Add(b);
        }

        Add("1/x", CalcCommand.Reciprocal, "Reciprocal", 0, 0);
        Add("y^x", CalcCommand.Power, "Power", 0, 1);
        Add("x²", CalcCommand.Square, "Square", 0, 2);
        Add("√x", CalcCommand.SquareRoot, "Square root", 0, 3);
        Add("π", CalcCommand.Pi, "Pi", 0, 4);

        Add("−", CalcCommand.Subtract, "Subtract", 1, 0);
        Add("+", CalcCommand.Add, "Add", 2, 0);
        Add("×", CalcCommand.Multiply, "Multiply", 3, 0);
        Add("÷", CalcCommand.Divide, "Divide", 4, 0);

        string[] digits = ["7", "8", "9", "4", "5", "6", "1", "2", "3"];
        for (int i = 0; i < 9; i++)
            Add(digits[i], CalcCommand.D0 + int.Parse(digits[i], CultureInfo.InvariantCulture), digits[i], 1 + i / 3, 1 + i % 3);
        Add("0", CalcCommand.D0, "0", 4, 1, 2);
        Add(".", CalcCommand.Decimal, "Decimal point", 4, 3);

        Add("X<>Y", CalcCommand.Swap, "Exchange X and Y", 1, 4);
        Add("Roll Up", CalcCommand.RollUp, "Roll Up", 2, 4, arrow: KeyMapper.ArrowUpData);
        Add("Roll Down", CalcCommand.RollDown, "Roll Down", 3, 4, arrow: KeyMapper.ArrowDownData);
        return g;
    }

    /// <summary>
    /// Same items as the desktop context menu, except Stay on Top (there are no overlapping windows on
    /// Android), plus Copy X / Paste X (no Cmd+C/V on a touch screen) and an in-app conversions editor.
    /// </summary>
    private void BuildMenu()
    {
        MenuItem Item(string header, Action onClick, MenuItemToggleType toggle = MenuItemToggleType.None, string? group = null)
        {
            var mi = new MenuItem { Header = header, ToggleType = toggle };
            if (group is not null) mi.GroupName = group;
            mi.Click += (_, _) => { onClick(); SyncMenuChecks(); };
            return mi;
        }

        _miShowStack = Item("Show Stack", () => { _settings.ShowStack = !_settings.ShowStack; ApplyLayout(); SaveSoon(); }, MenuItemToggleType.CheckBox);
        _miShowKeypad = Item("Show Keypad", () => { _settings.ShowKeypad = !_settings.ShowKeypad; ApplyLayout(); SaveSoon(); }, MenuItemToggleType.CheckBox);

        var decMenu = new MenuItem { Header = "Decimal Places", ItemsPanel = new FuncTemplate<Panel?>(() => new UniformGrid { Columns = 2 }) };
        for (int i = 0; i < 10; i++)
        {
            int d = i;
            _miDecimals[i] = Item(i.ToString(CultureInfo.InvariantCulture), () => { _settings.Decimals = d; UpdateDisplay(); SaveSoon(); },
                                  MenuItemToggleType.Radio, "decimals");
        }
        for (int r = 0; r < 5; r++) { decMenu.Items.Add(_miDecimals[r]); decMenu.Items.Add(_miDecimals[r + 5]); }

        _miFixed = Item("Fixed", () => { _settings.Format = FormatType.Fixed; UpdateDisplay(); SaveSoon(); }, MenuItemToggleType.Radio, "format");
        _miScientific = Item("Scientific", () => { _settings.Format = FormatType.Scientific; UpdateDisplay(); SaveSoon(); }, MenuItemToggleType.Radio, "format");
        var fmtMenu = new MenuItem { Header = "Format Type", Items = { _miFixed, _miScientific } };
        var fontMenu = new MenuItem
        {
            Header = "Font Size",
            Items = { Item("Larger", () => ChangeFontSize(+2)), Item("Smaller", () => ChangeFontSize(-2)) },
        };

        RebuildConversionsMenu();
        _menu.Items.Add(_miShowStack);
        _menu.Items.Add(_miShowKeypad);
        _menu.Items.Add(decMenu);
        _menu.Items.Add(fmtMenu);
        _menu.Items.Add(fontMenu);
        _menu.Items.Add(_convMenu);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(Item("Copy X", () => _ = CopyX()));
        _menu.Items.Add(Item("Paste X", () => _ = PasteX()));
        _menu.Items.Add(new Separator());
        _menu.Items.Add(Item("Help", ShowHelp));
        _menu.Items.Add(Item("About", ShowAbout));
        _menu.Opening += (_, _) => { RebuildConversionsMenu(); SyncMenuChecks(); };
        SyncMenuChecks();
    }

    internal void RebuildConversionsMenu()
    {
        _conversions = Conversions.Load(ConversionsPath);
        _convMenu.Items.Clear();
        foreach (var conv in _conversions.Items)
        {
            var c = conv;
            var mi = new MenuItem { Header = Icons.ConversionHeader(c) };   // vector arrow instead of "→"
            AutomationProperties.SetName(mi, c.Name);
            mi.Click += (_, _) => { Report(_calc.Convert(c.Factor)); SaveSoon(); };
            _convMenu.Items.Add(mi);
        }
        if (_conversions.Items.Count == 0)
            _convMenu.Items.Add(new MenuItem { Header = "(no conversions in the file)", IsEnabled = false });
        _convMenu.Items.Add(new Separator());
        if (_conversions.Warnings.Count > 0)
        {
            var warn = new MenuItem
            {
                Header = $"{_conversions.Warnings.Count} line(s) skipped",
                Icon = Icons.Warning(18, Brushes.DarkOrange),
            };
            warn.Click += (_, _) => EditConversions();
            _convMenu.Items.Add(warn);
        }
        var edit = new MenuItem { Header = "Edit Conversions..." };
        edit.Click += (_, _) => EditConversions();
        _convMenu.Items.Add(edit);
    }

    private void SyncMenuChecks()
    {
        _miShowStack.IsChecked = _settings.ShowStack;
        _miShowKeypad.IsChecked = _settings.ShowKeypad;
        for (int i = 0; i < 10; i++) _miDecimals[i].IsChecked = _settings.Decimals == i;
        _miFixed.IsChecked = _settings.Format == FormatType.Fixed;
        _miScientific.IsChecked = _settings.Format == FormatType.Scientific;
    }

    internal void OpenMenu(Control anchor)
    {
        if (OverlayOpen || _menu.IsOpen) return;
        _menu.Open(anchor);
    }

    private void OnDisplayHolding(object? sender, HoldingRoutedEventArgs e)
    {
        if (e.HoldingState != HoldingState.Started) return;
        e.Handled = true;
        OpenMenu(_display);
    }

    // ------------------------------------------------------------------ overlays (Help, About, editor, messages)

    private void ShowOverlay(Control overlay)
    {
        _menu.Close();
        _overlayHost.Children.Clear();
        _overlayHost.Children.Add(overlay);
    }

    /// <summary>Closes the top overlay; returns false if none was open (Back then leaves the app).</summary>
    internal bool CloseOverlay()
    {
        if (_overlayHost.Children.Count == 0) return false;
        _overlayHost.Children.Clear();
        Focus();
        return true;
    }

    public void ShowHelp()
    {
        _conversions = Conversions.Load(ConversionsPath);
        ShowOverlay(Overlays.Page("Help", Overlays.HelpBody(AndroidHelpText.Build(_conversions)), () => CloseOverlay()));
    }

    public void ShowAbout() => ShowOverlay(Overlays.Page("About", Overlays.AboutBody(), () => CloseOverlay()));

    private void EditConversions()
    {
        Conversions.Load(ConversionsPath);   // creates the default file if missing
        string text;
        try { text = File.ReadAllText(ConversionsPath); }
        catch { text = Conversions.DefaultCsv; }
        var editor = new ConversionsEditor(text, () => TopLevel.GetTopLevel(this)?.StorageProvider);
        editor.Saved += csv =>
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConversionsPath)!);
                File.WriteAllText(ConversionsPath, csv, new System.Text.UTF8Encoding(false));
                RebuildConversionsMenu();
                CloseOverlay();
            }
            catch (Exception ex) { editor.ShowStatus("Could not save: " + ex.Message, error: true); }
        };
        editor.Cancelled += () => CloseOverlay();
        ShowOverlay(Overlays.Page("Edit Conversions", editor, () => CloseOverlay()));
    }

    // ------------------------------------------------------------------ actions

    internal void Run(CalcCommand cmd)
    {
        Report(_calc.Execute(cmd));
        SaveSoon();
    }

    private void Report(string? error)
    {
        UpdateDisplay();
        if (error is not null) _ = ErrorHandler(error);
    }

    private void ChangeFontSize(int delta)
    {
        _settings.FontSize = Math.Clamp(_settings.FontSize + delta, AppSettings.MinFontSize, AppSettings.MaxFontSize);
        ApplyLayout();
        SaveSoon();
    }

    internal async Task CopyX()
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } cb)
                await cb.SetTextAsync(_calc.X.ToString("R", CultureInfo.InvariantCulture));
        }
        catch { /* clipboard unavailable */ }
    }

    internal async Task PasteX()
    {
        string text = "";
        try
        {
#pragma warning disable CS0618 // GetTextAsync is fine for plain text
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } cb) text = (await cb.GetTextAsync())?.Trim().Replace(",", "") ?? "";
#pragma warning restore CS0618
        }
        catch { /* clipboard unavailable */ }
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && double.IsFinite(v))
        {
            _calc.PushValue(v);
            UpdateDisplay();
            SaveSoon();
        }
        else _ = ErrorHandler("The clipboard does not contain a number.");
    }

    // ------------------------------------------------------------------ hardware keyboard & back

    private void OnAttached()
    {
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is null) return;
        // Tunnel on the TopLevel: keys arrive even when nothing has focus, and buttons can never swallow them.
        _topLevel.AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        _topLevel.BackRequested += OnBackRequested;
        if (_topLevel.InsetsManager is { } im)
        {
            // Android 15+ always draws edge-to-edge; keep the bar and keys clear of the status/navigation bars.
            // Pad manually (not Avalonia's automatic padding) so the overlays get the same insets.
            TopLevel.SetAutoSafeAreaPadding(this, false);
            TopLevel.SetAutoSafeAreaPadding(_topLevel, false);
            im.DisplayEdgeToEdgePreference = true;
            Padding = im.SafeAreaPadding;
            im.SafeAreaChanged += OnSafeAreaChanged;
        }
        Focus();
    }

    private void OnDetached()
    {
        if (_topLevel is null) return;
        _topLevel.RemoveHandler(KeyDownEvent, OnPreviewKeyDown);
        _topLevel.BackRequested -= OnBackRequested;
        if (_topLevel.InsetsManager is { } im) im.SafeAreaChanged -= OnSafeAreaChanged;
        _topLevel = null;
    }

    private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e) => Padding = e.SafeAreaPadding;

    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (_menu.IsOpen) { _menu.Close(); e.Handled = true; }
        else if (CloseOverlay()) e.Handled = true;
        else SaveSettings();          // not handled: Android moves the app to the background
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (HandleKey(e.Key, e.KeyModifiers, e.KeySymbol)) e.Handled = true;
    }

    /// <summary>
    /// Hardware / Bluetooth keyboard: the same mapping as the desktop versions (shared KeyMapper).
    /// While an overlay is open, keys go to it (the conversions editor needs them); Esc closes it.
    /// </summary>
    internal bool HandleKey(Key key, KeyModifiers mods, string? symbol)
    {
        if (OverlayOpen)
        {
            if (key == Key.Escape) return CloseOverlay();
            return false;
        }
        if (_menu.IsOpen) return false;
        bool ctrl = (mods & (KeyModifiers.Meta | KeyModifiers.Control)) != 0;
        if (ctrl)
        {
            switch (key)
            {
                case Key.C: _ = CopyX(); return true;
                case Key.V: _ = PasteX(); return true;
            }
            return false;
        }
        if ((mods & KeyModifiers.Alt) != 0) return false;
        if (key == Key.F1) { ShowHelp(); return true; }
        if (key is Key.Apps or Key.F10) { OpenMenu(_menuButton); return true; }
        if (KeyMapper.IsSwallowed(key)) return true;
        CalcCommand cmd = KeyMapper.Map(key, mods, symbol);
        if (cmd == CalcCommand.None) return false;
        Run(cmd);
        return true;
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

    /// <summary>
    /// Stack/keypad visibility and register font size. The main row and keypad share the remaining height
    /// (1 : 5 rows), so all keys are equally tall; without the keypad the main row is a normal key height.
    /// </summary>
    internal void ApplyLayout()
    {
        _stackRows.IsVisible = _settings.ShowStack;
        _keypad.IsVisible = _settings.ShowKeypad;
        _body.RowDefinitions[2].Height = _settings.ShowKeypad ? new GridLength(5, GridUnitType.Star) : new GridLength(0);
        _body.RowDefinitions[1].Height = _settings.ShowKeypad ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        _mainRow.Height = _settings.ShowKeypad ? double.NaN : 64;
        _mainRow.VerticalAlignment = _settings.ShowKeypad ? VerticalAlignment.Stretch : VerticalAlignment.Top;

        double px = _settings.FontSize * 96.0 / 72.0 * 1.25;   // a little larger than desktop for touch screens
        double rowH = Math.Ceiling(px * 1.3) + 6;
        foreach (var t in new[] { _regT, _regZ, _regY, _regX }) t.FontSize = px;
        foreach (var b in _regBorders) b.Height = rowH;
    }

    internal bool IsLandscape { get; private set; }

    /// <summary>Landscape: registers on the left, keys on the right (the keys keep a usable height).</summary>
    private void SetLandscape(bool landscape)
    {
        if (landscape == IsLandscape) return;
        IsLandscape = landscape;
        _body.ColumnDefinitions[1].Width = landscape ? new GridLength(1.25, GridUnitType.Star) : new GridLength(0);
        Grid.SetRowSpan(_display, landscape ? 3 : 1);
        int keyCol = landscape ? 1 : 0;
        Grid.SetColumn(_mainRow, keyCol);
        Grid.SetColumn(_keypad, keyCol);
        _mainRow.Margin = _keypad.Margin = landscape
            ? new Thickness(0, Pad - Gap / 2, Pad - Gap / 2, 0)
            : new Thickness(Pad - Gap / 2, 0, Pad - Gap / 2, 0);
    }

    // ------------------------------------------------------------------ persistence

    private void SaveSoon()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>Writes settings + stack to settings.json (called on pause/stop, Back, and shortly after changes).</summary>
    public void SaveSettings()
    {
        _saveTimer.Stop();
        bool lift = _calc.StackLift || _calc.IsEntering;   // a number being typed counts as complete
        _settings.X = _calc.X; _settings.Y = _calc.Y; _settings.Z = _calc.Z; _settings.T = _calc.T;
        _settings.LastX = _calc.LastX; _settings.StackLift = lift;
        SettingsStore.Save(_settings, _settingsPath);
    }
}
