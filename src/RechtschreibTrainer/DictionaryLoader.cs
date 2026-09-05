using RechtschreibTrainer.Core;

namespace RechtschreibTrainer;

/// <summary>
/// Lädt das kombinierte Wörterbuch: erst die mitgelieferte Standardliste,
/// dann die editierbare Benutzerdatei (deren Einträge gewinnen). Fehlt die
/// Benutzerdatei, wird sie mit einer kurzen Anleitung angelegt.
/// </summary>
internal static class DictionaryLoader
{
    private const string UserFileHeader =
        "# Deine persönlichen Vertipper: eine Zeile je Eintrag, Format falsch=richtig\n" +
        "# Beispiel:  teh=the   oder   seperat=separat\n" +
        "# Zeilen mit # sind Kommentare. Diese Einträge haben Vorrang vor der Standardliste.\n";

    /// <summary>Ab wie vielen gleichen Korrekturen ein Wort automatisch ins Wörterbuch wandert.</summary>
    private const int LearnThreshold = 3;

    public static CorrectionDictionary Load() => Load(out _);

    /// <param name="learned">Neu gelernte Einträge, für eine Benachrichtigung im Tray.</param>
    public static CorrectionDictionary Load(out IReadOnlyList<DictionaryDistiller.Candidate> learned)
    {
        var lines = new List<string>();

        if (File.Exists(AppPaths.ClassicMistakes))
            lines.AddRange(File.ReadAllLines(AppPaths.ClassicMistakes));

        if (File.Exists(AppPaths.BundledDictionary))
            lines.AddRange(File.ReadAllLines(AppPaths.BundledDictionary));

        if (!File.Exists(AppPaths.UserDictionary))
        {
            AppPaths.EnsureDataDir();
            File.WriteAllText(AppPaths.UserDictionary, UserFileHeader);
        }

        learned = LearnFromOwnLog();

        lines.AddRange(File.ReadAllLines(AppPaths.UserDictionary));

        return CorrectionDictionary.FromLines(lines);
    }

    /// <summary>
    /// Verdichtet den Korrektur-Log beim Programmstart: Wörter, die mindestens
    /// <see cref="LearnThreshold"/> mal gleich korrigiert wurden und noch
    /// nicht im Wörterbuch stehen, wandern automatisch hinein. Steht ein Wort
    /// auf der „nie korrigieren"-Liste, wird es nie vorgeschlagen — diese
    /// Absicherung gilt aber ohnehin zur Laufzeit unabhängig davon (siehe
    /// <see cref="OfflineCorrector"/>), das hier verhindert nur unnötigen
    /// Wörterbuch-Wildwuchs.
    /// </summary>
    private static IReadOnlyList<DictionaryDistiller.Candidate> LearnFromOwnLog()
    {
        // Satzanfang-Grossschreibung wird bereits in DictionaryDistiller
        // selbst ausgeschlossen (reine Positionsregel, keine Rechtschreibung).
        var records = LearnStore.ReadAll(AppPaths.CorrectionLog).ToList();
        if (records.Count == 0)
            return [];

        var existing = CorrectionDictionary.FromLines(File.ReadAllLines(AppPaths.UserDictionary));
        var neverCorrect = File.Exists(AppPaths.NeverCorrectList)
            ? File.ReadAllLines(AppPaths.NeverCorrectList).Where(l => l.Length > 0 && !l.StartsWith('#'))
            : [];
        var ambiguousNouns = File.Exists(AppPaths.AmbiguousNounsFile)
            ? File.ReadAllLines(AppPaths.AmbiguousNounsFile).Where(l => l.Length > 0 && !l.StartsWith('#'))
            : [];

        var exclude = new HashSet<string>(neverCorrect, StringComparer.OrdinalIgnoreCase);
        exclude.UnionWith(ambiguousNouns); // dieselbe Begruendung: braucht Satzkontext, keine feste Regel
        exclude.UnionWith(records.Select(r => r.Before).Where(existing.HasEntry));

        var candidates = DictionaryDistiller.Distill(records, exclude, LearnThreshold);
        if (candidates.Count == 0)
            return candidates;

        var newLines = new List<string>
        {
            "",
            $"# Automatisch gelernt am {DateTime.Now:yyyy-MM-dd} (mindestens {LearnThreshold}x gleich korrigiert):",
        };
        newLines.AddRange(candidates.Select(c => $"{c.Before}={c.After}"));

        File.AppendAllLines(AppPaths.UserDictionary, newLines);
        DebugLog.Write($"Gelernt: {candidates.Count} neue Wörterbuch-Einträge ({string.Join(", ", candidates.Select(c => $"{c.Before}={c.After} ({c.Count}x)"))})");
        return candidates;
    }

    /// <summary>
    /// Lädt die große deutsche Wortliste. Fehlen die Datendateien, läuft das
    /// Programm ohne Rechtschreibprüfung weiter — nur mit den Vertipper-Listen.
    /// </summary>
    public static SpellCorrector? LoadSpelling()
    {
        if (!File.Exists(AppPaths.WordListFile))
            return null;

        IEnumerable<string> Optional(string path) =>
            File.Exists(path) ? File.ReadLines(path) : [];

        var list = WordList.FromLines(
            File.ReadLines(AppPaths.WordListFile).Concat(Optional(AppPaths.DenglischVerbs)),
            Optional(AppPaths.FrequencyFile),
            Optional(AppPaths.NounFile),
            Optional(AppPaths.KeepLowercaseFile),
            Optional(AppPaths.BundledNames).Concat(Optional(AppPaths.UserNames)),
            Optional(AppPaths.AmbiguousNounsFile));
        return new SpellCorrector(list, SpellSettings.Default);
    }
}
