# RPNCalc (Delphi, 1997): Reconstruction Spec

This spec was recovered from `Rpncalc.exe` (a 32-bit Delphi 2/3 GUI app, 246 KB) by reading the form resources (DFM), the event-handler table, embedded strings and some of the machine code. The program was not run.

## 1. Main window ("RPN Calculator")
- Small tool window, 223×283 client area. Has minimize and close buttons. Font is Arial Bold.
- **KeyPreview = True**, so the keyboard drives the calculator.
- The right-click menu (`PopupRPNMenu`) is attached to the whole form.

Layout, top to bottom:

| Panel | Contents |
|---|---|
| `pStack` (can be hidden) | T, Z and Y registers: right-aligned white labels, Arial 16 bold |
| `pMain` | X register; buttons **Enter**, **+/-**, **Last X**, **<** (backspace) |
| `pKeypad` (can be hidden) | Row 1: 1/x, yˣ, x², √x, π. Then digits 7-9 / 4-6 / 1-3 / 0 and `.`; operators − + × ÷ in the left column; X⇄Y, Roll Up and Roll Down in the right column |

There is also a hidden debug group box, "Flag Status", with EnterFlag and PushFlag check boxes. It should not be carried over.

## 2. Keyboard shortcuts (taken from the button hints)
| Key | Action |
|---|---|
| Enter | Enter (push X up the stack) |
| S | Change sign |
| L | Last X |
| Backspace | Delete the last digit |
| X | Exchange X and Y |
| U | Roll up |
| D | Roll down |
| P | Push π |

Digits, `.` and `+ - * /` are handled in `FormKeyDown`.

## 3. Stack behaviour
- It is a classic HP-style 4-level stack: X, Y, Z and T.
- **Enter flag:** after Enter, the next digit overwrites X instead of pushing.
- **Push flag:** after an operation, the next digit pushes the stack first.
- **Last X:** every operation and conversion saves the old X before changing it. The code confirms that the conversion handlers store X into a Last X variable before multiplying.
- Divide by zero shows the error "Divide by Zero".

## 4. Right-click menu (all items to keep)
- **Show Stack** (toggle): hides or shows the T/Z/Y panel.
- **Show Keypad** (toggle): hides or shows the keypad panel, leaving a compact X-only view.
- **Stay on Top** (toggle).
- **Decimal Places** → 0-9, shown as two columns: 0-4 | 5-9.
- **Format Type** → Fixed / Scientific.
- **Font Size** → Larger / Smaller.
- **Conversions**, each of which multiplies X by the constant stored in the exe:

| Conversion | Factor in the original |
|---|---:|
| Cubic Inches → Fluid Ounces (US) | 0.5541 |
| Cubic Inches → Milliliters | 16.386 |
| Fluid Ounces (US) → Cubic Inches | 1.8047 |
| Fluid Ounces (US) → Milliliters | 29.57 |
| Milliliters → Cubic Inches | 0.06102 |
| Milliliters → Fluid Ounces (US) | 0.03382 |
| Grams → lbs./M | 2.2 |

- A separator, then **Help** (it has no handler, so it did nothing), then **About**. About opens a box with Product Name, Version, Copyright, Comments and an OK button.

The "Grams → lbs./M" factor is 2.2, which matches grams per thousand pieces → pounds per million pieces (1 g/1000 = 2.2 lb/M). Please confirm that meaning.

## 5. Saved state (old: registry; new: settings file)
The original used Delphi `TRegIniFile` under `HKEY_CURRENT_USER\Software\…\RPNCalc`. It wrote these values:

`Position` section: Left, Top, Width, Height
`Options` section: Decimals, StackDisplay, KeyBoardDisplay, StayOnTop, FontSize, plus a format flag (Fixed/Scientific)

The binary does not clearly show whether the stack *values* were also saved.

**Recommended for the new version:** store the settings plus the X/Y/Z/T and Last X values in a JSON or INI file under `%APPDATA%\RPNCalc\`. That avoids the registry and UAC problems you have had since Windows 10.

## 6. Proposed rebuild
- Target: a native Windows desktop app, a single portable `.exe` with no installer.
- Keep the same look, the same keyboard shortcuts and every right-click item above.
- Improvements to decide on: exact conversion factors (for example 16.387064 mL/in³), a working Help item, and High-DPI scaling.
