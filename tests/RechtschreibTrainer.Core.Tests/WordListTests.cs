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

    [Fact]
    public void RecognisesAWordThatExistsOnlyCapitalised()
    {
        // "Haus" ist bekannt, "haus" als eigenständiges (klein geschriebenes)
        // Wort nicht -> es ist ein Substantiv und gehört groß.
        var list = WordList.FromLines(["Haus", "gehen"], []);

        Assert.True(list.IsCapitalisedOnly("haus"));
    }

    [Fact]
    public void DoesNotFlagAWordThatIsValidInLowercaseToo()
    {
        // "gehen" ist klein ein gültiges Wort -> nicht als Substantiv behandeln.
        var list = WordList.FromLines(["gehen", "Gehen"], []);

        Assert.False(list.IsCapitalisedOnly("gehen"));
    }

    [Fact]
    public void DoesNotFlagAnUnknownWord()
    {
        Assert.False(Build().IsCapitalisedOnly("kältekreislauf"));
    }

    [Fact]
    public void ReturnsTheExactFormOfAProperNoun()
    {
        var list = WordList.FromLines([], [], properNouns: ["GitHub", "iPhone", "Montag"]);

        Assert.Equal("GitHub", list.ProperNoun("github"));
        Assert.Equal("iPhone", list.ProperNoun("iphone"));
        Assert.Equal("Montag", list.ProperNoun("montag"));
    }

    [Fact]
    public void ReturnsNullForAWordThatIsNotAProperNoun()
    {
        var list = WordList.FromLines([], [], properNouns: ["GitHub"]);

        Assert.Null(list.ProperNoun("haus"));
    }
}
