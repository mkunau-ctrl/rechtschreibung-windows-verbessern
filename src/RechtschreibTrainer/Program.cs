namespace RechtschreibTrainer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            DebugLog.Write($"UNHANDLED: {e.ExceptionObject}");
        Application.ThreadException += (_, e) =>
            DebugLog.Write($"THREAD EXCEPTION: {e.Exception}");

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try
        {
            Application.Run(new TrayApp());
        }
        catch (Exception ex)
        {
            DebugLog.Write($"STARTUP FAILED: {ex}");
            throw;
        }
    }
}
