using RechtschreibTrainer.Core;
using Xunit;

namespace RechtschreibTrainer.Core.Tests;

public class OfflineCorrectorTests
{
    private static OfflineCorrector WithDict(params string[] lines)
        => new(CorrectionDictionary.FromLines(lines));

    [Fact]
    public void AppliesADictionaryEntry()
    {
        var result = WithDict("cih=ich").Correct("cih", new WordContext(IsSentenceStart: false));

        Assert.Equal("ich", result.Corrected);
        Assert.Equal(CorrectionSource.Dictionary, result.Source);
        Assert.True(result.HasCorrection);
    }

    [Fact]
    public void FallsBackToARuleWhenNoDictionaryEntry()
    {
        var result = WithDict().Correct("scgauen", new WordContext(IsSentenceStart: false));

        Assert.Equal("schauen", result.Corrected);
        Assert.Equal(CorrectionSource.Rule, result.Source);
    }

    [Fact]
    public void CapitalizesTheFirstWordOfASentence()
    {
        var result = WithDict().Correct("hallo", new WordContext(IsSentenceStart: true));

        Assert.Equal("Hallo", result.Corrected);
        Assert.Equal(CorrectionSource.Capitalization, result.Source);
    }

    [Fact]
    public void LeavesACorrectMidSentenceWordUntouched()
    {
        var result = WithDict("cih=ich").Correct("Rechtschreibung", new WordContext(IsSentenceStart: false));

        Assert.False(result.HasCorrection);
        Assert.Equal(CorrectionSource.None, result.Source);
    }

    [Fact]
    public void DoesNotCapitalizeMidSentenceLowercaseIch()
    {
        var result = WithDict().Correct("ich", new WordContext(IsSentenceStart: false));

        Assert.False(result.HasCorrection);
    }

    private static OfflineCorrector WithSpelling(string[] dict, params string[] knownWords)
        => new(CorrectionDictionary.FromLines(dict),
               new SpellCorrector(WordList.FromLines(knownWords, []), SpellSettings.Default));

    [Fact]
    public void FallsBackToTheSpellCheckerWhenNoEntryOrRuleApplies()
    {
        var result = WithSpelling([], "vielleicht")
            .Correct("vielleciht", new WordContext(IsSentenceStart: false));

        Assert.Equal("vielleicht", result.Corrected);
        Assert.Equal(CorrectionSource.Spelling, result.Source);
    }

    [Fact]
    public void DoesNotGuessWhenTheWordMayBeAFragment()
    {
        var result = WithSpelling([], "vielleicht")
            .Correct("vielleciht", new WordContext(IsSentenceStart: false, AllowSpellGuess: false));

        Assert.False(result.HasCorrection);
    }

    [Fact]
    public void StillUsesTheDictionaryWhenGuessingIsOff()
    {
        // Ein exakter Wörterbuch-Treffer ist auch auf einem Bruchstück sicher.
        var result = WithSpelling(["cih=ich"], "ich")
            .Correct("cih", new WordContext(IsSentenceStart: false, AllowSpellGuess: false));

        Assert.Equal("ich", result.Corrected);
    }

    [Fact]
    public void PersonalDictionaryWinsOverTheSpellChecker()
    {
        // Der Nutzer hat das letzte Wort: sein Eintrag schlägt jede Statistik.
        var result = WithSpelling(["vielleciht=vielleicht!"], "vielleicht")
            .Correct("vielleciht", new WordContext(IsSentenceStart: false));

        Assert.Equal("vielleicht!", result.Corrected);
        Assert.Equal(CorrectionSource.Dictionary, result.Source);
    }

    [Fact]
    public void LeavesAWordTheSpellCheckerKnowsAlone()
    {
        var result = WithSpelling([], "Haus")
            .Correct("Haus", new WordContext(IsSentenceStart: false));

        Assert.False(result.HasCorrection);
    }


    [Fact]
    public void NeverTouchesAWordOnTheNeverCorrectList()
    {
        // F10 hat dieses Wort schon einmal zurückgenommen — nie wieder anfassen.
        var corrector = new OfflineCorrector(
            CorrectionDictionary.FromLines(["kunau=Kuna"]),
            new SpellCorrector(WordList.FromLines(["Kuna"], []), SpellSettings.Default),
            neverCorrect: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "kunau" });

        var result = corrector.Correct("kunau", new WordContext(IsSentenceStart: false));

