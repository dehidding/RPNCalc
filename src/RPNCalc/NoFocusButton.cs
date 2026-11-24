using System.Drawing.Drawing2D;

namespace RPNCalc;

/// <summary>Vector glyph drawn instead of text on a button face.</summary>
internal enum ButtonGlyph { None, ArrowUp, ArrowDown }

/// <summary>
/// A button that never keeps keyboard focus, so Enter / arrow keys / Space always go to the calculator
/// instead of "clicking" the last button pressed with the mouse.
/// </summary>
internal sealed class NoFocusButton : Button
{
    public NoFocusButton()
    {
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        UseMnemonic = false;
        UseVisualStyleBackColor = true;
    }

    protected override bool ShowFocusCues => false;

    private ButtonGlyph _glyph;
    private string _label = "";
    private (string Label, Size Size, Font Font, Font Fitted) _fitCache;

    private const TextFormatFlags LabelFlags =
        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

    /// <summary>
    /// Key label. It is drawn by this class (not by the native button, which wraps or clips text that
    /// does not fit) at <see cref="Control.Font"/>, or at the largest smaller size that fits inside the
    /// key, so long labels such as "X&lt;&gt;Y" shrink just enough. Also used as the accessible name.
    /// </summary>
    public string Label
    {
        get => _label;
        set
        {
            _label = value ?? "";
            AccessibleName = _label.Length > 0 ? _label : null;
            _fitCache = default;
            Invalidate();
        }
    }

    /// <summary>
    /// Largest font (same family/style as Font, at most Font's size) whose label fits the key face,
    /// keeping clear of the button border. Measured with the same API and flags used to draw it.
    /// </summary>
    internal Font FittedLabelFont(Graphics? g = null)
    {
        if (_fitCache.Label == _label && _fitCache.Size == ClientSize && ReferenceEquals(_fitCache.Font, Font) && _fitCache.Fitted is not null)
            return _fitCache.Fitted;
        float s = DeviceDpi / 96f;
        int availW = ClientSize.Width - 2 * (int)Math.Ceiling(3 * s);   // border + 1 px
        int availH = ClientSize.Height - 2 * (int)Math.Ceiling(2 * s);
        Font f = Font;
        float px = Font.SizeInPoints * DeviceDpi / 72f;   // work in pixels, whatever unit Font uses
        Size Measure(Font font) => g is null
            ? TextRenderer.MeasureText(_label, font, new Size(int.MaxValue, int.MaxValue), LabelFlags)
            : TextRenderer.MeasureText(g, _label, font, new Size(int.MaxValue, int.MaxValue), LabelFlags);
        while (px > 7f)
        {
            Size m = Measure(f);
            if (m.Width <= availW && m.Height <= availH) break;
            px -= 0.5f;
            f = UiFonts.Get(Font.FontFamily.Name, px, Font.Style, GraphicsUnit.Pixel);
        }
        _fitCache = (_label, ClientSize, Font, f);
        return f;
    }

    protected override void OnFontChanged(EventArgs e) { _fitCache = default; base.OnFontChanged(e); }
    protected override void OnSizeChanged(EventArgs e) { _fitCache = default; base.OnSizeChanged(e); }

    /// <summary>
    /// When set, a bold arrow is drawn (anti-aliased, sized from the button's pixel height so it scales
    /// with DPI) and no text is shown. AccessibleName should still describe the action.
    /// </summary>
    public ButtonGlyph Glyph
    {
        get => _glyph;
        set { _glyph = value; Invalidate(); }
    }

    /// <summary>Bold arrow outline in a 10 x 12 unit box (pointing up): wide head, thick shaft.</summary>
    internal static readonly PointF[] ArrowUpUnit =
    [
        new(5f, 0f), new(10f, 5.5f), new(6.9f, 5.5f), new(6.9f, 12f),
        new(3.1f, 12f), new(3.1f, 5.5f), new(0f, 5.5f),
    ];

    /// <summary>Arrow polygon in pixels, centred in <paramref name="bounds"/>; height ≈ 58% of the button.</summary>
    internal static PointF[] ArrowPolygon(ButtonGlyph glyph, RectangleF bounds)
    {
        float h = Math.Max(6f, Math.Min(bounds.Height * 0.58f, bounds.Width * 0.8f));
        float k = h / 12f, w = 10f * k;
        float x0 = bounds.X + (bounds.Width - w) / 2f, y0 = bounds.Y + (bounds.Height - h) / 2f;
        return ArrowUpUnit.Select(p => new PointF(
            x0 + p.X * k,
            y0 + (glyph == ButtonGlyph.ArrowDown ? 12f - p.Y : p.Y) * k)).ToArray();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);   // button face only: Text stays empty
        if (_glyph == ButtonGlyph.None)
        {
            if (_label.Length == 0) return;
            var color = Enabled ? ForeColor : SystemColors.GrayText;
            TextRenderer.DrawText(e.Graphics, _label, FittedLabelFont(e.Graphics), ClientRectangle, color,
                LabelFlags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        var g = e.Graphics;
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Enabled ? ForeColor : SystemColors.GrayText);
        g.FillPolygon(brush, ArrowPolygon(_glyph, ClientRectangle));
        g.SmoothingMode = old;
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        // The native BUTTON takes focus on mouse-down; hand it back to the form after the click.
        // (Even while a button has focus, MainForm.ProcessCmdKey sees Enter/arrows/Space first.)
        if (FindForm() is Form f) { f.ActiveControl = null; f.Focus(); }
    }
}
