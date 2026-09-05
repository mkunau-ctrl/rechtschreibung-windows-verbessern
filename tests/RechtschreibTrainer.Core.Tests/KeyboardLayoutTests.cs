using Xunit;

namespace RechtschreibTrainer.Core.Tests;

public class KeyboardLayoutTests
{
    [Theory]
    [InlineData('e', 'r')]  // Vertipper aus dem Benchmark: wettee -> wetter
    [InlineData('r', 'e')]  // symmetrisch
    [InlineData('a', 's')]
    [InlineData('n', 'm')]
    public void ErkenntNachbartastenAufDerselbenReihe(char a, char b)
    {
        Assert.True(KeyboardLayout.AreNeighbours(a, b));
    }

    [Theory]
    [InlineData('p', 'n')]  // Beleg aus OfflineCorrectorTests: darf NICHT als Nachbar gelten
    [InlineData('u', 'o')]  // eine Taste (i) liegt dazwischen
    [InlineData('a', 'p')]  // entgegengesetzte Enden der Reihe
    public void ErkenntNichtBenachbarteTastenAlsSolche(char a, char b)
    {
        Assert.False(KeyboardLayout.AreNeighbours(a, b));
    }

    [Fact]
    public void IgnoriertGrossKleinschreibung()
    {
        Assert.True(KeyboardLayout.AreNeighbours('E', 'r'));
        Assert.True(KeyboardLayout.AreNeighbours('e', 'R'));
    }

    [Fact]
    public void KenntUmlauteAlsTeilDerReihe()
    {
        // ü liegt neben p, ö und ä liegen neben l/ö.
        Assert.True(KeyboardLayout.AreNeighbours('p', 'ü'));
        Assert.True(KeyboardLayout.AreNeighbours('l', 'ö'));
        Assert.True(KeyboardLayout.AreNeighbours('ö', 'ä'));
    }

    [Fact]
    public void EinUnbekannterBuchstabeHatKeineNachbarn()
    {
        Assert.False(KeyboardLayout.AreNeighbours('e', '#'));
    }
}
