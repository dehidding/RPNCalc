# RPN Calculator for Windows 10/11

This is a modern re-implementation of my 1997 Delphi **RPNCalc**. It is a single portable `RPNCalc.exe` with no installer, no .NET install and no registry use. It is written in C# with WinForms on .NET 8, self-contained for win-x64, and is per-monitor DPI aware (PerMonitorV2).

## Download
Download the latest `RPNCalc.exe` from the [Releases page](https://github.com/dehidding/RPNCalc/releases/latest). It is a single self-contained Windows x64 file: no installer and no .NET install needed. The exe is unsigned, so Windows SmartScreen may show a warning the first time you run it (click **More info → Run anyway**).

## Features
- HP-style four-level stack (X, Y, Z, T) with **Last X**:
  - **Enter** copies X into Y, and the next number you type replaces X.
  - After an operation, the next number you type pushes the stack first.
  - Last X is saved before every operation and conversion.
  - Errors leave the stack unchanged and show a message: Divide by Zero, Invalid Input (for example √ of a negative number), and Overflow.
- Classic layout:
  - Stack panel with T/Z/Y.
  - X register with **Enter**, **+/-**, **Last X** and **<**.
  - Keypad: 1/x, y^x, x², √x and π on top; digits; − + × ÷ in the left column; X<>Y, Roll↑ and Roll↓ in the right column.
- Right-click menu:
  - Show Stack, Show Keypad and Stay on Top.
  - Decimal Places 0-9 in two columns (0-4 | 5-9).
  - Format Type: Fixed or Scientific, with check marks.
  - Font Size: Larger or Smaller.
  - Conversions.
  - Help and About.
- The window shrinks to fit when the stack or keypad is hidden. You can drag its left or right edge to make it wider.
- Long numbers automatically shrink so they fit in their register.
- Help window: press **F1** or use right-click → Help.

### Conversions (X is multiplied by the factor)
| Conversion | Factor |
|---|---|
| Cubic Inches → Fluid Ounces (US) | 16.387064 / 29.5735295625 (≈ 0.5541125541) |
| Cubic Inches → Milliliters | 16.387064 (exact) |
| Fluid Ounces (US) → Cubic Inches | 1.8046875 (exact) |
| Fluid Ounces (US) → Milliliters | 29.5735295625 (exact) |
| Milliliters → Cubic Inches | 1 / 16.387064 |
| Milliliters → Fluid Ounces (US) | 1 / 29.5735295625 |
| Grams → lbs./M | 2.2 ("M" is Roman numeral for 1,000) |

## Keyboard
Both the main keyboard and the numeric keypad work.

| Key | Action |
|---|---|
| 0-9, `.` | Type a number (`,` from the NumPad decimal key is also accepted) |
| `+` `-` `*` `/` | Add, subtract, multiply, divide |
| Enter / NumPad Enter | Enter |
| Backspace | Delete the last digit (clears X when you are not typing) |
| Esc / Delete | Clear X *(new)* |
| S | Change sign |
| L | Last X |
| X | Exchange X and Y |
| P | π |
| Up Arrow | Roll Up |
| Down Arrow | Roll Down |
| Ctrl+C / Ctrl+V | Copy X / paste a number into X *(new)* |
| F1 | Help |

Buttons never keep keyboard focus. Enter, the arrow keys and Space always go to the calculator, even right after you click a button.

## Settings file
Settings are loaded on start and saved on close to:

```
%APPDATA%\RPNCalc\settings.json
```

The file contains the window position and width, decimals, Fixed/Scientific format, show stack, show keypad, stay on top, font size, and the stack values X, Y, Z, T and Last X.

- If the file is missing or corrupt, the defaults are used.
- If a value is out of range, it is clamped to a valid value.
- If the saved position is no longer on any monitor, the window is moved onto the primary screen.
- To reset everything, delete the file.

## Project layout
```
RPNCalc.sln
src/RPNCalc.Engine/        Platform-independent engine: stack, operations, conversions, formatting, JSON settings
src/RPNCalc/               WinForms GUI (MainForm, HelpForm, AboutForm), icon from the original exe
tests/RPNCalc.Engine.Tests xUnit tests for the engine (run on Linux or Windows)
docs/                      Reconstruction spec recovered from the original 1997 Rpncalc.exe
```

## Building
You need the .NET 8 SDK. The same commands work on Windows, Linux and macOS. Cross-building from Linux is enabled with `EnableWindowsTargeting`.

```bash
dotnet test tests/RPNCalc.Engine.Tests                       # run the engine unit tests
dotnet publish src/RPNCalc/RPNCalc.csproj -c Release -o dist # -> dist/RPNCalc.exe (single file, self-contained, win-x64)
```

On Windows you can also open `RPNCalc.sln` in Visual Studio 2022 and press F5.

## License
Apache License 2.0. See [LICENSE](LICENSE).
