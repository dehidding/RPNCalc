namespace RPNCalc;

/// <summary>
/// A button that never keeps keyboard focus, so Enter / arrow keys / Space always go to the calculator
/// instead of "clicking" the last button pressed with the mouse.
/// </summary>
internal sealed class NoFocusButton : Button
{
    public NoFocusButton()
    {
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        UseMnemonic = false;
        UseVisualStyleBackColor = true;
    }

    protected override bool ShowFocusCues => false;

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        // The native BUTTON takes focus on mouse-down; hand it back to the form after the click.
        // (Even while a button has focus, MainForm.ProcessCmdKey sees Enter/arrows/Space first.)
        if (FindForm() is Form f) { f.ActiveControl = null; f.Focus(); }
    }
}