        Assert.False(result.HasCorrection);
    }

    [Fact]
    public void CombinesDictionaryFixWithSentenceStartCapitalization()
    {
        var result = WithDict("cih=ich").Correct("cih", new WordContext(IsSentenceStart: true));

        Assert.Equal("Ich", result.Corrected);
        Assert.Equal(CorrectionSource.Dictionary, result.Source);
    }

    // ---- Ersatzschreibung (ue/oe/ae/ss), Phase 2 ----

    [Fact]
    public void LoestUmlautErsatzschreibungAuf()
    {
        var result = WithSpelling([], "möchte")
            .Correct("moechte", new WordContext(IsSentenceStart: false));

        Assert.Equal("möchte", result.Corrected);
        Assert.Equal(CorrectionSource.Replacement, result.Source);
    }

    [Fact]
    public void RuehrtEinBereitsKorrektesWortMitSsNichtAn()
    {
        // "Masse" ist korrekt und ein ANDERES Wort als "Maße" — die
        // Ersatzschreibung darf ein bekanntes Wort niemals verfälschen.
        var result = WithSpelling([], "Masse", "Maße")
            .Correct("Masse", new WordContext(IsSentenceStart: false));

        Assert.False(result.HasCorrection);
    }

    [Fact]
    public void PersonalDictionaryWinsOverReplacementTable()
    {
        var result = WithSpelling(["fuer=Für (mein eigener Eintrag)"], "für")
            .Correct("fuer", new WordContext(IsSentenceStart: false));

        Assert.Equal("Für (mein eigener Eintrag)", result.Corrected);
        Assert.Equal(CorrectionSource.Dictionary, result.Source);
    }

    [Fact]
    public void ReplacementGreiftAuchWennDasWortEinBruchstueckSeinKoennte()
    {
        // Anders als das Fuzzy-Raten ist die Ersatzschreibung eine sichere,
        // nicht geratene Auflösung — sie darf auch auf einem Bruchstück laufen.
        var result = WithSpelling([], "möchte")
            .Correct("moechte", new WordContext(IsSentenceStart: false, AllowSpellGuess: false));

        Assert.Equal("möchte", result.Corrected);
        Assert.Equal(CorrectionSource.Replacement, result.Source);
    }

    [Fact]
    public void ReichtPrecededByDeterminerBisZurGrossschreibungsentscheidungDurch()
    {
        var corrector = new OfflineCorrector(
            CorrectionDictionary.FromLines([]),
            new SpellCorrector(
                WordList.FromLines(["fallen"], [], nouns: ["Fallen"], ambiguousNouns: ["fallen"]),
                SpellSettings.Default));

        var ohneArtikel = corrector.Correct("fallen", new WordContext(IsSentenceStart: false, PrecededByDeterminer: false));
        var mitArtikel = corrector.Correct("fallen", new WordContext(IsSentenceStart: false, PrecededByDeterminer: true));

        Assert.False(ohneArtikel.HasCorrection);
        Assert.Equal("Fallen", mitArtikel.Corrected);
    }

    [Fact]
    public void KeineErsetzungWennKeineAufloesungBekanntIst()
    {
        var result = WithSpelling([], "haus")
            .Correct("fuer", new WordContext(IsSentenceStart: false));

        Assert.False(result.HasCorrection);
    }

    [Fact]
    public void KombiniertErsatzschreibungMitEinemWeiterenVertipper()
    {
        // "zustaedig" ist zwei Fehler von "zuständig" entfernt: ae->ä UND ein
        // vergessenes n. Die Ersatzschreibung allein findet kein bekanntes
        // Wort ("zustädig" fehlt in der Liste) - erst das anschließende Raten
        // auf der aufgelösten Form findet den letzten Buchstaben.
        var result = WithSpelling([], "zuständig")
            .Correct("zustaedig", new WordContext(IsSentenceStart: false));

        Assert.Equal("zuständig", result.Corrected);
        Assert.Equal(CorrectionSource.Spelling, result.Source);
    }

    [Fact]
    public void VerkettungRuehrtEinBereitsKorrektesWortNichtAn()
    {
        // Belegter Fehlgriff: "besser" (korrekt) -> "beßer" (ss->ß, unbekannt)
        // -> geraten zu einem seltenen, aber existierenden Wort. Auch die
        // Verkettung muss die "schon richtig"-Absicherung respektieren.
        var result = WithSpelling([], "besser", "beißer")
            .Correct("besser", new WordContext(IsSentenceStart: false));

        Assert.False(result.HasCorrection);
    }

    [Fact]
    public void KombiniertErsatzschreibungNichtAufBruchstuecken()
    {
        // Das Verketten ist ein Raten (zwei Fehler kombiniert) - auf einem
        // möglichen Wort-Bruchstück bleibt das aus, anders als die reine,
        // ungeratene Ersatzschreibung.
        var result = WithSpelling([], "zuständig")
            .Correct("zustaedig", new WordContext(IsSentenceStart: false, AllowSpellGuess: false));

        Assert.False(result.HasCorrection);
    }
}
