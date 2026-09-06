using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace RechtschreibTrainer.Core.Tests;

/// <summary>
/// Großangelegter Test auf Nutzerwunsch: eine rund 10.000 Wörter lange,
/// zusammenhängende Geschichte mit möglichst vielfältigem Wortschatz
/// (<c>grosser-testtext.txt</c>, ca. 3.100 verschiedene Wortformen über
/// Alltag, Reise, Beruf, Natur, Wissenschaft, Feste). Anders als
/// <see cref="PassageBenchmarkTests"/> bekommen die Vertipper hier drei
/// klar benannte Schwierigkeitsgrade, und am Ende steht eine Auswertung,
/// welche Fehlerart am häufigsten danebengeht - nicht nur eine Gesamtquote.
///
/// Wichtig: Der getippte Gesamttext wird zeichenweise durch die echte
/// <see cref="WordWatcher"/>-Klasse geschickt (wie im laufenden Programm),
/// statt Satzanfang/Artikel-Kontext selbst nachzubauen. Genau das hat einen
/// echten Bug in WordWatcher aufgedeckt (Satzanfang nach wörtlicher Rede) -
/// eine eigene, vereinfachte Nachbildung hätte ihn verdeckt.
/// </summary>
public class GrosserTextBenchmarkTests
{
    private readonly ITestOutputHelper _out;
    public GrosserTextBenchmarkTests(ITestOutputHelper output) => _out = output;

    private enum Schwierigkeit { Kontrolle, LeichtNachbar, LeichtAuslassen, MittelVertauschen, MittelUmlaut, MittelGrossschreibung, SchwerDoppelt }

    private sealed record ErwartetesWort(string Original, string Typed, Schwierigkeit Grad);

    // Dieselbe Vereinfachung wie KeyboardLayout.cs (nur Nachbarn in derselben
    // Reihe), hier unabhängig nachgebaut, damit der Test keine internen
    // Details von KeyboardLayout kennen muss.
    private static readonly string[] Rows = ["qwertzuiopü", "asdfghjklöä", "yxcvbnm"];

    private static char? AdjacentOf(char c)
    {
        var lower = char.ToLowerInvariant(c);
        foreach (var row in Rows)
        {
            var i = row.IndexOf(lower);
            if (i < 0) continue;
            if (i + 1 < row.Length) return row[i + 1];
            if (i > 0) return row[i - 1];
        }
        return null;
    }

    private static string Swap(string w, int i) => w[..i] + w[i + 1] + w[i] + w[(i + 2)..];
    private static string Drop(string w, int i) => w[..i] + w[(i + 1)..];

    private static string SubstituteAdjacent(string w, int i)
    {
        var repl = AdjacentOf(w[i]);
        if (repl is null) return w;
        var c = char.IsUpper(w[i]) ? char.ToUpperInvariant(repl.Value) : repl.Value;
        return w[..i] + c + w[(i + 1)..];
    }

    private static string AsciiFallback(string w) => w
        .Replace("ü", "ue").Replace("Ü", "Ue")
        .Replace("ö", "oe").Replace("Ö", "Oe")
        .Replace("ä", "ae").Replace("Ä", "Ae")
        .Replace("ß", "ss");

    private static bool HasUmlaut(string w) => w.IndexOfAny(['ü', 'ö', 'ä', 'ß', 'Ü', 'Ö', 'Ä']) >= 0;
    private static string LowerFirst(string w) => char.ToLowerInvariant(w[0]) + w[1..];

    // Dasselbe Alphabet wie WordWatcher.IsWordChar (Buchstaben, Ziffern,
    // Bindestrich, Apostroph) - sonst zerlegt dieser Test "E-Mail" in zwei
    // Wörter, während WordWatcher es als eines behandelt, und die spätere
    // Zuordnung zwischen erwarteten und getippten Wörtern verrutscht.
    private const string TokenPattern = @"[\p{L}0-9\-']+";

