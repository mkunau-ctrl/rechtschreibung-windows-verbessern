using Xunit;
using Xunit.Abstractions;

namespace RechtschreibTrainer.Core.Tests;

/// <summary>
/// Misst die Trefferquote auf <b>ungesehenen</b> gängigen Tippfehlern:
/// <c>gaengige-tippfehler-holdout.tsv</c> enthält Fälle, die bewusst
/// <b>nicht</b> in <c>haeufige-fehler.txt</c> ausgeliefert werden. Die Frage,
/// die dieser Test beantwortet: Wie viel davon fängt die restliche Kette
/// (Regeln, Ersatzschreibung, Raten gegen die große Wortliste) von allein?
///
/// Das ist <b>keine Ratsche</b> — die Zahl schwankt mit den Wortlisten. Der
/// Test ist ein Analyse-Werkzeug und schützt nur gegen einen groben Einbruch.
/// </summary>
public class GaengigeTippfehlerBenchmarkTests
{
    private readonly ITestOutputHelper _out;
    public GaengigeTippfehlerBenchmarkTests(ITestOutputHelper output) => _out = output;

    private static readonly string HoldoutPath = Path.Combine(
        RepoFiles.Root, "tests", "RechtschreibTrainer.Core.Tests", "gaengige-tippfehler-holdout.tsv");

    private sealed record Case(string Wrong, string Right);

    private static List<Case> Load()
    {
        var cases = new List<Case>();
        foreach (var raw in File.ReadLines(HoldoutPath))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var parts = line.Split('\t', StringSplitOptions.TrimEntries);
            Assert.True(parts.Length == 2, $"Zeile hat nicht 2 Spalten: {raw}");
            cases.Add(new Case(parts[0], parts[1]));
        }
        return cases;
    }

    [Fact]
    public void TrefferquoteAufUngesehenenTippfehlern()
    {
        Assert.True(RepoFiles.HasWordLists, "data/woerter.txt fehlt — siehe data/HERKUNFT.md");

        var cases = Load();
        var corrector = RepoFiles.LoadCorrector();
        var ctx = new WordContext(IsSentenceStart: false);

        int gefixt = 0, verschlimmert = 0, unberuehrt = 0;
        var falsch = new List<string>();
        var verpasst = new List<string>();

        foreach (var c in cases)
        {
            var got = corrector.Correct(c.Wrong, ctx);
            if (string.Equals(got.Corrected, c.Right, StringComparison.Ordinal))
            {
                gefixt++;
            }
            else if (got.HasCorrection)
            {
                verschlimmert++;
                falsch.Add($"{c.Wrong} -> {got.Corrected} (erwartet: {c.Right})");
            }
            else
            {
                unberuehrt++;
                verpasst.Add($"{c.Wrong} (erwartet: {c.Right})");
            }
        }

        double quote = (double)gefixt / cases.Count;
        double schaden = (double)verschlimmert / cases.Count;

        _out.WriteLine("===== TREFFERQUOTE AUF UNGESEHENEN TIPPFEHLERN =====");
        _out.WriteLine($"Fälle: {cases.Count}");
        _out.WriteLine($"  korrekt gefixt : {gefixt} ({quote:P1})");
        _out.WriteLine($"  falsch geändert: {verschlimmert} ({schaden:P1})");
        _out.WriteLine($"  unberührt      : {unberuehrt}");
        _out.WriteLine("");
        _out.WriteLine($"--- FALSCH GEÄNDERT: {falsch.Count} ---");
        foreach (var f in falsch) _out.WriteLine("   " + f);
        _out.WriteLine("");
        _out.WriteLine($"--- VERPASST (unberührt gelassen): {verpasst.Count} ---");
        foreach (var v in verpasst) _out.WriteLine("   " + v);

        // Weiche Schranken: KEINE Ratsche. Nur Schutz gegen einen groben
        // Einbruch. Das Holdout-Set ist bewusst hart (Zufallsstichprobe, auch
        // seltene Wörter wie "libysche", "frönen") — die "falsch geändert"-Fälle
        // sind Fehlgriffe des Rate-Schritts, die ein ausgeliefertes Paar gerade
        // verhindern würde. Stand bei Einführung (2026-09-07): 60/76 gefixt
        // (78,9 %), 7/76 falsch (9,2 %). Siehe PROJEKT-LOG.md.
        Assert.True(quote >= 0.55,
            $"Trefferquote auf ungesehenen Tippfehlern nur {quote:P1} — unter 55 %, etwas ist kaputt");
        Assert.True(schaden <= 0.15,
            $"{schaden:P1} der ungesehenen Tippfehler falsch geändert — über 15 %, Präzisionsproblem");
    }
}
