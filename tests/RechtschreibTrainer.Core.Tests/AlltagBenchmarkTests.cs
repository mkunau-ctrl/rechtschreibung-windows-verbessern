using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace RechtschreibTrainer.Core.Tests;

/// <summary>
/// Alltags-Benchmark MIT Satzkontext (Qualitätsrunde 3). Anders als
/// <see cref="BenchmarkTests"/> (einzelne Wörter ohne Kontext) läuft hier jeder
/// Fall wie im echten Programm: Zeichen für Zeichen durch den
/// <see cref="WordWatcher"/>, dann durch den <see cref="LiveCorrectionController"/>,
/// dessen Ersetzungsbefehle auf einen nachgebauten „Bildschirm" angewendet
/// werden. Bewertet wird, was am Ende auf dem Bildschirm steht.
///
/// Die Fälle in <c>alltag-faelle.tsv</c> sind den Mustern der echten
/// Tippdaten des Nutzers nachgebaut, aber mit neutralen Sätzen.
/// </summary>
public class AlltagBenchmarkTests
{
    private readonly ITestOutputHelper _out;
    public AlltagBenchmarkTests(ITestOutputHelper output) => _out = output;

    private sealed record Fall(string Kontext, string Getippt, string[] Erlaubt, bool DarfNichtAendern, string Kategorie);

    private static readonly Lazy<Fall[]> Faelle = new(Laden);
    private static readonly Lazy<OfflineCorrector> Corrector = new(RepoFiles.LoadCorrector);

    private static Fall[] Laden()
    {
        var pfad = Path.Combine(RepoFiles.Root, "tests", "RechtschreibTrainer.Core.Tests", "alltag-faelle.tsv");
        var faelle = new List<Fall>();
        foreach (var roh in File.ReadLines(pfad))
        {
            if (roh.Trim().Length == 0 || roh.TrimStart().StartsWith('#'))
                continue;

            var teile = roh.Split('\t');
            if (teile.Length != 4)
                throw new FormatException($"Zeile hat nicht 4 Spalten: {roh}");

            var erlaubt = teile[2].Split('|').Select(e => e == "-" ? teile[1] : e).ToArray();
            faelle.Add(new Fall(teile[0], teile[1], erlaubt, teile[2] == "-", teile[3]));
        }
        return [.. faelle];
    }

    /// <summary>
    /// Tippt Kontext + Wort + Leerzeichen und liefert, was danach auf dem
    /// Bildschirm vom Wort übrig ist (ohne das abschließende Leerzeichen).
    /// </summary>
    internal static string Tippe(OfflineCorrector corrector, string kontext, string getippt)
    {
        var bildschirm = new StringBuilder();
        var watcher = new WordWatcher();
        var controller = new LiveCorrectionController(
            corrector,
            cmd =>
            {
                bildschirm.Length -= Math.Min(cmd.DeleteCount, bildschirm.Length);
                bildschirm.Append(cmd.Insert);
            },
            _ => { });
        watcher.WordCompleted += w => controller.HandleWord(w);

        void Zeichen(char c)
        {
            bildschirm.Append(c);
            watcher.OnChar(c);
        }

        if (kontext.Length > 0)
        {
            foreach (var c in kontext) Zeichen(c);
            Zeichen(' ');
        }

        var marke = bildschirm.Length;
        foreach (var c in getippt) Zeichen(c);
        Zeichen(' ');

        return bildschirm.ToString(marke, bildschirm.Length - marke).TrimEnd(' ');
    }

