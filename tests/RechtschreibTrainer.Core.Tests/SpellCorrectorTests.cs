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

    // ---- Runde 3: aus den echten Tippdaten ----

    // Haeufigkeiten aus data/haeufigkeit.txt (OpenSubtitles), gerundet.
    private static readonly string[] Alltag =
    [
        "ich", "mal", "will", "wild", "geht", "kann", "kahn", "noch", "noah", "mein", "guck",
        "auch", "nicht", "sind", "habe", "mach", "mich", "und", "ein", "eine", "Zeit", "kommt", "alles",
        "wenn", "das", "dem", "mit", "ja", "vor", "allem", "damit", "passt", "Spiel", "genau", "Genua", "meinen", "Mienen",
        "anderen", "Wanderern", "Lied", "Leid",
    ];

    private static readonly string[] AlltagHaeufigkeit =
    [
        "ich 5890279", "mal 435631", "will 293445", "wild 4282", "geht 290919", "kann 461755",
        "kahn 545", "noch 572486", "noah 1877", "mein 384765", "guck 8577", "auch 449250",
        "nicht 2756783", "sind 727078", "habe 636175", "mach 93345", "mich 893076", "und 2289891",
        "ein 1315301", "eine 1000000", "zeit 160393", "kommt 153845", "alles 366575", "wenn 694462", "das 3122198",
        "dem 400000", "mit 1014541", "ja 1054114", "vor 200000", "allem 30000", "damit 150000",
        "passt 40000", "spiel 50000", "genau 117667", "genua 117", "meinen 128288", "mienen 37",
        "anderen 86205", "wanderern 30", "lied 9000", "leid 30000",
    ];

    private static SpellCorrector BuildAlltag() =>
        new(WordList.FromLines(Alltag, AlltagHaeufigkeit, nouns: ["Zeit", "Spiel", "Mienen", "Wanderern", "Lied", "Leid"]),
            SpellSettings.Default);

    [Theory]
    [InlineData("kannn", "kann")]
    [InlineData("wennn", "wenn")]
    [InlineData("allles", "alles")]
    [InlineData("kommmmt", "kommt")]
    [InlineData("einn", "ein")]
    [InlineData("uund", "und")]
    [InlineData("jaa", "ja")]
    [InlineData("mmit", "mit")]
    public void EntferntDoppeltGetippteBuchstaben(string getippt, string erwartet)
    {
        Assert.Equal(erwartet, BuildAlltag().Suggest(getippt));
    }

    [Theory]
    [InlineData("ihc", "ich")]
    [InlineData("mla", "mal")]
    [InlineData("wil", "will")]
    [InlineData("kan", "kann")]
    [InlineData("noh", "noch")]
    [InlineData("mien", "mein")]
    [InlineData("ghet", "geht")]
    [InlineData("sidn", "sind")]
    [InlineData("ahbe", "habe")]
    [InlineData("auh", "auch")]
    [InlineData("niht", "nicht")]
    public void KorrigiertKurzeWoerterZuEinemSehrHaeufigenWort(string getippt, string erwartet)
    {
        Assert.Equal(erwartet, BuildAlltag().Suggest(getippt));
    }

    [Fact]
    public void KurzesWortOhneKlarenSiegerBleibtStehen()
    {
        // "mch": "mach" und "mich" sind beide sehr haeufig - nicht raten.
        Assert.Null(BuildAlltag().Suggest("mch"));
    }

    [Fact]
    public void KurzesWortMitNurSeltenemKandidatenBleibtStehen()
    {
        var c = Build(["kahn"], "kahn 545");

        Assert.Null(c.Suggest("kan"));
    }

    [Fact]
    public void ErgaenztKeinenVergessenenErstenBuchstaben()
    {
        // "anderern" -> "Wanderern" (vorn ein W ergaenzt) war ein echter
        // Fehlgriff; vorn vergessene Buchstaben sind meist Bruchstuecke.
        Assert.Equal("anderen", BuildAlltag().Suggest("anderern"));
    }

    [Theory]
    [InlineData("genua", "genau")]
    [InlineData("mienen", "meinen")]
    public void KorrigiertEinSeltenesWortDasFastSicherEinVertipperIst(string getippt, string erwartet)
    {
        Assert.Equal(erwartet, BuildAlltag().Suggest(getippt));
    }

    [Fact]
    public void MachtAusEinemHaeufigenEchtenWortKeinAnderes()
    {
        // "lied" ist selbst haeufig - kein Echtwort-Fehler, nur Grossschreibung.
        Assert.Equal("Lied", BuildAlltag().Suggest("lied"));
    }

    [Fact]
    public void GrossGetipptesSeltenesWortIstGemeint()
    {
        // Wer "Genua" gross tippt, meint die Stadt.
        Assert.Null(BuildAlltag().Suggest("Genua"));
    }

    [Theory]
    [InlineData("vorallem", "vor allem")]
    [InlineData("damitich", "damit ich")]
    [InlineData("demspiel", "dem Spiel")]
    [InlineData("japasst", "ja passt")]
    public void SetztEineFehlendeLeertaste(string getippt, string erwartet)
    {
        Assert.Equal(erwartet, BuildAlltag().Suggest(getippt));
    }

    [Fact]
    public void SehrHaeufigesWortSchlaegtSeltenesTrotzEtwasBessererFehlerArt()
    {
        // "anderern": "naderern" (vertauscht, Gewicht 1,0) gegen "anderen"
        // (ein Buchstabe zu viel, 0,9). Bei fast gleicher Fehlerart und
        // riesigem Haeufigkeitsunterschied gewinnt das haeufige Wort.
        var c = Build(["naderern", "anderen"], "anderen 86205");

        Assert.Equal("anderen", c.Suggest("anderern"));
    }

    [Fact]
    public void SeltenerOrtsnameKleinGetipptIstEinVertipper()
    {
        // "wil": nur als Ortsname "Wil" bekannt - klein getippt meint man "will".
        var c = new SpellCorrector(
            WordList.FromLines(["will"], ["will 293445"], properNouns: ["Wil"]), SpellSettings.Default);

        Assert.Equal("will", c.Suggest("wil"));
    }

    [Theory]
    [InlineData("vileicht", "vielleicht")]
    [InlineData("sytsme", "system")]
    public void KorrigiertZweiFehlerInLangenWoerternWennEindeutig(string getippt, string erwartet)
    {
        var c = Build(["vielleicht", "system", "systeme"], "vielleicht 200000", "system 20000", "systeme 1000");

        Assert.Equal(erwartet, c.Suggest(getippt));
    }

    [Theory]
    [InlineData("transportwege")]
    [InlineData("wartebereich")]
    [InlineData("präventionsmaßnahmen")]
    public void RaetNichtBeiEinemZerlegbarenKompositum(string wort)
    {
        var c = new SpellCorrector(WordList.FromLines(
            ["Transport", "Wege", "Transportweg", "warte", "Bereich", "Wertebereich", "Prävention", "Maßnahmen", "Präventionsmaßnahme"],
            [], nouns: ["Wege", "Bereich", "Maßnahmen", "Transport", "Prävention"]), SpellSettings.Default);

        var s = c.Suggest(wort);

        Assert.True(s is null || string.Equals(s, wort, StringComparison.OrdinalIgnoreCase),
            $"{wort} -> {s}");
    }

    [Theory]
    [InlineData("E-Mails")]
    [InlineData("Fleisch-")]
    [InlineData("Lenas")]
    public void LaesstBindestrichWoerterUndGenitivVonNamenInRuhe(string wort)
    {
        var c = new SpellCorrector(WordList.FromLines(
            ["E", "Mails", "Emails", "Fleisch", "Leonas"], [], properNouns: ["Lena"]), SpellSettings.Default);

        Assert.Null(c.Suggest(wort));
    }

    [Fact]
    public void TrenntKeinUnbekanntesWortHinterEinerPraepositionAufEinSubstantiv()
    {
        // "vorspiel" hinter "vor" waere ein Kompositum, kein fehlendes Leerzeichen.
        Assert.Null(BuildAlltag().Suggest("vorspiel"));
    }
}
