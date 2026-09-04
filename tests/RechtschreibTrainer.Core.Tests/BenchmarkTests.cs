using Xunit;
using Xunit.Abstractions;

namespace RechtschreibTrainer.Core.Tests;

/// <summary>Ein beschrifteter Benchmark-Fall aus benchmark-faelle.tsv.</summary>
public sealed record BenchmarkCase(string Typed, string? Expected, string Category)
{
    /// <summary>Muss das Programm hier ueberhaupt etwas aendern?</summary>
    public bool NeedsChange => Expected is not null;
}

/// <summary>
/// Die Messlatte fuer die Korrekturqualitaet (Plan Phase 0).
///
/// Drei Kennzahlen, absichtlich getrennt, weil sie unterschiedlich weh tun:
///
///   Praezision   Von allem, was das Programm anfasst: wie viel war richtig?
///                Wichtigste Zahl - eine falsche Ersetzung zerstoert einen
///                richtigen Satz und kostet Vertrauen.
///   Trefferquote Von allen noetigen Aenderungen: wie viele kamen?
///   Fehlalarme   Wie oft wird ein korrektes Wort angefasst? Ziel: nie.
///
/// Die Faelle der Kategorie "kontext" sind ohne Satzzusammenhang nicht
/// entscheidbar (Plan Phase 4) und werden deshalb getrennt ausgewiesen.
/// </summary>
public class BenchmarkTests
{
    private readonly ITestOutputHelper _out;

    public BenchmarkTests(ITestOutputHelper output) => _out = output;

    private static readonly Lazy<BenchmarkCase[]> Cases = new(Load);
    private static readonly Lazy<OfflineCorrector> Corrector = new(RepoFiles.LoadCorrector);

    private static BenchmarkCase[] Load()
    {
        var path = Path.Combine(
            RepoFiles.Root, "tests", "RechtschreibTrainer.Core.Tests", "benchmark-faelle.tsv");

        var cases = new List<BenchmarkCase>();
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var parts = line.Split('\t', StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
                throw new FormatException($"Zeile hat nicht 3 Spalten: {raw}");

            var expected = parts[1] == "-" ? null : parts[1];
            cases.Add(new BenchmarkCase(parts[0], expected, parts[2]));
        }

        return [.. cases];
    }

    /// <summary>Was der Korrektor aus dem Wort macht - null, wenn er es in Ruhe laesst.</summary>
    private static string? Apply(string word)
    {
        var result = Corrector.Value.Correct(word, new WordContext(IsSentenceStart: false));
        return result.HasCorrection ? result.Corrected : null;
    }

    private sealed record Report(
        int Changed, int ChangedRight, int NeedsChange, int NeedsChangeRight,
        int Clean, int CleanTouched, List<string> Wrong, List<string> Missed, List<string> FalseAlarms)
    {
        public double Precision => Changed == 0 ? 1 : (double)ChangedRight / Changed;
        public double Recall => NeedsChange == 0 ? 1 : (double)NeedsChangeRight / NeedsChange;
        public double FalseAlarmRate => Clean == 0 ? 0 : (double)CleanTouched / Clean;
    }

    private static Report Measure(IEnumerable<BenchmarkCase> cases)
    {
        int changed = 0, changedRight = 0, needs = 0, needsRight = 0, clean = 0, cleanTouched = 0;
        List<string> wrong = [], missed = [], falseAlarms = [];

        foreach (var c in cases)
        {
            var got = Apply(c.Typed);
            var right = string.Equals(got, c.Expected, StringComparison.Ordinal);

            if (got is not null)
            {
                changed++;
                if (right) changedRight++;
                else if (c.NeedsChange) wrong.Add($"{c.Typed} -> {got} (erwartet: {c.Expected})");
            }

            if (c.NeedsChange)
            {
                needs++;
                if (right) needsRight++;
                else if (got is null) missed.Add($"{c.Typed} (erwartet: {c.Expected})");
            }
            else
            {
                clean++;
                if (got is not null)
                {
                    cleanTouched++;
                    falseAlarms.Add($"{c.Typed} -> {got}");
                }
            }
        }

        return new Report(changed, changedRight, needs, needsRight, clean, cleanTouched,
            wrong, missed, falseAlarms);
    }

