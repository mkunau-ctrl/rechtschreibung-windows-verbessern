using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace RechtschreibTrainer.Core.Tests;

/// <summary>
/// Ergänzt <see cref="BenchmarkTests"/> (isolierte Einzelwörter) um einen
/// zusammenhängenden Fließtext: ein ganzer, aus mehreren Sätzen bestehender
/// deutscher Absatz wird Wort für Wort durch den echten Korrektor geschickt,
/// mit demselben Satzanfang-Kontext, den auch das laufende Programm sieht.
///
/// Die Vertipper werden **programmgesteuert** erzeugt (Vertauschung,
/// Auslassung, Tastatur-Nachbar-Ersetzung, ue/oe/ae/ss-Ersatzschreibung,
/// vergessene Großschreibung) statt von Hand in den Text getippt — damit sich
/// kein Übertragungsfehler einschleicht und die erwartete "richtige" Fassung
/// exakt der Originaltext bleibt, gegen den mechanisch verglichen wird.
/// </summary>
public class PassageBenchmarkTests
{
    private readonly ITestOutputHelper _out;

    public PassageBenchmarkTests(ITestOutputHelper output) => _out = output;

    private const string OriginalText =
        "Der Herbst beginnt in diesem Jahr besonders früh. " +
        "Schon Anfang September fallen die ersten bunten Blätter von den großen Bäumen im Park. " +
        "Viele Menschen nutzen das schöne Wetter noch einmal für einen langen Spaziergang, bevor der Winter kommt. " +
        "Am Wochenende gehe ich gerne früh morgens los, wenn die Straßen noch ruhig sind und nur wenige Autos unterwegs sind. " +
        "Der Weg führt zuerst an einem kleinen Fluss entlang, dann durch einen dichten Wald mit hohen Tannen und alten Eichen. " +
        "Manchmal begegnet mir dort ein Reh, das schnell zwischen den Bäumen verschwindet, sobald es mich bemerkt. " +
        "Nach etwa einer Stunde erreiche ich einen Aussichtspunkt, von dem aus man die ganze Stadt überblicken kann. " +
        "Bei klarem Himmel sieht man sogar die Berge am Horizont, die im Sommer oft in einem leichten Dunst verschwinden. " +
        "Ich setze mich meistens auf eine der alten Holzbänke und genieße die Ruhe, bevor ich den Rückweg antrete. " +
        "Unterwegs komme ich häufig an einem kleinen Gasthaus vorbei, das schon früh am Morgen öffnet. " +
        "Dort trinke ich manchmal einen Kaffee und lese die Zeitung, während andere Gäste sich über die Nachrichten unterhalten. " +
        "Der Besitzer kennt mich mittlerweile und begrüßt mich jedes Mal freundlich mit meinem Namen. " +
        "Zuhause angekommen dusche ich zuerst und bereite anschließend ein einfaches Frühstück vor, meistens Brot mit Käse und frischem Obst. " +
        "Den restlichen Tag verbringe ich oft mit kleineren Aufgaben im Haushalt, bevor ich müde ins Bett falle und schon an den nächsten Spaziergang denke.";

    /// <summary>Nomen im Text, an denen zusätzlich das Vergessen der Großschreibung geprobt wird.</summary>
    private static readonly HashSet<string> Nouns = new(StringComparer.Ordinal)
    {
        "Herbst", "Jahr", "Wetter", "Winter", "Fluss", "Wald", "Stunde", "Himmel",
        "Berge", "Ruhe", "Morgen", "Kaffee", "Zeitung", "Namen", "Frühstück",
        "Brot", "Käse", "Obst", "Buch", "Familie", "Bett", "Park", "Menschen",
    };

    // Nur same-row-Nachbarn - dieselbe Vereinfachung wie KeyboardLayout.cs,
    // hier unabhängig nachgebaut, damit dieser Test keine internen Details
    // von KeyboardLayout kennen muss.
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

    /// <summary>Ein Wort im Text mit Kontext und der programmatisch erzeugten Tippfassung.</summary>
    private sealed record Slot(string Original, string Typed, bool IsSentenceStart, bool ErrorInjected);

