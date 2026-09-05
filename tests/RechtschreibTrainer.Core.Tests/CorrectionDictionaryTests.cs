using RechtschreibTrainer.Core;
using Xunit;

namespace RechtschreibTrainer.Core.Tests;

public class CorrectionDictionaryTests
{
    [Fact]
    public void LooksUpAnExactEntry()
    {
        var dict = CorrectionDictionary.FromLines(new[] { "cih=ich" });

        Assert.True(dict.TryCorrect("cih", out var corrected));
        Assert.Equal("ich", corrected);
    }

    [Fact]
    public void ReturnsFalseForUnknownWord()
    {
        var dict = CorrectionDictionary.FromLines(new[] { "cih=ich" });

        Assert.False(dict.TryCorrect("Rechtschreibung", out _));
    }

    [Fact]
    public void IgnoresCommentsAndBlankLines()
    {
        var dict = CorrectionDictionary.FromLines(new[]
        {
            "# das ist ein Kommentar",
            "",
            "   ",
            "nocg=noch",
        });

        Assert.True(dict.TryCorrect("nocg", out var corrected));
        Assert.Equal("noch", corrected);
    }

    [Fact]
    public void LaterEntryWinsOverEarlier()
    {
        var dict = CorrectionDictionary.FromLines(new[] { "foo=bar", "foo=baz" });

        Assert.True(dict.TryCorrect("foo", out var corrected));
        Assert.Equal("baz", corrected);
    }

    [Fact]
    public void PreservesLeadingCapitalOfTheTypedWord()
    {
        var dict = CorrectionDictionary.FromLines(new[] { "cih=ich" });

        Assert.True(dict.TryCorrect("Cih", out var corrected));
        Assert.Equal("Ich", corrected);
    }

    [Fact]
    public void HasEntryReportsWhetherAWordIsAlreadyListed()
    {
        var dict = CorrectionDictionary.FromLines(new[] { "cih=ich" });

        Assert.True(dict.HasEntry("cih"));
        Assert.False(dict.HasEntry("ich"));
    }
}
