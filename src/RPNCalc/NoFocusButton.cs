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
        base.OnPaint(e);
        if (_glyph == ButtonGlyph.None) return;
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
