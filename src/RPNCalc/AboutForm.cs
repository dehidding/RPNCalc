using System.Reflection;

namespace RPNCalc;

/// <summary>About box: Product Name, Version, Copyright, Comments and OK (as in the original).</summary>
internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        var asm = Assembly.GetExecutingAssembly();
        string version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
                         ?? asm.GetName().Version?.ToString() ?? "1.1.1";

        Text = "About";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = UiFonts.Create("Segoe UI", 9F);
        ClientSize = new Size(340, 230);

        var icon = new PictureBox
        {
            Location = new Point(16, 16), Size = new Size(48, 48), SizeMode = PictureBoxSizeMode.Zoom,
        };
        try { icon.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath)?.ToBitmap(); } catch { }

        Label L(string text, int y, bool bold = false, int h = 20) => new()
        {
            Text = text, Location = new Point(80, y), Size = new Size(244, h), UseMnemonic = false,
            Font = bold ? UiFonts.Create("Segoe UI", 11F, FontStyle.Bold) : Font,
        };

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(85, 28), Location = new Point(239, 188) };
        Controls.AddRange(
        [
            icon,
            L("RPN Calculator", 16, bold: true, h: 24),
            L("Version " + version, 44),
            L("Copyright © David Hidding 2026", 68, h: 36),
            L("HP-style Reverse Polish Notation calculator for Windows 10/11. " +
              "Settings and stack are saved in %APPDATA%\\RPNCalc\\settings.json.", 110, h: 70),
            ok,
        ]);
        AcceptButton = ok;
        CancelButton = ok;
    }
}
