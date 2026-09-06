using RechtschreibTrainer.Core;
using Xunit;

namespace RechtschreibTrainer.Core.Tests;

public class SpellCorrectorTests
{
    private static SpellCorrector Build(string[] words, params string[] frequencies)
        => new(WordList.FromLines(words, frequencies), SpellSettings.Default);

    [Fact]
    public void LeavesAKnownWordAlone()
    {
        var c = Build(["Haus", "Maus"]);

        Assert.Null(c.Suggest("Haus"));
    }

    [Fact]
    public void LeavesAGenuinelyLowercaseWordAlone()
    {
        var c = Build(["gehen", "laufen"]);

        Assert.Null(c.Suggest("gehen"));
    }

    [Fact]
    public void FixesAWordWithExactlyOneCloseNeighbour()
    {
        var c = Build(["wetter", "retter"]);

        Assert.Equal("wetter", c.Suggest("wettee"));
    }

    [Fact]
    public void FixesATransposition()
    {
        var c = Build(["vielleicht"]);

        Assert.Equal("vielleicht", c.Suggest("vielleciht"));
    }

    [Fact]
    public void LeavesShortWordsAloneBecauseTheyAreTooAmbiguous()
    {
        // "dan" liegt neben das/den/dann/an/da - jede Wahl wäre geraten.
        var c = Build(["das", "den", "dann", "dan".ToUpperInvariant()]);

        Assert.Null(c.Suggest("dan"));
    }

    [Fact]
    public void ReturnsNullWhenNothingIsClose()
    {
        var c = Build(["Haus", "noch"]);

        Assert.Null(c.Suggest("Kältekreislauf"));
    }

    [Fact]
    public void PrefersAForgottenLetterOverAWrongLetter()
    {
        // "gepannt": ein vergessenes 's' (gespannt) ist wahrscheinlicher als
        // ein danebengegriffenes 'p' statt 'n' (genannt) - selbst wenn
        // "genannt" etwas häufiger vorkommt.
        var c = Build(["gespannt", "genannt"], "gespannt 1000", "genannt 1500");

        Assert.Equal("gespannt", c.Suggest("gepannt"));
    }

    [Fact]
    public void ReturnsNullWhenTwoCandidatesAreTooCloseToCall()
    {
        // Beide nur ein ersetzter Buchstabe entfernt und etwa gleich häufig:
        // raten wäre schlechter als nichts tun.
        var c = Build(["rasten", "rosten"], "rasten 1000", "rosten 950");

        Assert.Null(c.Suggest("rusten"));
    }

    [Fact]
    public void PicksTheMoreCommonWordWhenTheEditIsTheSameKind()
    {
        var c = Build(["rasten", "rosten"], "rasten 100000", "rosten 200");

        Assert.Equal("rasten", c.Suggest("rusten"));
    }

    [Fact]
    public void CapitalisesANounThatWasTypedLowercase()
    {
        var c = Build(["Montag", "gehen"]);

        Assert.Equal("Montag", c.Suggest("montag"));
    }

    [Fact]
    public void DoesNotCapitaliseAWordThatIsAlsoValidLowercase()
    {
        var c = Build(["Essen", "essen"]);

        Assert.Null(c.Suggest("essen"));
    }

    [Fact]
    public void DoesNotShortenAShortForeignWordToAGermanOne()
    {
        // "skill" -> "kill" (ein Buchstabe weniger) ist zu billig: fast jedes
        // kurze Wort hat so einen Nachbarn. Bei <= 5 Zeichen kein Weglassen.
        var c = Build(["kill", "still"]);

        Assert.Null(c.Suggest("skill"));
    }

    [Fact]
    public void FixesAFiveLetterWordByAddingAForgottenLetter()
    {
        // Bei 5 Zeichen ist ein vergessener Buchstabe erlaubt (aber kein
        // Streichen/Ersetzen).
        var c = Build(["weiter"]);

        Assert.Equal("weiter", c.Suggest("witer"));
    }

    [Fact]
    public void DoesNotFuzzyMatchWordsShorterThanFive()
    {
        var c = Build(["frage", "trage"]);

        Assert.Null(c.Suggest("frae"));
    }

    [Fact]
    public void CorrectsSpellingAndCapitalisationTogether()
    {
        // "montg" -> vergessenes 'a' -> "montag" -> Substantiv -> "Montag"
        var c = Build(["Montag"]);

        Assert.Equal("Montag", c.Suggest("montg"));
    }

