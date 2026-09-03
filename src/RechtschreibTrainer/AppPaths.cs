namespace RechtschreibTrainer;

/// <summary>Feste Dateiorte des Programms.</summary>
internal static class AppPaths
{
    /// <summary>Ordner für alle Benutzer-Dateien: Dokumente\RechtschreibTrainer.</summary>
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "RechtschreibTrainer");

    public static string KeystrokeLog => Path.Combine(DataDir, "keystrokes.log");
    public static string CorrectionLog => Path.Combine(DataDir, "korrekturen.jsonl");
    public static string UserDictionary => Path.Combine(DataDir, "woerterbuch.txt");

    /// <summary>Mitgelieferte Standardliste, liegt neben der .exe.</summary>
    public static string BundledDictionary => Path.Combine(AppContext.BaseDirectory, "standard-vertipper.txt");

    public static void EnsureDataDir() => Directory.CreateDirectory(DataDir);
}
