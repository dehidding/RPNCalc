using RPNCalc.Engine;

namespace RPNCalc.Mac;

internal static class HelpText
{
    public static string Build(ConversionTable conversions)
    {
        string conv = Conversions.HelpSection(conversions, "~/Library/Application Support/RPNCalc/" + Conversions.FileName);
        return $$"""
RPN CALCULATOR
==============
An HP-style Reverse Polish Notation (RPN) calculator with a
four-level stack: X (bottom, always shown), Y, Z and T.

HOW RPN WORKS
-------------
Type the first number, press Return, type the second number,
then press the operation. Example:  (3 + 4) × 5
    3  Return  4  +  5  ×      → 35

- Enter (Return) copies X into Y (the stack moves up); the
  next number you type replaces X.
- After an operation, the next number you type pushes the
  result up into Y automatically.
- Two-number operations use Y and X; the result goes to X and
  the stack drops (T is copied down into Z).
- Last X holds the value X had before the last operation or
  conversion, so you can recover from a mistake.

KEYBOARD
--------
  0-9, .           Type a number (main keys or numeric keypad)
  + - * /          Add, subtract, multiply, divide (Y op X)
                   (+ is Shift+=, * is Shift+8 without a keypad)
  Return / Enter   Enter (main Return or keypad Enter)
  delete (⌫)       Delete the last digit (clears X if not typing)
  Esc, fn+delete,
  keypad Clear     Clear X
  S                Change sign (+/-)
  L                Last X
  X                Exchange X and Y
  P                Pi
  Up Arrow         Roll Up    (X→Y→Z→T→X)
  Down Arrow       Roll Down  (T→Z→Y→X→T)
  Cmd+C / Ctrl+C   Copy X (full precision) to the clipboard
  Cmd+V / Ctrl+V   Paste a number from the clipboard into X
  F1 or Cmd+?      This help (on MacBooks F1 may need fn+F1)
  Cmd+Q            Quit (settings and stack are saved)

BUTTONS
-------
  Enter  +/-  Last X  <      Same as Return, S, L, delete
  1/x                        Reciprocal of X
  y^x                        Y raised to the power X
  x²   √x                    Square, square root of X
  π                          Pi
  X<>Y  ▲  ▼                 Exchange, Roll Up, Roll Down
Hover over a button to see its keyboard shortcut.

CONTEXT MENU (right-click, two-finger click or Ctrl+click)
------------
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
position and size, are saved when you close or quit to:
  ~/Library/Application Support/RPNCalc/settings.json
Delete that file to restore the defaults.
""";
    }
}
