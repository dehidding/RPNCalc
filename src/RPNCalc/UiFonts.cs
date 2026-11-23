namespace RPNCalc;

internal static class UiFonts
{
    // Fonts are cached for the lifetime of the app and never disposed: WinForms keeps the old Font
    // instance when an "equal" font is assigned, so disposing replaced fonts would break painting.
    private static readonly Dictionary<(string, float, FontStyle, GraphicsUnit), Font> Cache = [];

    /// <summary>Cached variant of <see cref="Create"/>, for fonts recreated on every layout.</summary>
    public static Font Get(string family, float size, FontStyle style, GraphicsUnit unit)
    {
        size = MathF.Round(size, 2);
        var key = (family, size, style, unit);
        if (!Cache.TryGetValue(key, out var f)) Cache[key] = f = Create(family, size, style, unit);
        return f;
    }

    /// <summary>Creates a font, falling back to a generic family if the requested one is not installed.</summary>
    public static Font Create(string family, float size, FontStyle style = FontStyle.Regular,
                              GraphicsUnit unit = GraphicsUnit.Point, bool monospace = false)
    {
        try { return new Font(family, size, style, unit); }
        catch (ArgumentException)
        {
            try { return new Font(monospace ? FontFamily.GenericMonospace : FontFamily.GenericSansSerif, size, style, unit); }
            catch (ArgumentException) { return new Font(SystemFonts.DefaultFont.FontFamily, size, style, unit); }
        }
    }
}
