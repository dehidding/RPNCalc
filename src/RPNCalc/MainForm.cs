using System.Globalization;
using RPNCalc.Engine;

namespace RPNCalc;

/// <summary>
/// Main calculator window. Layout is computed in code from the current monitor DPI
/// (per-monitor V2), so everything stays crisp and proportional at 100%-300% scaling.
/// </summary>
internal sealed class MainForm : Form
{
    private const string AppTitle = "RPN Calculator";
    private const int DefaultLogicalWidth = 240, MinLogicalWidth = 200;

    private readonly RpnCalculator _calc = new();
    private readonly AppSettings _settings;

    private readonly Panel _pStack = new(), _pMain = new(), _pKeypad = new();
    private readonly Label _tagT = MakeTag("T"), _tagZ = MakeTag("Z"), _tagY = MakeTag("Y"), _tagX = MakeTag("X");
    private readonly Label _regT = MakeReg(), _regZ = MakeReg(), _regY = MakeReg(), _regX = MakeReg();
    private readonly NoFocusButton _btnEnter, _btnChs, _btnLastX, _btnBack;
    private readonly List<(NoFocusButton Button, int Row, int Col, int Span)> _keypad = [];
    private readonly ToolTip _tips = new() { ShowAlways = true };

    private readonly ContextMenuStrip _menu = new();
    private ToolStripMenuItem _miShowStack = null!, _miShowKeypad = null!, _miStayOnTop = null!;
    private ToolStripMenuItem _miFixed = null!, _miScientific = null!;
    private readonly ToolStripMenuItem[] _miDecimals = new ToolStripMenuItem[10];

    private Font? _regFont, _tagFont, _btnFont;
    private int _logicalWidth;
    private bool _inLayout;
    private HelpForm? _help;

    public MainForm()
    {
        _settings = SettingsStore.Load();
        _calc.SetState(_settings.X, _settings.Y, _settings.Z, _settings.T, _settings.LastX, _settings.StackLift);
        _logicalWidth = _settings.Width ?? DefaultLogicalWidth;

        Text = AppTitle;
        AutoScaleMode = AutoScaleMode.None;   // we scale ourselves from DeviceDpi
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = true;
        KeyPreview = true;
        ShowIcon = true;
        Icon = LoadAppIcon();
        TopMost = _settings.StayOnTop;
        BackColor = SystemColors.Control;

        _btnEnter = MakeButton("Enter", CalcCommand.Enter, "Enter (Enter key)");
        _btnChs = MakeButton("+/-", CalcCommand.ChangeSign, "Change sign (S)");
        _btnLastX = MakeButton("Last X", CalcCommand.LastX, "Last X (L)");
        _btnBack = MakeButton("<", CalcCommand.Backspace, "Delete last digit (Backspace)");

        _pStack.Controls.AddRange([_tagT, _regT, _tagZ, _regZ, _tagY, _regY]);
        _pMain.Controls.AddRange([_tagX, _regX, _btnEnter, _btnChs, _btnLastX, _btnBack]);
        BuildKeypad();
        Controls.AddRange([_pStack, _pMain, _pKeypad]);

        BuildMenu();
        ContextMenuStrip = _menu;
        foreach (Control c in AllControls(this)) c.ContextMenuStrip = _menu;

        _tips.SetToolTip(_regX, "X register");
        _tips.SetToolTip(_regY, "Y register");
        _tips.SetToolTip(_regZ, "Z register");
        _tips.SetToolTip(_regT, "T register");

        RestorePosition();
        Relayout();
        UpdateDisplay();
    }

    // ------------------------------------------------------------------ construction helpers

    private static Label MakeTag(string text) => new()
    {
        Text = text, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false, UseMnemonic = false,
    };

    private static Label MakeReg() => new()
    {
        BackColor = Color.White, ForeColor = Color.Black, BorderStyle = BorderStyle.Fixed3D,
        TextAlign = ContentAlignment.MiddleRight, AutoSize = false, UseMnemonic = false, AutoEllipsis = false,
    };

    private NoFocusButton MakeButton(string text, CalcCommand cmd, string tip)
    {
        var b = new NoFocusButton { Text = text };
        b.Click += (_, _) => Run(cmd);
        _tips.SetToolTip(b, tip);
        return b;
    }