    /// <summary>
    /// Baut die Tippfassung deterministisch: Wörter ab 6 Zeichen bekommen reihum
    /// (nach Wortindex) Vertauschung, Auslassung, Tastatur-Nachbar-Ersetzung
    /// oder Ersatzschreibung, dazwischen bleiben bewusst zwei von sechs
    /// unangetastet (Kontrollfälle). Kurze Funktionswörter (und, ich, die, der,
    /// das, in, an, im, es, mit, auf, von, für …) werden nie angefasst - sie
    /// sind die Fehlalarm-Kontrolle. Zusätzlich, unabhängig davon: jeder dritte
    /// Satzanfang und jedes zweite vorkommende Nomen aus <see cref="Nouns"/>
    /// werden klein getippt.
    /// </summary>
    private static List<Slot> BuildSlots()
    {
        var tokens = Regex.Matches(OriginalText, @"[A-Za-zÄÖÜäöüß]+|[.,!?]");
        var slots = new List<Slot>();

        var sentenceStart = true;
        var sentenceCounter = 0;
        var nounOccurrence = new Dictionary<string, int>(StringComparer.Ordinal);
        var wordIndex = 0;

        foreach (Match token in tokens)
        {
            var text = token.Value;
            if (text.Length == 1 && ".,!?".Contains(text[0]))
            {
                if (text is "." or "!" or "?")
                {
                    sentenceStart = true;
                    sentenceCounter++;
                }
                continue;
            }

            var isStart = sentenceStart;
            sentenceStart = false;

            var typed = text;
            var injected = false;

            if (text.Length >= 6)
            {
                switch (wordIndex % 6)
                {
                    case 1:
                        typed = Swap(text, text.Length / 2);
                        injected = true;
                        break;
                    case 2:
                        typed = Drop(text, text.Length / 2);
                        injected = true;
                        break;
                    case 3:
                        var withSub = SubstituteAdjacent(text, text.Length / 2);
                        if (withSub != text) { typed = withSub; injected = true; }
                        break;
                    case 5:
                        if (HasUmlaut(text)) { typed = AsciiFallback(text); injected = true; }
                        break;
                    // case 0, 4: Kontrollfall, unangetastet
                }
            }
            wordIndex++;

            if (isStart && sentenceCounter % 3 == 0)
            {
                typed = LowerFirst(typed);
                injected = true;
            }
            else if (Nouns.Contains(text))
            {
                var n = nounOccurrence.GetValueOrDefault(text);
                nounOccurrence[text] = n + 1;
                if (n % 2 == 0)
                {
                    typed = LowerFirst(typed);
                    injected = true;
                }
            }

            slots.Add(new Slot(text, typed, isStart, injected));
        }

        return slots;
    }

