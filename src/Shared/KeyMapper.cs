using Avalonia.Input;
using RPNCalc.Engine;

namespace RPNCalc.Shared;

/// <summary>
/// Keyboard → calculator command mapping shared by the Avalonia front ends (macOS and Android).
/// Linked into each project as source, so it has no assembly of its own.
/// Character keys use the layout-aware KeySymbol ('+', '*' work on any layout); numeric-keypad keys are
/// matched by key code so they work even when the platform supplies no symbol.
/// </summary>
internal static class KeyMapper
{
    /// <summary>Bold arrow outline in a 10 x 12 unit box: wide head, thick shaft (same shape as on Windows).</summary>
    internal const string ArrowUpData = "M5,0 L10,5.5 L6.9,5.5 L6.9,12 L3.1,12 L3.1,5.5 L0,5.5 Z";
    internal const string ArrowDownData = "M5,12 L10,6.5 L6.9,6.5 L6.9,0 L3.1,0 L3.1,6.5 L0,6.5 Z";

    /// <summary>Keys that are swallowed so they never move focus or "press" a button.</summary>
    internal static bool IsSwallowed(Key key) => key is Key.Space or Key.Left or Key.Right or Key.Tab;

    /// <summary>
    /// Maps a key without Cmd/Ctrl/Alt to a command, or <see cref="CalcCommand.None"/>.
    /// </summary>
    internal static CalcCommand Map(Key key, KeyModifiers mods, string? symbol)
    {
        CalcCommand cmd = key switch
        {
            Key.Enter => CalcCommand.Enter,                  // Return and keypad Enter
            Key.Up => CalcCommand.RollUp,
            Key.Down => CalcCommand.RollDown,
            Key.Back => CalcCommand.Backspace,               // Backspace / Mac "delete"
            Key.Escape or Key.Delete or Key.Clear or Key.OemClear => CalcCommand.ClearX, // Esc, forward-delete, keypad Clear
            >= Key.NumPad0 and <= Key.NumPad9 => CalcCommand.D0 + (key - Key.NumPad0),
            Key.Add => CalcCommand.Add,
            Key.Subtract => CalcCommand.Subtract,
            Key.Multiply => CalcCommand.Multiply,
            Key.Divide => CalcCommand.Divide,
            Key.Decimal or Key.Separator => CalcCommand.Decimal,
            _ => CalcCommand.None,
        };
        if (cmd == CalcCommand.None && !string.IsNullOrEmpty(symbol) && symbol.Length == 1)
            cmd = CommandMap.FromChar(symbol[0]);
        if (cmd == CalcCommand.None && string.IsNullOrEmpty(symbol))
        {
            // Fallback when the platform supplies no symbol.
            if (key is >= Key.D0 and <= Key.D9 && mods == KeyModifiers.None) cmd = CalcCommand.D0 + (key - Key.D0);
            else if (key == Key.OemPeriod && mods == KeyModifiers.None) cmd = CalcCommand.Decimal;
            else if (key == Key.OemPlus && mods == KeyModifiers.Shift) cmd = CalcCommand.Add;
            else if (key == Key.OemMinus && mods == KeyModifiers.None) cmd = CalcCommand.Subtract;
        }
        return cmd;
    }
}