    // ---- Tastatur-Distanz (Phase 3) ----

    [Fact]
    public void BevorzugtEinenDanebengriffAufDieNachbartasteVorWeitEntferntenTasten()
    {
        // "wetter" (letztes 'r' statt 'e' getippt - r/e sind Nachbarn) gegen
        // "wetten" (letztes 'n' statt 'e' - n/e liegen weit auseinander).
        // "wetten" ist häufiger, aber die Nachbartaste ist der plausiblere
        // Vertipper und muss trotzdem gewinnen.
        var c = Build(["wetter", "wetten"], "wetter 10", "wetten 100000");

        Assert.Equal("wetter", c.Suggest("wettee"));
    }

    // "Ohne Tastaturnähe entscheidet weiterhin die Häufigkeit" ist bereits
    // durch PicksTheMoreCommonWordWhenTheEditIsTheSameKind abgedeckt
    // (u->a und u->o sind auf der QWERTZ-Tastatur beides keine Nachbarn).

    // ---- Verwechselbare Buchstaben i/e (Bugfix nach Nutzer-Feedback) ----

    [Fact]
    public void ErkenntEinVertauschtesEStattIAuchBeiFuenfBuchstaben()
    {
        // "necht" (5 Zeichen) ist bisher NIE korrigiert worden, weil bei
        // genau 5 Zeichen jedes Ersetzen abgeschaltet war - i/e-Vertipper
        // sind aber gerade bei kurzen, sehr häufigen Wörtern das Alltägliche.
        var c = Build(["nicht"]);

        Assert.Equal("nicht", c.Suggest("necht"));
    }

    [Fact]
    public void BevorzugtDasHaeufigereWortTrotzTastaturNachbarschaftDesAnderenKandidaten()
    {
        // "kerche": "Kirche" (e->i, kein Tastaturnachbar, aber verwechselbar)
        // gegen "Lerche" (k->l, Tastaturnachbar). Ohne den Fix gewinnt allein
        // wegen der Tastaturnähe "Lerche" - obwohl "Kirche" ungleich häufiger
        // ist. Beide müssen in derselben Gewichtsklasse landen, dann
        // entscheidet die Häufigkeit wie überall sonst auch.
        var c = Build(["Kirche", "Lerche"], "Kirche 10507", "Lerche 109");

        Assert.Equal("Kirche", c.Suggest("kerche"));
    }

    [Fact]
    public void ErsetztBeiFuenfBuchstabenWeiterhinNichtBeliebigeBuchstaben()
    {
        // Der gezielte i/e-Fix darf die bestehende Schutzschranke für
        // generisches Ersetzen bei kurzen Wörtern nicht aufweichen.
        var c = Build(["kill", "still"]);

        Assert.Null(c.Suggest("skill"));
    }

    // Zwei gleichzeitige Fehler (z.B. "korriegert" statt "korrigiert") wurden
    // versuchsweise über eine Verkettung zweier Ersetzungen umgesetzt und
    // gegen den Fließtext-Benchmark gemessen: 7 zusätzliche Treffer, aber 3
    // neue Fehlkorrekturen (Präzision 100% -> 97,2%, unter dem 98%-Ziel) -
    // z.B. "zustaedig" -> "zusteig" statt "zuständig". Ohne Sprachkontext
    // lässt sich ein zufällig getroffenes echtes Wort nicht von einem
    // gemeinten unterscheiden. Bewusst verworfen, siehe PROJEKT-LOG.

    // ---- Mehrdeutige Substantive (Phase 4) ----

    private static SpellCorrector BuildAmbiguous(string[] words, string[] nouns, params string[] ambiguousNouns)
        => new(WordList.FromLines(words, [], nouns: nouns, ambiguousNouns: ambiguousNouns), SpellSettings.Default);

    [Fact]
    public void LaesstEinMehrdeutigesSubstantivOhneArtikelKleinGeschrieben()
    {
        var c = BuildAmbiguous(["fallen"], ["Fallen"], "fallen");

        Assert.Null(c.Suggest("fallen", precededByDeterminer: false));
    }

    [Fact]
    public void GrossschreibtEinMehrdeutigesSubstantivMitArtikelDavor()
    {
        var c = BuildAmbiguous(["fallen"], ["Fallen"], "fallen");

        Assert.Equal("Fallen", c.Suggest("fallen", precededByDeterminer: true));
    }
}
