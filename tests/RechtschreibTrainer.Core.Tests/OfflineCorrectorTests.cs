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

    [Fact]
    public void CombinesDictionaryFixWithSentenceStartCapitalization()
    {
        var result = WithDict("cih=ich").Correct("cih", new WordContext(IsSentenceStart: true));

        Assert.Equal("Ich", result.Corrected);
        Assert.Equal(CorrectionSource.Dictionary, result.Source);
    }
}
