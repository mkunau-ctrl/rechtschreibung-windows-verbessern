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

    /// <summary>Wörter, die der Nutzer per F10 zurückgewiesen hat.</summary>
    public static string NeverCorrectList => Path.Combine(DataDir, "nie-korrigieren.txt");

    /// <summary>Vom Nutzer editierbare Tastenbelegung.</summary>
    public static string HotkeyFile => Path.Combine(DataDir, "tasten.txt");

    /// <summary>Mitgelieferte Standardliste, liegt neben der .exe.</summary>
    public static string BundledDictionary => Path.Combine(AppContext.BaseDirectory, "standard-vertipper.txt");

    /// <summary>Deutsche Wortformen, neben der .exe im Unterordner data.</summary>
    public static string WordListFile => Path.Combine(AppContext.BaseDirectory, "data", "woerter.txt");

    /// <summary>Worthäufigkeiten zum Sortieren der Kandidaten.</summary>
    public static string FrequencyFile => Path.Combine(AppContext.BaseDirectory, "data", "haeufigkeit.txt");

    public static void EnsureDataDir() => Directory.CreateDirectory(DataDir);
}