    [Fact]
    public void KorrekturqualitaetImFliesstext()
    {
        Assert.True(RepoFiles.HasWordLists, "data/woerter.txt fehlt - siehe data/HERKUNFT.md");

        var slots = BuildSlots();
        var corrector = RepoFiles.LoadCorrector();

        int total = slots.Count, injected = 0, controlWords = 0;
        int fixedRight = 0, fixedWrong = 0, missed = 0, falseAlarm = 0, unaffected = 0;
        List<string> wrongDetails = [], missedDetails = [], falseAlarmDetails = [];

        var typedOut = new List<string>();
        var correctedOut = new List<string>();
        string? previousTyped = null;

        foreach (var slot in slots)
        {
            // Wie im echten Betrieb (WordWatcher): das Indiz kommt vom
            // tatsächlich GETIPPTEN Vorwort, nicht vom beabsichtigten.
            var precededByDeterminer = previousTyped is not null && Determiners.Contains(previousTyped);
            var result = corrector.Correct(slot.Typed, new WordContext(slot.IsSentenceStart, PrecededByDeterminer: precededByDeterminer));
            previousTyped = slot.Typed;
            var output = result.HasCorrection ? result.Corrected : slot.Typed;

            typedOut.Add(slot.Typed);
            correctedOut.Add(output);

            var matches = string.Equals(output, slot.Original, StringComparison.Ordinal);

            if (slot.ErrorInjected)
            {
                injected++;
                if (matches) fixedRight++;
                else if (result.HasCorrection) { fixedWrong++; wrongDetails.Add($"{slot.Typed} -> {output} (erwartet: {slot.Original})"); }
                else { missed++; missedDetails.Add($"{slot.Typed} (erwartet: {slot.Original})"); }
            }
            else
            {
                controlWords++;
                if (matches) unaffected++;
                else { falseAlarm++; falseAlarmDetails.Add($"{slot.Original} -> {output}"); }
            }
        }

        var quote = (double)(fixedRight + unaffected) / total;
        var recall = injected == 0 ? 1 : (double)fixedRight / injected;
        var falseAlarmRate = controlWords == 0 ? 0 : (double)falseAlarm / controlWords;

        _out.WriteLine("=========== FLIESSTEXT-BENCHMARK ===========");
        _out.WriteLine($"Wörter gesamt: {total}  |  mit Vertipper: {injected}  |  Kontrollwörter (korrekt getippt): {controlWords}");
        _out.WriteLine("");
        _out.WriteLine($"GESAMTQUOTE (Wörter am Ende korrekt, wie im Original): {quote:P1}   ({fixedRight + unaffected} von {total})");
        _out.WriteLine($"  davon: Vertipper korrigiert {fixedRight}/{injected} ({recall:P1})   Kontrollwörter unangetastet {unaffected}/{controlWords} ({1 - falseAlarmRate:P1})");
        _out.WriteLine("");
        _out.WriteLine("--- Getippter Text ---");
        _out.WriteLine(string.Join(' ', typedOut));
        _out.WriteLine("");
        _out.WriteLine("--- Text nach der Korrektur ---");
        _out.WriteLine(string.Join(' ', correctedOut));
        _out.WriteLine("");
        _out.WriteLine("--- Original (Ziel) ---");
        _out.WriteLine(OriginalText);

        void Section(string title, List<string> items)
        {
            _out.WriteLine("");
            _out.WriteLine($"--- {title}: {items.Count} ---");
            foreach (var i in items) _out.WriteLine($"   {i}");
        }

        Section("FALSCH KORRIGIERT", wrongDetails);
        Section("ÜBERSEHEN", missedDetails);
        Section("FEHLALARME (korrektes Wort angefasst)", falseAlarmDetails);

        // Ratsche wie in BenchmarkTests: gegen den zuletzt erreichten Stand,
        // nicht gegen ein Endziel. Verbessert sich etwas, hier hochsetzen.
        //
        // 2026-09-05, erste Messung: Gesamtquote 93,3 % (222 von 238).
        // Alle beobachteten Fehlalarme waren echte Substantiv-Homographen aus
        // der 258k-Wortliste (Falle/Fallen, Dusche, nominalisierte Adjektive
        // wie Großen/Klarem/Schöne, Genitiv Morgens, ein durchgerutschtes
        // nominalisiertes Verb Überblicken) — dieselbe, schon dokumentierte
        // Lücke aus Plan-Phase 4 (Großschreibung ohne Satzkontext), hier nur
        // mit echten Zahlen aus Fließtext statt aus der Wortliste belegt.
        //
        // 2026-09-05, nach mehrdeutige-substantive.txt + PrecededByDeterminer:
        // Gesamtquote 96,6 % (230 von 238), Fehlalarme 9 -> 2. Behoben: früh,
        // fallen, dusche, falle, morgens, schöne (wo kein Artikel direkt davor
        // stand). Bekannte Grenze bleibt: "großen"/"dichten" stehen direkt
        // hinter einem Artikel, sind dort aber ein dekliniertes Adjektiv vor
        // dem eigentlichen Substantiv ("den großen Bäumen") - das
        // unterscheidet ein einzelnes Vorwort nicht von echter Nominalisierung
        // ("die Großen"). Bräuchte echtes Parsen, nicht mehr nur ein Vorwort.
        const double standGesamtquote = 0.966; // exakt 230/238 = 0,96638...
        Assert.True(quote >= standGesamtquote,
            $"RUECKSCHRITT: Gesamtquote {quote:P1} unter dem erreichten Stand {standGesamtquote:P1}");
    }
}
