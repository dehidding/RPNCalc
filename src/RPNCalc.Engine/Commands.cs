namespace RPNCalc.Engine;

public enum CalcCommand
{
    None,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9, Decimal,
    Enter, ChangeSign, LastX, Backspace, ClearX,
    Add, Subtract, Multiply, Divide,
    Reciprocal, Power, Square, SquareRoot, Pi,
    Swap, RollUp, RollDown,
}

public static class CommandMap
{
    /// <summary>
    /// Maps a typed character (WM_CHAR) to a command. Covers the main keyboard and the numeric keypad,
    /// since NumPad digits/operators also produce these characters. ',' is accepted as a decimal point
    /// because the NumPad decimal key types ',' on some keyboard layouts.
    /// </summary>
    public static CalcCommand FromChar(char c) => char.ToUpperInvariant(c) switch
    {
        >= '0' and <= '9' => CalcCommand.D0 + (c - '0'),
        '.' or ',' => CalcCommand.Decimal,
        '+' => CalcCommand.Add,
        '-' => CalcCommand.Subtract,
        '*' => CalcCommand.Multiply,
        '/' => CalcCommand.Divide,
        'S' => CalcCommand.ChangeSign,
        'L' => CalcCommand.LastX,
        'X' => CalcCommand.Swap,
        'P' => CalcCommand.Pi,
        _ => CalcCommand.None,
    };

    /// <summary>Executes a command; returns an error message or null.</summary>
    public static string? Execute(this RpnCalculator c, CalcCommand cmd)
    {
        switch (cmd)
        {
            case >= CalcCommand.D0 and <= CalcCommand.D9: c.InputDigit((char)('0' + (cmd - CalcCommand.D0))); return null;
            case CalcCommand.Decimal: c.InputDecimalPoint(); return null;
            case CalcCommand.Enter: c.Enter(); return null;
            case CalcCommand.ChangeSign: c.ChangeSign(); return null;
            case CalcCommand.LastX: c.RecallLastX(); return null;
            case CalcCommand.Backspace: c.Backspace(); return null;
            case CalcCommand.ClearX: c.ClearX(); return null;
            case CalcCommand.Add: return c.Add();
            case CalcCommand.Subtract: return c.Subtract();
            case CalcCommand.Multiply: return c.Multiply();
            case CalcCommand.Divide: return c.Divide();
            case CalcCommand.Reciprocal: return c.Reciprocal();
            case CalcCommand.Power: return c.Power();
            case CalcCommand.Square: return c.Square();
            case CalcCommand.SquareRoot: return c.SquareRoot();
            case CalcCommand.Pi: c.PushPi(); return null;
            case CalcCommand.Swap: c.Swap(); return null;
            case CalcCommand.RollUp: c.RollUp(); return null;
            case CalcCommand.RollDown: c.RollDown(); return null;
            default: return null;
        }
    }
}
