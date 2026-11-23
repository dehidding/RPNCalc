using System.Globalization;

namespace RPNCalc.Engine;

public enum FormatType { Fixed, Scientific }

/// <summary>Formats register values for display (Fixed or Scientific, 0-9 decimal places).</summary>
public static class NumberFormatter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Format(double value, int decimals, FormatType type)
    {
        decimals = Math.Clamp(decimals, 0, 9);
        if (double.IsNaN(value)) return "Error";
        if (double.IsPositiveInfinity(value)) return "Overflow";
        if (double.IsNegativeInfinity(value)) return "-Overflow";

        if (type == FormatType.Fixed)
        {
            double abs = Math.Abs(value);
            // Fall back to scientific when fixed notation would be unreadable or lose everything.
            if (abs >= 1e15 || (abs != 0 && abs < 0.5 * Math.Pow(10, -decimals) && abs < 1e-9))
                return Scientific(value, decimals);
            string s = value.ToString("F" + decimals, Inv);
            return IsNegativeZero(s) ? s[1..] : s;
        }
        return Scientific(value, decimals);
    }

    private static bool IsNegativeZero(string s) => s.StartsWith('-') && s.Skip(1).All(c => c == '0' || c == '.');

    private static string Scientific(double value, int decimals)
    {
        // e.g. 12345 with 3 decimals -> "1.235E+04"
        string s = value.ToString((decimals == 0 ? "0" : "0." + new string('0', decimals)) + "E+00", Inv);
        return IsNegativeZero(s.Split('E')[0]) ? s[1..] : s;
    }
}
