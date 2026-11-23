namespace RPNCalc;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Applies ApplicationHighDpiMode=PerMonitorV2, visual styles and default text rendering from the .csproj.
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.Message, "RPN Calculator", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.Run(new MainForm());
    }
}
