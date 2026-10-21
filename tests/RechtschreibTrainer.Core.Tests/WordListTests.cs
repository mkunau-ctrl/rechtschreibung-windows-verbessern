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
    public void SkipsCommentLinesInTheWordList()
    {
        // Mitgelieferte Zusatzlisten (z. B. denglisch-verben.txt) haben einen
        // Kommentarkopf mit "#" - der darf nicht als Wort landen.
        var list = WordList.FromLines(["# Kommentar", "codest"], []);

        Assert.True(list.Knows("codest"));
        Assert.False(list.Knows("# Kommentar"));
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

    // ---- Mehrdeutige Substantive: nur mit vorangehendem Artikel grossschreiben ----

    [Fact]
    public void GrossschreibtEinMehrdeutigesSubstantivOhneVorangehendenArtikelNicht()
    {
        // "fallen" ist haeufig ein Verb ("die Blaetter fallen") und selten
        // ein Substantiv ("in mehreren Fallen") - ohne Artikel davor bleibt
        // es klein, auch wenn "Fallen" als Substantivform bekannt ist.
        var list = WordList.FromLines(["fallen"], [], nouns: ["Fallen"], ambiguousNouns: ["fallen"]);

        Assert.False(list.IsCapitalisedOnly("fallen", precededByDeterminer: false));
    }

    [Fact]
    public void GrossschreibtEinMehrdeutigesSubstantivMitVorangehendemArtikel()
    {
        var list = WordList.FromLines(["fallen"], [], nouns: ["Fallen"], ambiguousNouns: ["fallen"]);

        Assert.True(list.IsCapitalisedOnly("fallen", precededByDeterminer: true));
    }

    [Fact]
    public void UnbedenklicheSubstantiveBrauchenKeinenArtikel()
    {
        // "montag" steht NICHT auf der Liste der mehrdeutigen Substantive -
        // die alte, einfache Regel gilt unveraendert weiter.
        var list = WordList.FromLines(["montag"], [], nouns: ["Montag"], ambiguousNouns: ["fallen"]);

        Assert.True(list.IsCapitalisedOnly("montag", precededByDeterminer: false));
    }

    // ---- Runde 3: Grossschreibung nach Korpus-Belegen (data/schreibung.txt) ----

    private static WordList MitKorpus(string wort, int gross, int klein, bool kleinGueltig = true) =>
        WordList.FromLines(
            [wort], [],
            nouns: [char.ToUpperInvariant(wort[0]) + wort[1..]],
            casing: [$"{wort} {gross} {klein}"],
            lowercaseValid: kleinGueltig ? [wort] : []);

    [Fact]
    public void MeistKleinGeschriebenesWortBleibtKleinAuchMitArtikel()
    {
        // "würde": im Korpus mitten im Satz zu 99 % klein ("ich würde").
        var list = MitKorpus("würde", gross: 75, klein: 6417);

        Assert.False(list.IsCapitalisedOnly("würde", precededByDeterminer: false));
        Assert.False(list.IsCapitalisedOnly("würde", precededByDeterminer: true));
    }

    [Fact]
    public void FastImmerGrossGeschriebenesWortWirdOhneBelegGross()
    {
        // "dinge": 1.763 x gross, 0 x klein - obwohl "dinge" (von "dingen")
        // klein ein gueltiges Wort ist.
        var list = MitKorpus("dinge", gross: 1763, klein: 0);

        Assert.True(list.IsCapitalisedOnly("dinge", precededByDeterminer: false));
    }

    [Fact]
    public void GemischtesWortBrauchtEinenSubstantivBeleg()
    {
        // "essen": 30 % gross - "das Essen" vs. "wir essen".
        var list = MitKorpus("essen", gross: 1412, klein: 3324);

        Assert.False(list.IsCapitalisedOnly("essen", precededByDeterminer: false));
        Assert.True(list.IsCapitalisedOnly("essen", precededByDeterminer: true));
    }

    [Fact]
    public void OhneKorpusBelegBrauchtEinKleinGueltigesWortEinenBeleg()
    {
        // Zu wenige Belege (< 5): dann entscheidet, ob es klein gueltig ist.
        var list = MitKorpus("gebeten", gross: 0, klein: 2);

        Assert.False(list.IsCapitalisedOnly("gebeten", precededByDeterminer: false));
        Assert.True(list.IsCapitalisedOnly("gebeten", precededByDeterminer: true));
    }

    [Fact]
    public void OhneKorpusBelegUndNurGrossGueltigWirdGrossGeschrieben()
    {
        var list = MitKorpus("laptop", gross: 0, klein: 0, kleinGueltig: false);

        Assert.True(list.IsCapitalisedOnly("laptop", precededByDeterminer: false));
    }

    [Fact]
    public void MehrdeutigeListeErlaubtKeineGrossschreibungGegenDenKorpus()
    {
        // "drei" steht auf der Mehrdeutig-Liste - das darf "alle drei" aber
        // nicht grossziehen, wenn der Korpus fast nur "drei" klein kennt.
        var list = WordList.FromLines(
            ["drei"], [], nouns: ["Drei"], ambiguousNouns: ["drei"],
            casing: ["drei 19 4770"]);

        Assert.False(list.IsCapitalisedOnly("drei", precededByDeterminer: true));
    }

    [Fact]
    public void KleinBleibenListeBrauchtNurEinenBelegWennDerKorpusEsHergibt()
    {
        // "frage" steht auf klein-schreiben.txt ("ich frage"), ist im Korpus
        // aber zu 73 % gross - mit "eine" davor ist es das Substantiv.
        var list = WordList.FromLines(
            ["frage"], [], nouns: ["Frage"], keepLowercase: ["frage"],
            casing: ["frage 2237 828"]);

        Assert.False(list.IsCapitalisedOnly("frage", precededByDeterminer: false));
        Assert.True(list.IsCapitalisedOnly("frage", precededByDeterminer: true));
    }

    [Fact]
    public void KleinBleibenListeOhneKorpusBleibtKlein()
    {
        var list = WordList.FromLines(["weil"], [], nouns: ["Weil"], keepLowercase: ["weil"]);

        Assert.False(list.IsCapitalisedOnly("weil", precededByDeterminer: true));
    }

    [Theory]
    [InlineData("testen")]
    [InlineData("essen")]
    [InlineData("ändern")]
    public void InfinitivNachKontraktionWirdGross(string wort)
    {
        // "zum Testen", "beim Essen", "zum Ändern" - substantivierter Infinitiv.
        var stamm = wort.EndsWith("en") ? wort[..^2] : wort[..^1];
        var list = WordList.FromLines(
            [wort, stamm + "e", stamm + "t", stamm + "et"], [],
            nouns: [char.ToUpperInvariant(wort[0]) + wort[1..]],
            casing: [$"{wort} 0 50"]);

        Assert.True(list.IsCapitalisedOnly(wort, precededByDeterminer: true, precededByContraction: true));
        Assert.False(list.IsCapitalisedOnly(wort, precededByDeterminer: true, precededByContraction: false));
    }

    [Fact]
    public void AdjektivNachKontraktionBleibtKlein()
    {
        // "zum ersten Mal" - "ersten" ist kein Infinitiv (kein "erstet"/"erste"-Verb).
        var list = WordList.FromLines(
            ["ersten", "erste"], [], nouns: ["Ersten"], casing: ["ersten 3 900"]);

        Assert.False(list.IsCapitalisedOnly("ersten", precededByDeterminer: true, precededByContraction: true));
    }

    [Fact]
    public void VornameDerKleinMeistEinGewoehnlichesWortIstGiltNichtAlsName()
    {
        // "anders" ist auch ein Vorname (Anders) - im Satz aber fast immer das Adverb.
        var list = WordList.FromLines(
            ["anders"], [], properNouns: ["Anders"],
            casing: ["anders 3 1200"], lowercaseValid: ["anders"]);

        Assert.Null(list.ProperNounReading("anders"));
        Assert.Equal("Anders", list.ProperNoun("anders"));
    }

    [Fact]
    public void EchterEigennameBleibtEinName()
    {
        var list = WordList.FromLines([], [], properNouns: ["GitHub"]);

        Assert.Equal("GitHub", list.ProperNounReading("github"));
    }

    [Fact]
    public void MehrdeutigeListeVerlangtWeiterhinEinenBeleg()
    {
        var list = WordList.FromLines(
            ["fallen"], [], nouns: ["Fallen"], ambiguousNouns: ["fallen"],
            casing: ["fallen 950 50"]);

        Assert.False(list.IsCapitalisedOnly("fallen", precededByDeterminer: false));
    }
}
