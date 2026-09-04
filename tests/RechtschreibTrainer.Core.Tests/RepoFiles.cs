namespace RechtschreibTrainer.Core.Tests;

/// <summary>
/// Findet die echten Datendateien im Repo und baut daraus denselben
/// <see cref="OfflineCorrector"/>, den das laufende Programm benutzt.
///
/// Warum nicht die Klassen aus dem Windows-Projekt? Die liegen in einer
/// WinExe und sind hier nicht referenzierbar. Diese Klasse bildet deshalb
/// nach, was <c>DictionaryLoader</c> tut — Reihenfolge und Quellen müssen
/// gleich bleiben, sonst misst der Benchmark etwas anderes als den Alltag.
/// </summary>
public static class RepoFiles
{
    /// <summary>Wurzel des Repos, gefunden über die Marker-Datei RechtschreibTrainer.sln.</summary>
    public static string Root { get; } = FindRoot();

    public static string DataDir => Path.Combine(Root, "data");

    /// <summary>Ordner mit den mitgelieferten Vertipper-Listen (neben der .exe).</summary>
    public static string BundledDir => Path.Combine(Root, "src", "RechtschreibTrainer");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "RechtschreibTrainer.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "RechtschreibTrainer.sln nicht gefunden — Repo-Wurzel unauffindbar.");
    }

    /// <summary>Sind die grossen Wortlisten da? (data/ liegt nicht in Git.)</summary>
    public static bool HasWordLists => File.Exists(Path.Combine(DataDir, "woerter.txt"));

    private static IEnumerable<string> Optional(string path) =>
        File.Exists(path) ? File.ReadLines(path) : [];

    /// <summary>Die grosse Wortliste, so wie das Programm sie laedt.</summary>
    public static WordList LoadWordList() => WordList.FromLines(
        File.ReadLines(Path.Combine(DataDir, "woerter.txt")),
        Optional(Path.Combine(DataDir, "haeufigkeit.txt")),
        Optional(Path.Combine(DataDir, "substantive.txt")),
        Optional(Path.Combine(DataDir, "klein-schreiben.txt")),
        Optional(Path.Combine(DataDir, "namen.txt")));

    /// <summary>Die mitgelieferten Vertipper-Listen, in der Ladereihenfolge des Programms.</summary>
    public static CorrectionDictionary LoadDictionary()
    {
        var lines = new List<string>();
        lines.AddRange(Optional(Path.Combine(BundledDir, "klassische-fehler.txt")));
        lines.AddRange(Optional(Path.Combine(BundledDir, "standard-vertipper.txt")));
        // Die persoenliche woerterbuch.txt des Nutzers bleibt bewusst aussen vor:
        // der Benchmark soll den Auslieferungszustand messen, nicht einen PC.
        return CorrectionDictionary.FromLines(lines);
    }

    /// <summary>Die vollstaendige Kette, wie sie im Betrieb laeuft.</summary>
    public static OfflineCorrector LoadCorrector() => new(
        LoadDictionary(),
        new SpellCorrector(LoadWordList(), SpellSettings.Default));
}
