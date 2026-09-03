namespace RechtschreibTrainer;

/// <summary>
/// Einfaches Diagnose-Log nach Dokumente\RechtschreibTrainer\debug.log.
/// Nur aktiv, solange die Datei existiert (oder eine leere Datei
/// "debug.on" im selben Ordner liegt) — im Normalbetrieb also aus.
/// </summary>
internal static class DebugLog
{
    private static readonly object Gate = new();
    private static readonly bool Enabled;
    private static readonly string Path;

    static DebugLog()
    {
        Path = System.IO.Path.Combine(AppPaths.DataDir, "debug.log");
        var flag = System.IO.Path.Combine(AppPaths.DataDir, "debug.on");
        Enabled = File.Exists(flag) || File.Exists(Path);
    }

    public static void Write(string message)
    {
        if (!Enabled) return;
        try
        {
            lock (Gate)
                File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
        }
        catch
        {
            // Diagnose darf nie den Betrieb stören.
        }
    }
}
