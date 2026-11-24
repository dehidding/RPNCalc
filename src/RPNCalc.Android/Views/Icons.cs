using Avalonia;
using Avalonia.Controls;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Layout;
using Avalonia.Media;
using RPNCalc.Engine;

namespace RPNCalc.Droid;

/// <summary>
/// Vector icons used instead of symbol characters (⋮ ← → ⚠ ✓ ▲ ▼). Android's Roboto and Droid Sans Mono
/// do not contain those glyphs, so as text they can show as "tofu" boxes on some devices. Geometry
/// renders the same everywhere. All shapes are drawn in a 24 x 24 unit box.
/// </summary>
internal static class Icons
{
    /// <summary>Hamburger menu: three horizontal lines (stroked with round caps).</summary>
    internal const string MenuData = "M4,6.5 H20 M4,12 H20 M4,17.5 H20";
    /// <summary>Back arrow pointing left.</summary>
    internal const string BackData = "M20,12 H5 M11,5.5 L4.5,12 L11,18.5";
    /// <summary>Arrow pointing right (conversion From → To).</summary>
    internal const string RightArrowData = "M4,12 H19 M13,5.5 L19.5,12 L13,18.5";
    /// <summary>Check mark.</summary>
    internal const string CheckData = "M4.5,12.5 L9.5,17.5 L19.5,6.5";
    /// <summary>Warning triangle outline (the "!" is added by <see cref="Warning"/>).</summary>
    internal const string WarningData = "M12,3 L22,20.5 H2 Z";

    /// <summary>A stroked line icon in the 24-unit box; Stroke follows <paramref name="brush"/>.</summary>
    internal static Path Line(string data, IBrush brush, double thickness = 2.4) => new()
    {
        Data = Geometry.Parse(data),
        Stroke = brush,
        StrokeThickness = thickness,
        StrokeLineCap = PenLineCap.Round,
        StrokeJoin = PenLineJoin.Round,
        Stretch = Stretch.None,
        Width = 24, Height = 24,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        IsHitTestVisible = false,
    };

    /// <summary>Wraps a 24-unit icon so it occupies exactly <paramref name="size"/> in layout.</summary>
    internal static Control Sized(string data, double size, IBrush brush, double thickness = 2.4) => new Viewbox
    {
        Width = size, Height = size, Stretch = Stretch.Uniform,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        IsHitTestVisible = false,
        Child = new Canvas
        {
            Width = 24, Height = 24,
            Children = { Line(data, brush, thickness) },
        },
    };

    /// <summary>Warning triangle with an exclamation mark.</summary>
    internal static Control Warning(double size, IBrush brush) => new Viewbox
    {
        Width = size, Height = size, Stretch = Stretch.Uniform, IsHitTestVisible = false,
        VerticalAlignment = VerticalAlignment.Center,
        Child = new Canvas
        {
            Width = 24, Height = 24,
            Children =
            {
                Line(WarningData, brush, 2),
                Line("M12,9.5 V14", brush, 2.2),
                Line("M12,17.3 V17.4", brush, 2.6),
            },
        },
    };

    /// <summary>"From → To" for a conversion menu item, with a vector arrow.</summary>
    internal static Control ConversionHeader(Conversion c, IBrush? arrowBrush = null)
    {
        var arrow = Sized(RightArrowData, 14, arrowBrush ?? Brushes.Black, 2.6);
        arrow.Margin = new Thickness(6, 0);
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new TextBlock { Text = c.From, VerticalAlignment = VerticalAlignment.Center },
                arrow,
                new TextBlock { Text = c.To, VerticalAlignment = VerticalAlignment.Center },
            },
        };
    }
}
