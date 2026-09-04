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

    public static CorrectionDictionary Load()
    {
        var lines = new List<string>();

        if (File.Exists(AppPaths.BundledDictionary))
            lines.AddRange(File.ReadAllLines(AppPaths.BundledDictionary));

        if (!File.Exists(AppPaths.UserDictionary))
        {
            AppPaths.EnsureDataDir();
            File.WriteAllText(AppPaths.UserDictionary, UserFileHeader);
        }

        lines.AddRange(File.ReadAllLines(AppPaths.UserDictionary));

        return CorrectionDictionary.FromLines(lines);
    }

    /// <summary>
    /// Lädt die große deutsche Wortliste. Fehlen die Datendateien, läuft das
    /// Programm ohne Rechtschreibprüfung weiter — nur mit den Vertipper-Listen.
    /// </summary>
    public static SpellCorrector? LoadSpelling()
    {
        if (!File.Exists(AppPaths.WordListFile))
            return null;

        var frequencies = File.Exists(AppPaths.FrequencyFile)
            ? File.ReadLines(AppPaths.FrequencyFile)
            : [];

        var list = WordList.FromLines(File.ReadLines(AppPaths.WordListFile), frequencies);
        return new SpellCorrector(list, SpellSettings.Default);
    }
}
