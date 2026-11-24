using RPNCalc.Engine;

namespace RPNCalc;

/// <summary>Modeless Help window (F1 or right-click → Help).</summary>
internal sealed class HelpForm : Form
{
    private readonly ConversionTable _conversions;

    public HelpForm(ConversionTable conversions)
    {
        _conversions = conversions;
        Text = "RPN Calculator Help";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(600, 600);
        MinimumSize = new Size(360, 300);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        KeyPreview = true;
        Font = UiFonts.Create("Segoe UI", 9.75F);

        var box = new RichTextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Window, DetectUrls = false, ScrollBars = RichTextBoxScrollBars.Both, WordWrap = false,
            Font = UiFonts.Create("Consolas", 10F, monospace: true), Text = BuildText(),
        };
        var ok = new Button { Text = "Close", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 90 };
        ok.Click += (_, _) => Close();
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(8) };
        bottom.Controls.Add(ok);
        var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 10, 4, 0), BackColor = SystemColors.Window };
        pad.Controls.Add(box);
        Controls.Add(pad);
        Controls.Add(bottom);
        CancelButton = ok;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape || keyData == Keys.F1) { Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private string BuildText()
    {
        string conv = Conversions.HelpSection(_conversions, @"%APPDATA%\RPNCalc\" + Conversions.FileName);
        return $$"""
RPN CALCULATOR
==============
An HP-style Reverse Polish Notation (RPN) calculator with a
four-level stack: X (bottom, always shown), Y, Z and T.

HOW RPN WORKS
-------------
Type the first number, press Enter, type the second number,
then press the operation. Example:  (3 + 4) × 5
    3  Enter  4  +  5  ×      → 35

- Enter copies X into Y (the stack moves up); the next number
  you type replaces X.
- After an operation, the next number you type pushes the
  result up into Y automatically.
- Two-number operations use Y and X; the result goes to X and
  the stack drops (T is copied down into Z).
- Last X holds the value X had before the last operation or
  conversion, so you can recover from a mistake.

KEYBOARD
--------
  0-9, .           Type a number (main keyboard or NumPad)
  + - * /          Add, subtract, multiply, divide (Y op X)
  Enter            Enter (main Enter or NumPad Enter)
  Backspace        Delete the last digit (clears X if not typing)
  Esc / Delete     Clear X
  S                Change sign (+/-)
  L                Last X
  X                Exchange X and Y
  P                Pi
  Up Arrow         Roll Up    (X→Y→Z→T→X)
  Down Arrow       Roll Down  (T→Z→Y→X→T)
  Ctrl+C           Copy X (full precision) to the clipboard
  Ctrl+V           Paste a number from the clipboard into X
  F1               This help

BUTTONS
-------
  Enter  +/-  Last X  <      Same as Enter, S, L, Backspace
  1/x                        Reciprocal of X
  y^x                        Y raised to the power X
  x²   √x                    Square, square root of X
  π                          Pi
  X<>Y  ▲  ▼                 Exchange, Roll Up, Roll Down
Hover over a button to see its keyboard shortcut.

RIGHT-CLICK MENU
----------------
  Show Stack       Show/hide the T, Z, Y registers
  Show Keypad      Show/hide the keypad (compact X-only view)
  Stay on Top      Keep the calculator above other windows
  Decimal Places   0 to 9 digits after the decimal point
  Format Type      Fixed (1234.5600) or Scientific (1.2346E+03)
  Font Size        Larger / Smaller register digits
  Conversions      Multiply X by a conversion factor
                   (the old X is saved in Last X); the list
                   comes from conversions.csv (see below)
  Help             This window
  About            Version information

CONVERSIONS
-----------
{{conv}}

ERRORS
------
Divide by zero, square root of a negative number and similar
invalid operations show a message and leave the stack unchanged.

WINDOW AND SETTINGS
-------------------
Drag the left or right edge to make the window wider. The
window shrinks automatically when the stack or keypad is hidden.

All settings and the stack (X, Y, Z, T, Last X), plus the window
position and size, are saved when you close the calculator to:
  %APPDATA%\RPNCalc\settings.json
Delete that file to restore the defaults.
""";
    }
}
