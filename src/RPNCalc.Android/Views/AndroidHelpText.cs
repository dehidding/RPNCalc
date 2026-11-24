using RPNCalc.Engine;

namespace RPNCalc.Droid;

internal static class AndroidHelpText
{
    public static string Build(ConversionTable conversions)
    {
        string conv = Conversions.HelpSection(conversions, "(app storage)/" + Conversions.FileName)
            .Replace("opens the file. Delete it to get the built-in list back.",
                     "opens the in-app editor (Reset to defaults restores the list).");
        return $$"""
RPN CALCULATOR
==============
An HP-style Reverse Polish Notation (RPN) calculator with a
four-level stack: X (bottom, always shown), Y, Z and T.

HOW RPN WORKS
-------------
Type the first number, tap Enter, type the second number,
then tap the operation. Example:  (3 + 4) × 5
    3  Enter  4  +  5  ×      → 35

- Enter copies X into Y (the stack moves up); the next
  number you type replaces X.
- After an operation, the next number you type pushes the
  result up into Y automatically.
- Two-number operations use Y and X; the result goes to X and
  the stack drops (T is copied down into Z).
- Last X holds the value X had before the last operation or
  conversion, so you can recover from a mistake.

KEYS
----
  Enter  +/-  Last X  <      Enter, change sign, Last X,
                             delete last digit (clears X
                             when not typing)
  1/x                        Reciprocal of X
  y^x                        Y raised to the power X
  x²   √x                    Square, square root of X
  π                          Pi
  X<>Y  ▲  ▼                 Exchange, Roll Up, Roll Down

MENU (⋮ button, or long-press the display)
----
  Show Stack       Show/hide the T, Z, Y registers
  Show Keypad      Show/hide the keypad
  Decimal Places   0 to 9 digits after the decimal point
  Format Type      Fixed (1234.5600) or Scientific (1.2346E+03)
  Font Size        Larger / Smaller register digits
  Conversions      Multiply X by a conversion factor
                   (the old X is saved in Last X)
  Copy X           Copy X (full precision) to the clipboard
  Paste X          Paste a number from the clipboard into X
  Help, About      This page / version information
Stay on Top from the desktop versions is not offered: Android
apps run full screen.

HARDWARE KEYBOARD
-----------------
  0-9 .  + - * /   Numbers and operations
  Enter            Enter
  Backspace        Delete the last digit
  Esc / Delete     Clear X (Esc also closes this page)
  S  L  X  P       Change sign, Last X, X<>Y, Pi
  Up / Down        Roll Up / Roll Down
  Ctrl+C / Ctrl+V  Copy X / Paste X
  F1               Help

CONVERSIONS
-----------
{{conv}}

ERRORS
------
Divide by zero, square root of a negative number and similar
invalid operations show a message and leave the stack unchanged.

SETTINGS
--------
Settings and the stack (X, Y, Z, T, Last X) are saved in the
app's private storage whenever they change and when the app
goes to the background. Clearing the app's storage in Android
Settings restores the defaults.

Back (button or gesture) closes Help, About and the editor.
""";
    }
}
