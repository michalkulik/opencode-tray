using System.Windows.Forms;

namespace OpenCodeTray;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, name: @"Global\OpenCodeTray.SingleInstance", out var isNew);
        if (!isNew)
        {
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ShowError(e.ExceptionObject as Exception);

        var settings = AppSettings.Load();
        Application.Run(new TrayApplicationContext(settings));
    }

    private static void ShowError(Exception? exception)
    {
        if (exception is null)
        {
            return;
        }

        try
        {
            MessageBox.Show(
                exception.Message,
                "OpenCode Tray — błąd",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // Nothing more we can do.
        }
    }
}
