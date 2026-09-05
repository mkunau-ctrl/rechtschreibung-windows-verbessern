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

    /// <summary>Mitgelieferte Liste klassischer deutscher Rechtschreibfehler.</summary>
    public static string ClassicMistakes => Path.Combine(AppContext.BaseDirectory, "klassische-fehler.txt");

    /// <summary>Mitgelieferte eingedeutschte englische Tech-Verben (coden, committen, pushen …).</summary>
    public static string DenglischVerbs => Path.Combine(AppContext.BaseDirectory, "denglisch-verben.txt");

    /// <summary>Deutsche Wortformen, neben der .exe im Unterordner data.</summary>
    public static string WordListFile => Path.Combine(AppContext.BaseDirectory, "data", "woerter.txt");

    /// <summary>Worthäufigkeiten zum Sortieren der Kandidaten.</summary>
    public static string FrequencyFile => Path.Combine(AppContext.BaseDirectory, "data", "haeufigkeit.txt");

    /// <summary>Großgeschriebene Substantivformen für die Groß-/Kleinschreibung.</summary>
    public static string NounFile => Path.Combine(AppContext.BaseDirectory, "data", "substantive.txt");

    /// <summary>Wörter, die klein bleiben, auch wenn es gleichlautende Substantive gibt.</summary>
    public static string KeepLowercaseFile => Path.Combine(AppContext.BaseDirectory, "data", "klein-schreiben.txt");

    /// <summary>Wörter, die nur mit vorangehendem Artikel großgeschrieben werden (siehe Determiners).</summary>
    public static string AmbiguousNounsFile => Path.Combine(AppContext.BaseDirectory, "mehrdeutige-substantive.txt");

    /// <summary>Mitgelieferte Namen/Eigennamen (exakte Schreibweise) neben der .exe.</summary>
    public static string BundledNames => Path.Combine(AppContext.BaseDirectory, "data", "namen.txt");

    /// <summary>Eigene Namen des Nutzers, im Datenordner, editierbar.</summary>
    public static string UserNames => Path.Combine(DataDir, "eigene-namen.txt");

    public static void EnsureDataDir() => Directory.CreateDirectory(DataDir);
}
