using RechtschreibTrainer.Core;
using Xunit;

namespace RechtschreibTrainer.Core.Tests;

public class WordListTests
{
    private static WordList Build() => WordList.FromLines(
        words: new[] { "Haus", "noch", "Häuser", "gehst", "  ", "" },
        frequencyLines: new[] { "noch 12345", "haus 999", "kaputte zeile", "" });

    [Fact]
    public void KnowsAWordItWasGiven()
    {
        Assert.True(Build().Knows("Haus"));
    }

    [Fact]
    public void DoesNotKnowAnUnlistedWord()
    {
        Assert.False(Build().Knows("Kältekreislauf"));
    }

    [Fact]
    public void KnowsAWordRegardlessOfLeadingCapital()
    {
        // Der Nutzer tippt klein; "haus" muss als bekanntes Wort gelten,
        // damit es nicht "korrigiert" wird.
        var list = Build();

        Assert.True(list.Knows("haus"));
        Assert.True(list.Knows("Gehst"));
    }

    [Fact]
    public void SkipsBlankLines()
    {
        Assert.False(Build().Knows(" "));
    }

    [Fact]
    public void ReadsFrequencies()
    {
        Assert.Equal(12345, Build().Frequency("noch"));
    }

    [Fact]
    public void ReportsZeroFrequencyForAnUnknownWord()
    {
        Assert.Equal(0, Build().Frequency("Kältekreislauf"));
    }

    [Fact]
    public void LooksUpFrequencyCaseInsensitively()
    {
        Assert.Equal(999, Build().Frequency("Haus"));
    }
}