    [Fact]
    public void MessungUndBericht()
    {
        Assert.True(RepoFiles.HasWordLists, "data/woerter.txt fehlt - siehe data/HERKUNFT.md");

        var all = Cases.Value;
        var ohneKontext = all.Where(c => c.Category != "kontext").ToArray();
        var kontext = all.Where(c => c.Category == "kontext").ToArray();

        var r = Measure(ohneKontext);
        var k = Measure(kontext);

        _out.WriteLine("=========== BENCHMARK KORREKTURQUALITAET ===========");
        _out.WriteLine($"Faelle gesamt: {all.Length}  (davon kontextabhaengig: {kontext.Length})");
        _out.WriteLine("");
        _out.WriteLine("--- Hauptmessung (ohne die kontextabhaengigen Faelle) ---");
        _out.WriteLine($"Praezision    {r.Precision:P1}   ({r.ChangedRight} von {r.Changed} Aenderungen richtig)   Ziel >= 98%");
        _out.WriteLine($"Trefferquote  {r.Recall:P1}   ({r.NeedsChangeRight} von {r.NeedsChange} noetigen Aenderungen)   Ziel >= 90%");
        _out.WriteLine($"Fehlalarme    {r.FalseAlarmRate:P1}   ({r.CleanTouched} von {r.Clean} korrekten Woertern angefasst)   Ziel ~0%");
        _out.WriteLine("");
        _out.WriteLine($"--- Kontextabhaengige Faelle (Plan Phase 4): {k.CleanTouched} von {k.Clean} angefasst ---");
        foreach (var f in k.FalseAlarms) _out.WriteLine($"   {f}");

        void Section(string title, List<string> items)
        {
            _out.WriteLine("");
            _out.WriteLine($"--- {title}: {items.Count} ---");
            foreach (var i in items) _out.WriteLine($"   {i}");
        }

        Section("FALSCH ERSETZT (schlimmste Kategorie)", r.Wrong);
        Section("FEHLALARME (korrektes Wort angefasst)", r.FalseAlarms);
        Section("NICHT KORRIGIERT (uebersehen)", r.Missed);

        _out.WriteLine("");
        _out.WriteLine($"Noch bis zum Ziel:  Praezision {ZielPraezision - r.Precision:P1}   " +
                       $"Trefferquote {ZielTrefferquote - r.Recall:P1}");

        // Ratsche: geprueft wird gegen den zuletzt erreichten Stand, nicht gegen
        // das Endziel. So ist der Test gruen, schlaegt aber sofort an, wenn eine
        // Aenderung die Qualitaet verschlechtert. Wird etwas besser, werden die
        // Werte unten hochgesetzt - dann kann es nie wieder darunter fallen.
        Assert.True(r.Precision >= StandPraezision,
            $"RUECKSCHRITT: Praezision {r.Precision:P1} unter dem erreichten Stand {StandPraezision:P1}");
        Assert.True(r.FalseAlarmRate <= StandFehlalarme,
            $"RUECKSCHRITT: Fehlalarme {r.FalseAlarmRate:P1} ueber dem erreichten Stand {StandFehlalarme:P1}");
        Assert.True(r.Recall >= StandTrefferquote,
            $"RUECKSCHRITT: Trefferquote {r.Recall:P1} unter dem erreichten Stand {StandTrefferquote:P1}");
    }

    // ---- Der erreichte Stand. Verbessert sich etwas, HIER hochsetzen. ----
    // 2026-09-05, Phase 0 (Ausgangsmessung, noch keine Verbesserung gebaut):
    //   Praezision 94,9 % | Trefferquote 82,5 % | Fehlalarme 1,3 %
    private const double StandPraezision = 0.949;
    private const double StandTrefferquote = 0.824;
    private const double StandFehlalarme = 0.013;

    // ---- Das Ziel aus dem Plan (noch nicht erreicht). ----
    private const double ZielPraezision = 0.98;
    private const double ZielTrefferquote = 0.90;

    [Fact]
    public void JederFallIstEindeutigBeschriftet()
    {
        var doppelt = Cases.Value
            .GroupBy(c => c.Typed, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(doppelt.Count == 0, "Doppelte Faelle im Benchmark: " + string.Join(", ", doppelt));
    }
}
