using System.Globalization;

namespace RPNCalc.Engine;

/// <summary>
/// HP-style four-level RPN stack (X, Y, Z, T) with Last X.
/// Mirrors the original 1997 Delphi program's EnterFlag / PushFlag semantics:
///  - after Enter, the next number entered overwrites X (stack lift disabled);
///  - after an operation, the next number entered pushes the stack first (stack lift enabled).
/// Every method returns null on success or an error message (state is left unchanged on error).
/// </summary>
public sealed class RpnCalculator
{
    public const int MaxEntryDigits = 15;
    public const string ErrDivideByZero = "Divide by Zero";
    public const string ErrInvalid = "Invalid Input";
    public const string ErrOverflow = "Overflow";

    private string? _entry;          // digits being typed; null when not in entry mode
    private double _x;

    public double Y { get; private set; }
    public double Z { get; private set; }
    public double T { get; private set; }
    public double LastX { get; private set; }

    /// <summary>True when the next number entered pushes the stack (HP "stack lift enabled").</summary>
    public bool StackLift { get; private set; }

    /// <summary>X register; while typing it reflects the parsed entry.</summary>
    public double X => _entry is null ? _x : ParseEntry(_entry);

    public bool IsEntering => _entry is not null;

    /// <summary>The raw text being typed (e.g. "12." or "-0.5"), or null.</summary>
    public string? EntryText => _entry;

    public void SetState(double x, double y, double z, double t, double lastX, bool stackLift)
    {
        _entry = null;
        _x = Sanitize(x); Y = Sanitize(y); Z = Sanitize(z); T = Sanitize(t); LastX = Sanitize(lastX);
        StackLift = stackLift;
    }

    private static double Sanitize(double v) => double.IsFinite(v) ? v : 0.0;

    // ---------------------------------------------------------------- number entry

    public void InputDigit(char d)
    {
        if (d < '0' || d > '9') throw new ArgumentOutOfRangeException(nameof(d));
        BeginEntryIfNeeded();
        string body = _entry!.TrimStart('-');
        int digitCount = body.Count(char.IsDigit);
        if (body == "0") { _entry = _entry.StartsWith('-') ? "-" + d : d.ToString(); return; }
        if (digitCount >= MaxEntryDigits) return;
        _entry += d;
    }

    public void InputDecimalPoint()
    {
        BeginEntryIfNeeded();
        if (_entry!.Contains('.')) return;
        _entry += _entry.TrimStart('-').Length == 0 ? "0." : ".";
    }

    private void BeginEntryIfNeeded()
    {
        if (_entry is not null) return;
        if (StackLift) Lift();
        _entry = "0";
        StackLift = false;
    }

    /// <summary>Backspace: delete the last typed character; outside entry it clears X.</summary>
    public void Backspace()
    {
        if (_entry is null) { ClearX(); return; }
        _entry = _entry[..^1];
        if (_entry.Length == 0 || _entry == "-")
        {
            _entry = null; _x = 0; StackLift = false;
        }
    }

    /// <summary>Clear X to 0; the next number overwrites it.</summary>
    public void ClearX()
    {
        _entry = null; _x = 0; StackLift = false;
    }

    private void FinishEntry()
    {
        if (_entry is null) return;
        _x = ParseEntry(_entry);
        _entry = null;
    }

    private static double ParseEntry(string s)
    {
        if (s.Length == 0 || s == "-") return 0;
        return double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private void Lift() { T = Z; Z = Y; Y = _x; }
    private void Drop() { Y = Z; Z = T; }

    // ---------------------------------------------------------------- stack operations

    public void Enter()
    {
        FinishEntry();
        Lift();
        StackLift = false;   // EnterFlag: next number overwrites X
    }

    public void ChangeSign()
    {
        if (_entry is not null)
        {
            _entry = _entry.StartsWith('-') ? _entry[1..] : "-" + _entry;
            return;
        }
        _x = -_x;
        if (_x == 0) _x = 0; // avoid "-0"
    }

    public void Swap()
    {
        FinishEntry();
        (_x, Y) = (Y, _x);
        StackLift = true;
    }

    /// <summary>Roll Up: X→Y→Z→T→X.</summary>
    public void RollUp()
    {
        FinishEntry();
        double t = T;
        T = Z; Z = Y; Y = _x; _x = t;
        StackLift = true;
    }

    /// <summary>Roll Down: T→Z→Y→X→T.</summary>
    public void RollDown()
    {
        FinishEntry();
        double x = _x;
        _x = Y; Y = Z; Z = T; T = x;
        StackLift = true;
    }

    public void PushPi() => PushValue(Math.PI);
    public void RecallLastX() => PushValue(LastX);

    /// <summary>Puts a value in X the same way a typed number would (honours stack lift).</summary>
    public void PushValue(double v)
    {
        bool wasEntering = IsEntering;   // a finished entry always lifts, like a completed number
        FinishEntry();
        if (StackLift || wasEntering) Lift();
        _x = Sanitize(v);
        StackLift = true;
    }

    // ---------------------------------------------------------------- arithmetic

    public string? Add() => Binary((y, x) => y + x);
    public string? Subtract() => Binary((y, x) => y - x);
    public string? Multiply() => Binary((y, x) => y * x);
    public string? Divide() => Binary((y, x) => x == 0 ? throw new CalcException(ErrDivideByZero) : y / x);

    public string? Power() => Binary((y, x) =>
    {
        if (y == 0 && x < 0) throw new CalcException(ErrDivideByZero);
        return Math.Pow(y, x);
    });

    public string? Reciprocal() => Unary(x => x == 0 ? throw new CalcException(ErrDivideByZero) : 1.0 / x);
    public string? Square() => Unary(x => x * x);
    public string? SquareRoot() => Unary(x => x < 0 ? throw new CalcException(ErrInvalid) : Math.Sqrt(x));

    /// <summary>Multiply X by a conversion factor (Last X saved first, as in the original).</summary>
    public string? Convert(double factor) => Unary(x => x * factor);

    private string? Binary(Func<double, double, double> op)
    {
        FinishEntry();
        double r;
        try { r = op(Y, _x); }
        catch (CalcException ex) { StackLift = true; return ex.Message; }
        if (double.IsNaN(r)) { StackLift = true; return ErrInvalid; }
        if (double.IsInfinity(r)) { StackLift = true; return ErrOverflow; }
        LastX = _x;
        _x = r;
        Drop();
        StackLift = true;
        return null;
    }

    private string? Unary(Func<double, double> op)
    {
        FinishEntry();
        double r;
        try { r = op(_x); }
        catch (CalcException ex) { StackLift = true; return ex.Message; }
        if (double.IsNaN(r)) { StackLift = true; return ErrInvalid; }
        if (double.IsInfinity(r)) { StackLift = true; return ErrOverflow; }
        LastX = _x;
        _x = r;
        StackLift = true;
        return null;
    }

    private sealed class CalcException(string message) : Exception(message);
}