    private void BuildKeypad()
    {
        void Add(string text, CalcCommand cmd, string tip, int row, int col, int span = 1,
                 ButtonGlyph glyph = ButtonGlyph.None)
        {
            var b = MakeButton(text, cmd, tip);
            if (glyph != ButtonGlyph.None)
            {
                // Face shows only a drawn bold arrow; the name stays for screen readers.
                b.AccessibleName = text;
                b.Text = "";
                b.Glyph = glyph;
            }
            _keypad.Add((b, row, col, span));
            _pKeypad.Controls.Add(b);
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
            Add(digits[i], CalcCommand.D0 + int.Parse(digits[i]), digits[i], 1 + i / 3, 1 + i % 3);
        Add("0", CalcCommand.D0, "0", 4, 1, 2);
        Add(".", CalcCommand.Decimal, "Decimal point (.)", 4, 3);

        Add("X<>Y", CalcCommand.Swap, "Exchange X and Y (X)", 1, 4);
        Add("Roll Up", CalcCommand.RollUp, "Roll Up (Up Arrow)", 2, 4, glyph: ButtonGlyph.ArrowUp);
        Add("Roll Down", CalcCommand.RollDown, "Roll Down (Down Arrow)", 3, 4, glyph: ButtonGlyph.ArrowDown);
    }

    private void BuildMenu()
    {
        _miShowStack = new ToolStripMenuItem("Show Stack", null, (_, _) => { _settings.ShowStack = !_settings.ShowStack; Relayout(); });
        _miShowKeypad = new ToolStripMenuItem("Show Keypad", null, (_, _) => { _settings.ShowKeypad = !_settings.ShowKeypad; Relayout(); });
        _miStayOnTop = new ToolStripMenuItem("Stay on Top", null, (_, _) => { _settings.StayOnTop = !_settings.StayOnTop; TopMost = _settings.StayOnTop; });

        // Decimal Places: two columns, 0-4 | 5-9 (table layout fills row by row, so interleave).
        var decMenu = new ToolStripMenuItem("Decimal Places");
        var decDrop = new ToolStripDropDown { LayoutStyle = ToolStripLayoutStyle.Table };
        if (decDrop.LayoutSettings is TableLayoutSettings table) table.ColumnCount = 2;
        for (int i = 0; i < 10; i++)
        {
            int d = i;
            _miDecimals[i] = new ToolStripMenuItem(i.ToString(CultureInfo.InvariantCulture), BlankImage, (_, _) => { _settings.Decimals = d; UpdateDisplay(); })
            {
                // A plain ToolStripDropDown (needed for the 2-column table layout) draws no check margin,
                // so the current choice is shown with a check-mark image instead.
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                ImageScaling = ToolStripItemImageScaling.SizeToFit,
                Padding = new Padding(2, 1, 14, 1),
            };
        }
        for (int r = 0; r < 5; r++) decDrop.Items.AddRange([_miDecimals[r], _miDecimals[r + 5]]);
        decMenu.DropDown = decDrop;

        _miFixed = new ToolStripMenuItem("Fixed", null, (_, _) => { _settings.Format = FormatType.Fixed; UpdateDisplay(); });
        _miScientific = new ToolStripMenuItem("Scientific", null, (_, _) => { _settings.Format = FormatType.Scientific; UpdateDisplay(); });
        var fmtMenu = new ToolStripMenuItem("Format Type", null, _miFixed, _miScientific);

        var fontMenu = new ToolStripMenuItem("Font Size", null,
            new ToolStripMenuItem("Larger", null, (_, _) => ChangeFontSize(+2)),
            new ToolStripMenuItem("Smaller", null, (_, _) => ChangeFontSize(-2)));

        var convMenu = new ToolStripMenuItem("Conversions");
        foreach (var conv in Conversions.All)
        {
            var c = conv;
            convMenu.DropDownItems.Add(new ToolStripMenuItem(c.Name.Replace("->", "→"), null, (_, _) => Report(_calc.Convert(c.Factor))));
        }

        _menu.Items.AddRange(
        [
            _miShowStack, _miShowKeypad, _miStayOnTop,
            decMenu, fmtMenu, fontMenu, convMenu,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Help", null, (_, _) => ShowHelp()) { ShortcutKeyDisplayString = "F1" },
            new ToolStripMenuItem("About", null, (_, _) => { using var a = new AboutForm(); a.ShowDialog(this); }),
        ]);
        _menu.Opening += (_, _) => SyncMenuChecks();
    }

    private void SyncMenuChecks()
    {
        _miShowStack.Checked = _settings.ShowStack;
        _miShowKeypad.Checked = _settings.ShowKeypad;
        _miStayOnTop.Checked = _settings.StayOnTop;
        for (int i = 0; i < 10; i++)
        {
            bool on = _settings.Decimals == i;
            _miDecimals[i].Image = on ? CheckImage : BlankImage;
            _miDecimals[i].Font = on ? UiFonts.Get(_menu.Font.FontFamily.Name, _menu.Font.Size, FontStyle.Bold, _menu.Font.Unit) : _menu.Font;
        }
        _miFixed.Checked = _settings.Format == FormatType.Fixed;
        _miScientific.Checked = _settings.Format == FormatType.Scientific;
    }

    private static readonly Bitmap BlankImage = new(32, 32);
    private static readonly Bitmap CheckImage = DrawCheck();

    private static Bitmap DrawCheck()
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(SystemColors.MenuText, 4f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        g.DrawLines(pen, new[] { new PointF(6, 17), new PointF(13, 24), new PointF(26, 8) });
        return bmp;
    }

    private static IEnumerable<Control> AllControls(Control root)
    {
        foreach (Control c in root.Controls)
        {
            yield return c;
            foreach (var child in AllControls(c)) yield return child;
        }
    }

    private static Icon? LoadAppIcon()
    {
        try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { return null; }
    }

    // ------------------------------------------------------------------ calculator actions

    private void Run(CalcCommand cmd) => Report(_calc.Execute(cmd));

    private void Report(string? error)
    {
        UpdateDisplay();
        if (error is not null)
            MessageBox.Show(this, error, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void ChangeFontSize(int delta)
    {
        _settings.FontSize = Math.Clamp(_settings.FontSize + delta, AppSettings.MinFontSize, AppSettings.MaxFontSize);
        Relayout();
    }

    private void ShowHelp()
    {
        if (_help is null || _help.IsDisposed)
        {
            _help = new HelpForm();
            _help.Show(this);
        }
        else
        {
            if (_help.WindowState == FormWindowState.Minimized) _help.WindowState = FormWindowState.Normal;
            _help.Activate();
        }
    }

    private void CopyX()
    {
        try { Clipboard.SetText(_calc.X.ToString("R", CultureInfo.InvariantCulture)); }
        catch { System.Media.SystemSounds.Beep.Play(); }
    }

    private void PasteX()
    {
        string text;
        try { text = Clipboard.GetText().Trim().Replace(",", ""); }
        catch { text = ""; }
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && double.IsFinite(v))
        {
            _calc.PushValue(v);
            UpdateDisplay();
        }
        else System.Media.SystemSounds.Beep.Play();
    }

    // ------------------------------------------------------------------ keyboard

    /// <summary>
    /// Non-character keys. Runs before any child control sees the key (also when a button
    /// has focus), so buttons can never swallow Enter, arrows or Space.
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Enter: Run(CalcCommand.Enter); return true;      // main Enter and NumPad Enter
            case Keys.Up: Run(CalcCommand.RollUp); return true;
            case Keys.Down: Run(CalcCommand.RollDown); return true;
            case Keys.Back: Run(CalcCommand.Backspace); return true;
            case Keys.Escape:
            case Keys.Delete: Run(CalcCommand.ClearX); return true;
            case Keys.F1: ShowHelp(); return true;
            case Keys.Control | Keys.C:
            case Keys.Control | Keys.Insert: CopyX(); return true;
            case Keys.Control | Keys.V:
            case Keys.Shift | Keys.Insert: PasteX(); return true;
            case Keys.Space:
            case Keys.Left:
            case Keys.Right:
            case Keys.Tab:
            case Keys.Shift | Keys.Tab: return true;                   // swallow: never activate/move between buttons
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>Character keys: digits, '.', + - * / (main keyboard and NumPad) and S, L, X, P.</summary>
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (ModifierKeys.HasFlag(Keys.Control) || ModifierKeys.HasFlag(Keys.Alt)) return;
        var cmd = CommandMap.FromChar(e.KeyChar);
        if (cmd != CalcCommand.None)
        {
            Run(cmd);
            e.Handled = true;
        }
    }

    // ------------------------------------------------------------------ display

    private string Fmt(double v) => NumberFormatter.Format(v, _settings.Decimals, _settings.Format);

    private void UpdateDisplay()
    {
        SetReg(_regX, _calc.IsEntering ? _calc.EntryText! : Fmt(_calc.X));
        SetReg(_regY, Fmt(_calc.Y));
        SetReg(_regZ, Fmt(_calc.Z));
        SetReg(_regT, Fmt(_calc.T));
    }

    /// <summary>Sets a register's text, shrinking the font if the number does not fit.</summary>
    private void SetReg(Label lbl, string text)
    {
        lbl.Text = text;
        if (_regFont is null) return;
        int avail = Math.Max(1, lbl.ClientSize.Width - lbl.Padding.Horizontal - Px(4));
        var flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        int w = TextRenderer.MeasureText(text, _regFont, Size.Empty, flags).Width;
        Font f = _regFont;
        if (w > avail)
        {
            float px = Math.Max(6f, (float)Math.Floor(_regFont.Size * avail / w));
            f = UiFonts.Get("Arial", px, FontStyle.Bold, GraphicsUnit.Pixel);
        }
        if (!ReferenceEquals(lbl.Font, f)) lbl.Font = f;
    }

    // ------------------------------------------------------------------ layout & DPI

    private float DpiScale => DeviceDpi / 96f;
    private int Px(float logical) => (int)Math.Round(logical * DpiScale);

    private void Relayout()
    {
        if (_inLayout) return;
        _inLayout = true;
        SuspendLayout();
        try
        {
            float s = DpiScale;
            ReplaceFonts(s);

            int m = Px(4), gap = Px(3), tagW = Px(14);
            int w = Math.Max(Px(MinLogicalWidth), Px(_logicalWidth));
            int rowH = (int)Math.Ceiling(_regFont!.GetHeight() * 1.25f) + Px(4);
            int btnH = Px(26);
            int innerW = w - 2 * m;

            void PlaceReg(Label tag, Label reg, int y)
            {
                tag.Bounds = new Rectangle(m, y, tagW, rowH);
                reg.Bounds = new Rectangle(m + tagW, y, innerW - tagW, rowH);
                tag.Font = _tagFont;
                reg.Font = _regFont;
            }

            int y = 0;
            _pStack.Visible = _settings.ShowStack;
            if (_settings.ShowStack)
            {
                PlaceReg(_tagT, _regT, m);
                PlaceReg(_tagZ, _regZ, m + (rowH + gap));
                PlaceReg(_tagY, _regY, m + 2 * (rowH + gap));
                _pStack.Bounds = new Rectangle(0, y, w, m + 3 * (rowH + gap) - gap);
                y = _pStack.Bottom;
            }

            // X register and its button row: Enter | +/- | Last X | < with relative widths 1.8 : 1 : 1.4 : 0.8.
            PlaceReg(_tagX, _regX, m);
            int by = m + rowH + gap;
            float[] units = [1.8f, 1f, 1.4f, 0.8f];
            float unit = (innerW - 3 * gap) / units.Sum();
            NoFocusButton[] xRow = [_btnEnter, _btnChs, _btnLastX, _btnBack];
            float bx = m;
            for (int i = 0; i < xRow.Length; i++)
            {
                int left = (int)Math.Round(bx);
                int right = i == xRow.Length - 1 ? m + innerW : (int)Math.Round(bx + units[i] * unit);
                xRow[i].Bounds = new Rectangle(left, by, right - left, btnH);
                bx += units[i] * unit + gap;
            }
            foreach (var b in new[] { _btnEnter, _btnChs, _btnLastX, _btnBack }) b.Font = _btnFont;
            _pMain.Bounds = new Rectangle(0, y, w, by + btnH + m);
            y = _pMain.Bottom;

            _pKeypad.Visible = _settings.ShowKeypad;
            if (_settings.ShowKeypad)
            {
                float cw = (innerW - 4 * gap) / 5f;
                int ColX(int c) => m + (int)Math.Round(c * (cw + gap));
                foreach (var (b, row, col, span) in _keypad)
                {
                    int left = ColX(col);
                    int right = col + span >= 5 ? m + innerW : ColX(col + span) - gap;
                    b.Bounds = new Rectangle(left, row * (btnH + gap), right - left, btnH);
                    b.Font = _btnFont;
                }
                _pKeypad.Bounds = new Rectangle(0, y, w, 5 * (btnH + gap) - gap + m);
                y = _pKeypad.Bottom;
            }

            // Fixed height, horizontally resizable window.
            MinimumSize = Size.Empty;
            MaximumSize = Size.Empty;
            ClientSize = new Size(w, y);
            int ncW = Width - ClientSize.Width, ncH = Height - ClientSize.Height;
            MinimumSize = new Size(Px(MinLogicalWidth) + ncW, y + ncH);
            MaximumSize = new Size(Px(2000) + ncW, y + ncH);
        }
        finally
        {
            ResumeLayout(true);
            _inLayout = false;
        }
        UpdateDisplay();
    }

    private void ReplaceFonts(float s)
    {
        // Sizes are points at 96 DPI converted to pixels, then scaled for the monitor.
        _regFont = UiFonts.Get("Arial", _settings.FontSize * 96f / 72f * s, FontStyle.Bold, GraphicsUnit.Pixel);
        _tagFont = UiFonts.Get("Arial", 9f * 96f / 72f * s, FontStyle.Bold, GraphicsUnit.Pixel);
        _btnFont = UiFonts.Get("Arial", 8.25f * 96f / 72f * s, FontStyle.Bold, GraphicsUnit.Pixel);
        foreach (Control c in AllControls(this))
            if (c is Label or Button) c.Font = c is Button ? _btnFont : (c == _tagT || c == _tagZ || c == _tagY || c == _tagX ? _tagFont : _regFont);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_inLayout || WindowState != FormWindowState.Normal || !IsHandleCreated) return;
        _logicalWidth = Math.Max(MinLogicalWidth, (int)Math.Round(ClientSize.Width / DpiScale));
        Relayout();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        int logicalWidth = _logicalWidth;
        _inLayout = true;           // ignore the intermediate resize to Windows' suggested rectangle
        try
        {
            MinimumSize = Size.Empty;   // old-DPI limits must not clamp the suggested rectangle
            MaximumSize = Size.Empty;
            base.OnDpiChanged(e);
        }
        finally { _inLayout = false; }
        _logicalWidth = logicalWidth;
        Relayout();                 // rebuild fonts and geometry for the new DPI
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Relayout();             // DeviceDpi is now that of the monitor the window is actually on
        EnsureOnScreen();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ActiveControl = null;
        Activate();
    }

