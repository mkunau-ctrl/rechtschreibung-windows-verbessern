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
}