    [Fact]
    public void MessungUndBericht()
    {
        Assert.True(RepoFiles.HasWordLists, "data/woerter.txt fehlt - siehe data/HERKUNFT.md");

        int geaendert = 0, geaendertRichtig = 0, noetig = 0, noetigRichtig = 0, sauber = 0, sauberAngefasst = 0;
        var fehler = new Dictionary<string, List<string>>();
        var proKategorie = new SortedDictionary<string, (int Gut, int Gesamt)>();

        foreach (var f in Faelle.Value)
        {
            var ergebnis = Tippe(Corrector.Value, f.Kontext, f.Getippt);
            var hatGeaendert = !string.Equals(ergebnis, f.Getippt, StringComparison.Ordinal);
            var richtig = f.Erlaubt.Contains(ergebnis, StringComparer.Ordinal);

            if (hatGeaendert)
            {
                geaendert++;
                if (richtig) geaendertRichtig++;
            }

            if (f.DarfNichtAendern)
            {
                sauber++;
                if (hatGeaendert && !richtig) sauberAngefasst++;
            }
            else
            {
                noetig++;
                if (richtig) noetigRichtig++;
            }

            var (gut, gesamt) = proKategorie.GetValueOrDefault(f.Kategorie);
            proKategorie[f.Kategorie] = (gut + (richtig ? 1 : 0), gesamt + 1);

            if (!richtig)
            {
                var art = !hatGeaendert ? "UEBERSEHEN" : f.DarfNichtAendern ? "FEHLALARM" : "FALSCH ERSETZT";
                if (!fehler.TryGetValue(art, out var liste)) fehler[art] = liste = [];
                liste.Add($"[{f.Kategorie}] „{f.Kontext} {f.Getippt}\" -> {ergebnis}   (erwartet: {string.Join(" / ", f.Erlaubt)})");
            }
        }

        var praezision = geaendert == 0 ? 1 : (double)geaendertRichtig / geaendert;
        var treffer = noetig == 0 ? 1 : (double)noetigRichtig / noetig;
        var fehlalarme = sauber == 0 ? 0 : (double)sauberAngefasst / sauber;

        _out.WriteLine("=========== ALLTAGS-BENCHMARK (mit Satzkontext) ===========");
        _out.WriteLine($"Faelle: {Faelle.Value.Length}");
        _out.WriteLine($"Praezision    {praezision:P1}   ({geaendertRichtig} von {geaendert} Aenderungen richtig)");
        _out.WriteLine($"Trefferquote  {treffer:P1}   ({noetigRichtig} von {noetig} noetigen Aenderungen)");
        _out.WriteLine($"Fehlalarme    {fehlalarme:P1}   ({sauberAngefasst} von {sauber} Woertern, die bleiben muessen)");
        _out.WriteLine("");
        _out.WriteLine("--- pro Kategorie (richtig/gesamt) ---");
        foreach (var (kat, (gut, gesamt)) in proKategorie)
            _out.WriteLine($"   {kat,-12} {gut,4}/{gesamt,-4} ({(double)gut / gesamt:P0})");
        foreach (var art in new[] { "FALSCH ERSETZT", "FEHLALARM", "UEBERSEHEN" })
        {
            if (!fehler.TryGetValue(art, out var liste)) continue;
            _out.WriteLine("");
            _out.WriteLine($"--- {art}: {liste.Count} ---");
            foreach (var z in liste) _out.WriteLine($"   {z}");
        }

        // Ratsche wie in BenchmarkTests: geprueft wird gegen den zuletzt
        // erreichten Stand. Wird etwas besser, die Werte unten hochsetzen.
        Assert.True(praezision >= StandPraezision,
            $"RUECKSCHRITT: Praezision {praezision:P1} unter dem erreichten Stand {StandPraezision:P1}");
        Assert.True(fehlalarme <= StandFehlalarme,
            $"RUECKSCHRITT: Fehlalarme {fehlalarme:P1} ueber dem erreichten Stand {StandFehlalarme:P1}");
        Assert.True(treffer >= StandTrefferquote,
            $"RUECKSCHRITT: Trefferquote {treffer:P1} unter dem erreichten Stand {StandTrefferquote:P1}");
    }

    // ---- Der erreichte Stand. Verbessert sich etwas, HIER hochsetzen. ----
    // 2026-10-21, Ausgangsmessung (vor Runde 3):
    //   Praezision 52,6 % | Trefferquote 67,8 % | Fehlalarme 46,4 %
    // 2026-10-21, nach Runde 3 (Technik/Satzanfang/Passwort im WordWatcher,
    //   Korpus-Grossschreibung, Kurzwoerter, Doppelbuchstaben, zwei Fehler,
    //   Echtwort-Fehler, fehlende Leertaste, Kompositum-Schutz):
    //   Praezision 100,0 % | Trefferquote 97,4 % | Fehlalarme 0,0 %
    private const double StandPraezision = 1.0;
    // 2026-10-22, +11 Faelle (Namen/Substantive ohne Woerterbuch-Eintrag):
    //   Praezision 100,0 % | Trefferquote 97,6 % (122/125) | Fehlalarme 0,0 %
    private const double StandTrefferquote = 0.976; // exakt 122/125
    private const double StandFehlalarme = 0.0;

    [Fact]
    public void JederFallIstEindeutig()
    {
        var doppelt = Faelle.Value
            .GroupBy(f => (f.Kontext, f.Getippt))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Kontext} | {g.Key.Getippt}")
            .ToList();
        Assert.True(doppelt.Count == 0, "Doppelte Faelle: " + string.Join(", ", doppelt));
    }
}