    /// <summary>
    /// Legt für jedes Wort im Original deterministisch nach Wortindex die
    /// Tippfassung fest (Wörter ab 5 Zeichen, sonst greift ohnehin kein
    /// Raten): reihum 3 unangetastete Kontrollwörter, 2 leichte Fehler
    /// (Tastatur-Nachbar, ausgelassener Buchstabe), 3 mittlere (vertauscht,
    /// Umlaut-Ersatzschreibung, vergessene Großschreibung bei echten
    /// Substantiven) und 2 schwere Fälle (zwei Fehler gleichzeitig). Baut
    /// daraus außerdem den vollständigen getippten Text (gleiche
    /// Interpunktion/Leerzeichen wie das Original, nur die Wörter ersetzt).
    /// </summary>
    private static (List<ErwartetesWort> Erwartet, string GetippterText) BuildTyped(string original, WordList words)
    {
        var matches = Regex.Matches(original, TokenPattern);
        var erwartet = new List<ErwartetesWort>();
        var typedText = new StringBuilder();
        var cursor = 0;
        var wordIndex = 0;

        foreach (Match m in matches)
        {
            typedText.Append(original, cursor, m.Index - cursor);
            var t = m.Value;

            // Groß, nicht am Textanfang direkt nach Satzende, und die
            // Kleinschreibung ist ein bekanntes Wort -> mit hoher
            // Wahrscheinlichkeit ein echtes Substantiv (kein erfundener
            // Eigenname wie "Mia"/"Momo", die die Wortliste nicht kennt).
            var looksLikeRealNoun = char.IsUpper(t[0]) && words.Knows(LowerFirst(t));

            var typed = t;
            var grad = Schwierigkeit.Kontrolle;

            if (t.Length >= 5)
            {
                switch (wordIndex % 10)
                {
                    case 1:
                        var withSub = SubstituteAdjacent(t, t.Length / 2);
                        if (withSub != t) { typed = withSub; grad = Schwierigkeit.LeichtNachbar; }
                        break;
                    case 2:
                        typed = Drop(t, t.Length / 3);
                        grad = Schwierigkeit.LeichtAuslassen;
                        break;
                    case 3:
                        typed = Swap(t, t.Length / 2);
                        grad = Schwierigkeit.MittelVertauschen;
                        break;
                    case 4:
                        if (HasUmlaut(t)) { typed = AsciiFallback(t); grad = Schwierigkeit.MittelUmlaut; }
                        else if (looksLikeRealNoun) { typed = LowerFirst(t); grad = Schwierigkeit.MittelGrossschreibung; }
                        break;
                    case 6:
                        typed = Drop(Swap(t, t.Length / 2), Math.Max(0, t.Length / 2 - 1));
                        grad = Schwierigkeit.SchwerDoppelt;
                        break;
                    case 7:
                        var withSub2 = SubstituteAdjacent(t, t.Length / 2);
                        if (withSub2 != t) { typed = withSub2; grad = Schwierigkeit.LeichtNachbar; }
                        break;
                    case 8:
                        if (looksLikeRealNoun) { typed = LowerFirst(t); grad = Schwierigkeit.MittelGrossschreibung; }
                        else { typed = Swap(t, t.Length / 2); grad = Schwierigkeit.MittelVertauschen; }
                        break;
                    case 9:
                        var withSub3 = SubstituteAdjacent(t, Math.Max(0, t.Length / 3));
                        typed = Drop(withSub3 == t ? t : withSub3, t.Length - 2);
                        grad = Schwierigkeit.SchwerDoppelt;
                        break;
                    // case 0, 5: Kontrolle, unangetastet
                }
            }

            wordIndex++;
            typedText.Append(typed);
            erwartet.Add(new ErwartetesWort(t, typed, grad));
            cursor = m.Index + m.Length;
        }

        typedText.Append(original, cursor, original.Length - cursor);
        return (erwartet, typedText.ToString());
    }

