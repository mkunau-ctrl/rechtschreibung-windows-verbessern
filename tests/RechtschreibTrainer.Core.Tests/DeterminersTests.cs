using Xunit;

namespace RechtschreibTrainer.Core.Tests;

public class DeterminersTests
{
    [Theory]
    [InlineData("der")] [InlineData("die")] [InlineData("das")]
    [InlineData("ein")] [InlineData("eine")] [InlineData("einem")]
    [InlineData("mein")] [InlineData("meine")] [InlineData("deinem")]
    [InlineData("dieser")] [InlineData("jedes")] [InlineData("welcher")]
    public void ErkenntArtikelUndPossessivpronomen(string word)
    {
        Assert.True(Determiners.Contains(word));
    }

    [Fact]
    public void IgnoriertGrossKleinschreibung()
    {
        Assert.True(Determiners.Contains("Der"));
        Assert.True(Determiners.Contains("DIE"));
    }

    [Theory]
    [InlineData("Bäumen")] [InlineData("angekommen")] [InlineData("zuerst")]
    [InlineData("und")] [InlineData("bevor")] [InlineData("früh")]
    public void HaeltGewoehnlicheWoerterNichtFuerArtikel(string word)
    {
        Assert.False(Determiners.Contains(word));
    }
}
