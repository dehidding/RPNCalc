namespace RPNCalc.Engine;

public sealed record Conversion(string Name, double Factor);

/// <summary>Unit conversions from the original Conversions submenu (X is multiplied by the factor).</summary>
public static class Conversions
{
    public const double MlPerCubicInch = 16.387064;        // exact (2.54^3)
    public const double MlPerFluidOunce = 29.5735295625;   // exact (US fl oz = 1.8046875 in³)

    public static readonly Conversion CubicInchesToFluidOunces = new("Cubic Inches -> Fluid Ounces (US)", MlPerCubicInch / MlPerFluidOunce);
    public static readonly Conversion CubicInchesToMilliliters = new("Cubic Inches -> Milliliters", MlPerCubicInch);
    public static readonly Conversion FluidOuncesToCubicInches = new("Fluid Ounces (US) -> Cubic Inches", 1.8046875);
    public static readonly Conversion FluidOuncesToMilliliters = new("Fluid Ounces (US) -> Milliliters", MlPerFluidOunce);
    public static readonly Conversion MillilitersToCubicInches = new("Milliliters -> Cubic Inches", 1.0 / MlPerCubicInch);
    public static readonly Conversion MillilitersToFluidOunces = new("Milliliters -> Fluid Ounces (US)", 1.0 / MlPerFluidOunce);
    /// <summary>Grams (per piece) -> lbs. per M pieces ("M" = Roman numeral for 1,000); factor 2.2 kept exactly as in the original.</summary>
    public static readonly Conversion GramsToLbsPerM = new("Grams -> lbs./M", 2.2);

    /// <summary>Menu order, identical to the original program.</summary>
    public static IReadOnlyList<Conversion> All { get; } =
    [
        CubicInchesToFluidOunces, CubicInchesToMilliliters,
        FluidOuncesToCubicInches, FluidOuncesToMilliliters,
        MillilitersToCubicInches, MillilitersToFluidOunces,
        GramsToLbsPerM,
    ];
}