    [Fact]
    public void KorrekturqualitaetImGrossenText()
    {
        Assert.True(RepoFiles.HasWordLists, "data/woerter.txt fehlt - siehe data/HERKUNFT.md");

        var textPath = Path.Combine(RepoFiles.Root, "tests", "RechtschreibTrainer.Core.Tests", "grosser-testtext.txt");
        var original = File.ReadAllText(textPath);

        var words = RepoFiles.LoadWordList();
        var (erwartet, getippterText) = BuildTyped(original, words);
        var corrector = new OfflineCorrector(RepoFiles.LoadDictionary(), new SpellCorrector(words, SpellSettings.Default));

        // Wie im echten Programm: der getippte Text laeuft zeichenweise durch
        // WordWatcher, der daraus Woerter samt Satzanfang-/Artikel-Kontext
        // macht - keine Nachbildung dieser Logik hier im Test.
        var watcher = new WordWatcher();
        var getippteWoerter = new List<WordCompleted>();
        watcher.WordCompleted += getippteWoerter.Add;
        foreach (var c in getippterText) watcher.OnChar(c);

        Assert.Equal(erwartet.Count, getippteWoerter.Count);

        var proGrad = Enum.GetValues<Schwierigkeit>().ToDictionary(g => g, _ => (total: 0, richtig: 0));
        int totalWords = erwartet.Count, totalInjected = 0, totalControl = 0;
        int fixedRight = 0, fixedWrong = 0, missed = 0, falseAlarm = 0, unaffected = 0;

        var missedByGrad = new Dictionary<Schwierigkeit, int>();
        var wrongByGrad = new Dictionary<Schwierigkeit, int>();
        var wrongDetails = new List<string>();
        var missedSamples = new List<string>();
        var falseAlarmDetails = new List<string>();

        for (var i = 0; i < erwartet.Count; i++)
        {
            var slot = erwartet[i];
            var getippt = getippteWoerter[i];
            Assert.Equal(slot.Typed, getippt.Word); // Ausrichtung pruefen: gleiche Reihenfolge, gleiches Wort

            var result = corrector.Correct(getippt.Word, getippt.Context);
            var output = result.HasCorrection ? result.Corrected : getippt.Word;
            var matches = string.Equals(output, slot.Original, StringComparison.Ordinal);

            var (total, richtig) = proGrad[slot.Grad];
            proGrad[slot.Grad] = (total + 1, richtig + (matches ? 1 : 0));

            if (slot.Grad == Schwierigkeit.Kontrolle)
            {
                totalControl++;
                if (matches) unaffected++;
                else { falseAlarm++; falseAlarmDetails.Add($"{slot.Original} -> {output}"); }
            }
            else
            {
                totalInjected++;
                if (matches) fixedRight++;
                else if (result.HasCorrection)
                {
                    fixedWrong++;
                    wrongByGrad[slot.Grad] = wrongByGrad.GetValueOrDefault(slot.Grad) + 1;
                    if (wrongDetails.Count < 40) wrongDetails.Add($"[{slot.Grad}] {slot.Typed} -> {output} (erwartet: {slot.Original})");
                }
                else
                {
                    missed++;
                    missedByGrad[slot.Grad] = missedByGrad.GetValueOrDefault(slot.Grad) + 1;
                    if (missedSamples.Count < 40) missedSamples.Add($"[{slot.Grad}] {slot.Typed} (erwartet: {slot.Original})");
                }
            }
        }

        var quote = (double)(fixedRight + unaffected) / totalWords;
        var recall = totalInjected == 0 ? 1 : (double)fixedRight / totalInjected;
        var falseAlarmRate = totalControl == 0 ? 0 : (double)falseAlarm / totalControl;

        _out.WriteLine("=========== GROSSER TEXT (10.000-WOERTER-BENCHMARK) ===========");
        _out.WriteLine($"Wörter gesamt: {totalWords}  |  mit Vertipper: {totalInjected}  |  Kontrollwörter: {totalControl}");
        _out.WriteLine("");
        _out.WriteLine($"GESAMTQUOTE: {quote:P1}   ({fixedRight + unaffected} von {totalWords})");
        _out.WriteLine($"  Trefferquote (Vertipper korrigiert): {recall:P1}  ({fixedRight}/{totalInjected})");
        _out.WriteLine($"  Fehlalarmrate (Kontrollwort angefasst): {falseAlarmRate:P2}  ({falseAlarm}/{totalControl})");
        _out.WriteLine($"  Falsch korrigiert: {fixedWrong}   Übersehen: {missed}");
        _out.WriteLine("");
        _out.WriteLine("--- Trefferquote pro Schwierigkeitsgrad ---");
        foreach (var grad in new[] {
            Schwierigkeit.LeichtNachbar, Schwierigkeit.LeichtAuslassen,
            Schwierigkeit.MittelVertauschen, Schwierigkeit.MittelUmlaut, Schwierigkeit.MittelGrossschreibung,
            Schwierigkeit.SchwerDoppelt })
        {
            var (total, richtig) = proGrad[grad];
            var pct = total == 0 ? 0 : (double)richtig / total;
            _out.WriteLine($"  {grad,-22} {richtig,5}/{total,-5} ({pct:P1})   uebersehen: {missedByGrad.GetValueOrDefault(grad),-4} falsch: {wrongByGrad.GetValueOrDefault(grad)}");
        }

        _out.WriteLine("");
        _out.WriteLine("--- Häufigste Fehlerart bei Nicht-Korrektur (uebersehen + falsch), absteigend ---");
        var kombiniert = Enum.GetValues<Schwierigkeit>()
            .Where(g => g != Schwierigkeit.Kontrolle)
            .Select(g => (Grad: g, Anzahl: missedByGrad.GetValueOrDefault(g) + wrongByGrad.GetValueOrDefault(g)))
            .OrderByDescending(x => x.Anzahl);
        foreach (var (grad, anzahl) in kombiniert)
            _out.WriteLine($"  {grad,-22} {anzahl} Faelle");

        void Section(string title, List<string> items)
        {
            _out.WriteLine("");
            _out.WriteLine($"--- {title}: {items.Count} (Ausschnitt, max. 40) ---");
            foreach (var i in items.Take(40)) _out.WriteLine($"   {i}");
        }

        Section("FALSCH KORRIGIERT", wrongDetails);
        Section("UEBERSEHEN", missedSamples);
        Section("FEHLALARME", falseAlarmDetails);

        _out.WriteLine("");
        _out.WriteLine("--- FEHLALARME nach Originalwort gruppiert (fuer Kuratierung) ---");
        foreach (var g in falseAlarmDetails.GroupBy(x => x.Split(" -> ")[0]).OrderByDescending(g => g.Count()))
            _out.WriteLine($"   {g.Key} x{g.Count()} -> {g.First().Split(" -> ")[1]}");

        // Reine Beobachtung/Auswertung auf Nutzerwunsch, keine Ratsche -
        // dieser Test dient der Analyse, nicht der Regressionssicherung
        // (dafür gibt es BenchmarkTests und PassageBenchmarkTests).
        Assert.True(totalWords > 9000, $"Text scheint zu kurz ({totalWords} Woerter) - Datei geprueft?");
    }
}
