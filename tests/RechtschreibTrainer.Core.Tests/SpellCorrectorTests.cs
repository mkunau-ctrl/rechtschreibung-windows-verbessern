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
    public void LeavesAKnownLowercaseWordAlone()
    {
        // Der Nutzer tippt klein - "haus" ist trotzdem korrekt geschrieben.
        var c = Build(["Haus", "Maus"]);

        Assert.Null(c.Suggest("haus"));
    }

    [Fact]
    public void FixesAWordWithExactlyOneCloseNeighbour()
    {
        var c = Build(["noch", "Nacht"]);

        Assert.Equal("noch", c.Suggest("nocg"));
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
        var c = Build(["Rasen", "Rosen"], "rasen 1000", "rosen 950");

        Assert.Null(c.Suggest("Rusen"));
    }

    [Fact]
    public void PicksTheMoreCommonWordWhenTheEditIsTheSameKind()
    {
        var c = Build(["Rasen", "Rosen"], "rasen 100000", "rosen 200");

        Assert.Equal("Rasen", c.Suggest("Rusen"));
    }
}