    // Lock the vertical size: turn top/bottom resize borders into plain borders / side borders.
    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84;
        base.WndProc(ref m);
        if (m.Msg != WM_NCHITTEST) return;
        m.Result = (int)m.Result switch
        {
            12 or 15 => 18,   // HTTOP / HTBOTTOM -> HTBORDER
            13 or 16 => 10,   // HTTOPLEFT / HTBOTTOMLEFT -> HTLEFT
            14 or 17 => 11,   // HTTOPRIGHT / HTBOTTOMRIGHT -> HTRIGHT
            _ => m.Result,
        };
    }

    // ------------------------------------------------------------------ persistence

    private void RestorePosition()
    {
        if (_settings.Left is int l && _settings.Top is int t)
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(l, t);
        }
        else StartPosition = FormStartPosition.CenterScreen;
    }

    /// <summary>If the saved position is off every monitor (e.g. monitor removed), move onto the primary screen.</summary>
    private void EnsureOnScreen()
    {
        var titleBar = new Rectangle(Left + Px(20), Top, Math.Max(Px(40), Width - Px(40)), Px(24));
        bool visible = Screen.AllScreens.Any(sc =>
        {
            var r = Rectangle.Intersect(sc.WorkingArea, titleBar);
            return r.Width >= Px(40) && r.Height >= Px(10);
        });
        if (visible) return;
        var wa = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + (wa.Height - Height) / 2);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel) return;
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        _settings.Left = bounds.Left;
        _settings.Top = bounds.Top;
        _settings.Width = _logicalWidth;
        bool lift = _calc.StackLift || _calc.IsEntering;   // a number being typed counts as complete
        _settings.X = _calc.X; _settings.Y = _calc.Y; _settings.Z = _calc.Z; _settings.T = _calc.T;
        _settings.LastX = _calc.LastX; _settings.StackLift = lift;
        SettingsStore.Save(_settings);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tips.Dispose(); _menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
