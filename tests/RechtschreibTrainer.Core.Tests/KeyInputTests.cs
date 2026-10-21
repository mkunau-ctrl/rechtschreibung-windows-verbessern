using RechtschreibTrainer.Core;
using Xunit;

namespace RechtschreibTrainer.Core.Tests;

public class KeyInputTests
{
    [Fact]
    public void NormalesZeichenWirdDurchgereicht()
    {
        var r = KeyInput.Classify("a", ctrl: false, alt: false, win: false);

        Assert.Equal(KeyInputKind.Text, r.Kind);
        Assert.Equal("a", r.Text);
    }

    [Theory]
    [InlineData("@")]
    [InlineData("€")]
    [InlineData("{")]
    [InlineData("\\")]
    public void AltGrZeichenWerdenDurchgereicht(string zeichen)
    {
        // AltGr meldet Windows als Strg + Alt gleichzeitig.
        var r = KeyInput.Classify(zeichen, ctrl: true, alt: true, win: false);

        Assert.Equal(KeyInputKind.Text, r.Kind);
        Assert.Equal(zeichen, r.Text);
    }

    [Theory]
    [InlineData(true, false, false)]  // Strg+A, Strg+V, Strg+Z
    [InlineData(false, true, false)]  // Alt+Tab
    [InlineData(false, false, true)]  // Win+D
    public void TastenkuerzelBrechenDenKontext(bool ctrl, bool alt, bool win)
    {
        var r = KeyInput.Classify("a", ctrl, alt, win);

        Assert.Equal(KeyInputKind.ContextBreak, r.Kind);
    }

    [Fact]
    public void SteuerzeichenBrechenDenKontext()
    {
        // Strg+C liefert ohne Zustand "\u0003" statt eines Buchstabens.
        var r = KeyInput.Classify("\u0003", ctrl: false, alt: false, win: false);

        Assert.Equal(KeyInputKind.ContextBreak, r.Kind);
    }

    [Fact]
    public void TottasteBrichtDenKontext()
    {
        // ^ oder ´ allein: das naechste Zeichen wird kombiniert ("é"), was der
        // Hook nicht sicher nachbilden kann - das Wort lieber nicht anfassen.
        var r = KeyInput.Classify(null, ctrl: false, alt: false, win: false, deadKey: true);

        Assert.Equal(KeyInputKind.ContextBreak, r.Kind);
    }

    [Fact]
    public void TasteOhneZeichenWirdIgnoriert()
    {
        var r = KeyInput.Classify(null, ctrl: false, alt: false, win: false);

        Assert.Equal(KeyInputKind.Ignore, r.Kind);
    }
}
