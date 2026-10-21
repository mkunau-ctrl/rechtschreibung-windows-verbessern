using RechtschreibTrainer.Core;
using Xunit;

namespace RechtschreibTrainer.Core.Tests;

public class WordWatcherTests
{
    private static (WordWatcher watcher, List<WordCompleted> completed) Build()
    {
        var w = new WordWatcher();
        var seen = new List<WordCompleted>();
        w.WordCompleted += seen.Add;
        return (w, seen);
    }

    private static void Type(WordWatcher w, string text)
    {
        foreach (var c in text) w.OnChar(c);
    }

    [Fact]
    public void EmitsAWordWhenSpaceIsTyped()
    {
        var (w, seen) = Build();

        Type(w, "hallo welt ");

        Assert.Equal(2, seen.Count);
        Assert.Equal("hallo", seen[0].Word);
        Assert.Equal(' ', seen[0].Boundary);
        Assert.Equal("welt", seen[1].Word);
    }

    [Fact]
    public void FirstWordOfInputIsASentenceStart()
    {
        var (w, seen) = Build();

        Type(w, "hallo welt ");

        Assert.True(seen[0].Context.IsSentenceStart);
        Assert.False(seen[1].Context.IsSentenceStart);
    }

    [Fact]
    public void WordAfterAPeriodIsASentenceStart()
    {
        var (w, seen) = Build();

        Type(w, "eins. zwei ");

        Assert.Equal("zwei", seen[^1].Word);
        Assert.True(seen[^1].Context.IsSentenceStart);
    }

    [Fact]
    public void WortNachFrageZeichenInWoertlicherRedeIstKeinSatzanfang()
    {
        // "Wie war dein Tag?", fragte er - das Fragezeichen steckt in der
        // woertlichen Rede, der Satz geht mit dem Redebegleitsatz weiter.
        // Ohne den Fix wuerde "fragte" faelschlich grossgeschrieben.
        var (w, seen) = Build();

        Type(w, "„Wie war dein Tag?“, fragte er. ");

        var fragte = seen.Single(s => s.Word == "fragte");
        Assert.False(fragte.Context.IsSentenceStart);
    }

    [Fact]
    public void WortNachAusrufezeichenInWoertlicherRedeIstKeinSatzanfang()
    {
        var (w, seen) = Build();

        Type(w, "„Halt!“, rief sie. ");

        var rief = seen.Single(s => s.Word == "rief");
        Assert.False(rief.Context.IsSentenceStart);
    }

    [Fact]
    public void EchterSatzanfangNachSchliessendemAnfuehrungszeichenBleibtErhalten()
    {
        // "Ja." Dann ging sie. - kein Komma nach dem Zitat, also ein echter
        // neuer Satz. Muss weiterhin grossgeschrieben werden.
        var (w, seen) = Build();

        Type(w, "„Ja.“ dann ging sie. ");

        var dann = seen.Single(s => s.Word == "dann");
        Assert.True(dann.Context.IsSentenceStart);
    }

    [Fact]
    public void BackspaceShortensTheCurrentWord()
    {
        var (w, seen) = Build();

        Type(w, "hallox");
        w.OnBackspace();
        w.OnChar(' ');

        Assert.Single(seen);
        Assert.Equal("hallo", seen[0].Word);
    }

    [Fact]
    public void BackspacePastTheWordStartDropsThePartlyEditedWord()
    {
        var (w, seen) = Build();

        Type(w, "hallo welt ");
        w.OnBackspace();        // deletes the space -> editing earlier text
        Type(w, "en ");

        // "welten" must NOT be emitted: the buffer was reset by the edit
        Assert.DoesNotContain(seen, x => x.Word == "welten");
    }

    [Fact]
    public void EmitsTheFirstFullyTypedWordAfterInvalidate()
    {
        var (w, seen) = Build();

        w.Invalidate();          // e.g. the user clicked into a field
        Type(w, "danach ");

        var completed = Assert.Single(seen);
        Assert.Equal("danach", completed.Word);
        Assert.False(completed.Context.IsSentenceStart); // Kontext nach Sprung unbekannt
    }

    [Fact]
    public void TheWordRightAfterAnInterruptionMayBeAFragment()
    {
        // Nach Klick/Fokuswechsel kann der Cursor mitten in einem Wort stehen:
        // was danach getippt wird, ist vielleicht nur ein Bruchstück. Raten ist
        // dann zu gefährlich - nur exakte Treffer sind noch erlaubt.
        var (w, seen) = Build();

        w.Invalidate();
        Type(w, "toniert ");
        Type(w, "danach ");

        Assert.False(seen[0].Context.AllowSpellGuess);
        Assert.True(seen[1].Context.AllowSpellGuess);
    }

    [Fact]
    public void UninterruptedTypingAllowsGuessing()
    {
        var (w, seen) = Build();

        Type(w, "vielleciht ");

        Assert.True(seen[0].Context.AllowSpellGuess);
    }

    [Fact]
    public void InvalidateDiscardsThePartlyTypedWord()
    {
        var (w, seen) = Build();

        Type(w, "wel");        // Wort angefangen
        w.Invalidate();        // Cursor springt weg
        Type(w, "t ");

        var completed = Assert.Single(seen);
        Assert.Equal("t", completed.Word); // nicht "welt"
    }

    [Fact]
    public void EnterEmitsTheWordAndStartsANewSentence()
    {
        var (w, seen) = Build();

        Type(w, "hallo welt ");
        Type(w, "ende");
        w.OnEnter();
        Type(w, "neu ");

        Assert.Equal("ende", seen[^2].Word);
        Assert.Equal('\n', seen[^2].Boundary);
        Assert.True(seen[^1].Context.IsSentenceStart);
    }

    // ---- PrecededByDeterminer ----

    [Fact]
    public void MerktSichEinenVorangehendenArtikel()
    {
        var (w, seen) = Build();

        Type(w, "der Fluss ");

        Assert.False(seen[0].Context.PrecededByDeterminer); // "der" selbst hat kein Vorwort
        Assert.True(seen[1].Context.PrecededByDeterminer);  // "Fluss" nach "der"
    }

    [Fact]
    public void KeinArtikelWennDasVorwortKeinerIst()
    {
        var (w, seen) = Build();

        Type(w, "schnell fallen ");

        Assert.False(seen[1].Context.PrecededByDeterminer);
    }

    [Fact]
    public void VerwirftDenArtikelKontextNachEinemBruch()
    {
        var (w, seen) = Build();

        Type(w, "der ");
        w.Invalidate(); // z. B. Mausklick dazwischen
        Type(w, "fallen ");

        Assert.False(seen[1].Context.PrecededByDeterminer);
    }

    [Fact]
    public void ArtikelPlusDekliniertesAdjektivIstEinSubstantivBeleg()
    {
        // Runde 3: "die ganze zeit", "der grosse Fluss" - zwischen Artikel und
        // Substantiv steht ein dekliniertes Adjektiv (-e/-en/-er/-es/-em).
        var (w, seen) = Build();

        Type(w, "der grosse Fluss ");

        Assert.True(seen[1].Context.PrecededByDeterminer);  // "grosse" nach "der"
        Assert.True(seen[2].Context.PrecededByDeterminer);  // "Fluss" nach "der grosse"
    }

    [Fact]
    public void ArtikelZweiWorteDavorOhneAdjektivZaehltNicht()
    {
        var (w, seen) = Build();

        Type(w, "der hund schnell ");

        Assert.False(seen[2].Context.PrecededByDeterminer); // "schnell" nach "der hund"
    }

    [Theory]
    [InlineData("am ende ")]
    [InlineData("im spiel ")]
    [InlineData("zum testen ")]
    [InlineData("beim laufen ")]
    [InlineData("ins kino ")]
    public void KontraktionMitArtikelIstEinSubstantivBeleg(string text)
    {
        var (w, seen) = Build();

        Type(w, text);

        Assert.True(seen[1].Context.PrecededByDeterminer);
    }

    [Fact]
    public void ZahlDavorIstEinSubstantivBeleg()
    {
        var (w, seen) = Build();

        Type(w, "etwa 30 sekunden ");

        Assert.True(seen[2].Context.PrecededByDeterminer);
    }

    // ---- Technische Zeichenfolgen (E-Mail, URL, Pfad) ----

    private static IEnumerable<WordCompleted> Angefasst(IEnumerable<WordCompleted> seen) =>
        seen.Where(s => !s.Context.DoNotTouch);

    [Theory]
    [InlineData("test@beispiel.de ")]
    [InlineData("max.muster@gmx.de ")]
    [InlineData("www.beispiel.de ")]
    [InlineData("https://beispiel.de/seite ")]
    [InlineData(@"c:\users\test\datei ")]
    [InlineData("user_name ")]
    [InlineData("#hashtag ")]
    [InlineData("index.html ")]
    [InlineData("s24 ")]
    [InlineData("hallo2008 ")]
    public void TechnischeZeichenfolgenWerdenNieAngefasst(string text)
    {
        var (w, seen) = Build();

        Type(w, "schau ");
        Type(w, text);
        Type(w, "danach ");

        Assert.Equal(["schau", "danach"], Angefasst(seen).Select(s => s.Word));
    }

    [Fact]
    public void NachTechnischerZeichenfolgeGehtEsNormalWeiter()
    {
        var (w, seen) = Build();

        Type(w, "an test@gmx.de und dann ");

        Assert.Contains(seen, s => s.Word == "und" && !s.Context.DoNotTouch);
        Assert.False(seen.Single(s => s.Word == "und").Context.IsSentenceStart);
    }

    [Fact]
    public void WortVorPunktUndLeerzeichenWirdNormalGemeldet()
    {
        var (w, seen) = Build();

        Type(w, "das ist gut. dann ");

        var gut = seen.Single(s => s.Word == "gut");
        Assert.False(gut.Context.DoNotTouch);
        Assert.Equal('.', gut.Boundary);
        Assert.Equal(" ", gut.TypedAfterBoundary);   // das Leerzeichen steht schon da
        Assert.True(seen.Single(s => s.Word == "dann").Context.IsSentenceStart);
    }

    [Fact]
    public void DoppelpunktMitLeerzeichenIstNormaleSatzzeichen()
    {
        var (w, seen) = Build();

        Type(w, "hinweis: das ");

        Assert.All(seen, s => Assert.False(s.Context.DoNotTouch));
        Assert.False(seen[1].Context.IsSentenceStart);
    }

    [Fact]
    public void RuecktasteNachPunktHoltDasWortZurueck()
    {
        var (w, seen) = Build();

        Type(w, "gut.");
        w.OnBackspace();          // Punkt wieder weg
        Type(w, "e ");

        Assert.Equal("gute", Assert.Single(seen).Word);
    }

    [Fact]
    public void EnterNachPunktMeldetDasWortMitZeilenumbruch()
    {
        var (w, seen) = Build();

        Type(w, "ende.");
        w.OnEnter();

        var ende = Assert.Single(seen);
        Assert.Contains('\n', ende.TypedAfterBoundary);
    }

    // ---- Satzanfang ----

    [Theory]
    [InlineData("das kostet ca. zehn ")]
    [InlineData("siehe bzw. die ")]
    [InlineData("das ist z.B. ein ")]
    [InlineData("das war usw. und ")]
    [InlineData("am 19. oder ")]
    [InlineData("das ist ggf. besser ")]
    public void KeinSatzanfangNachAbkuerzungOderOrdinalzahl(string text)
    {
        var (w, seen) = Build();

        Type(w, text);

        Assert.False(seen[^1].Context.IsSentenceStart);
    }

    [Theory]
    [InlineData("gut? ja ")]
    [InlineData("gut! ja ")]
    public void FrageUndAusrufezeichenBeendenDenSatz(string text)
    {
        var (w, seen) = Build();

        Type(w, text);

        Assert.True(seen[^1].Context.IsSentenceStart);
    }

    // ---- Passwort-Kontext ----

    [Theory]
    [InlineData("das passwort ist geheim ")]
    [InlineData("mein kennwort lautet geheim ")]
    [InlineData("die pin ist geheim ")]
    [InlineData("password geheim ")]
    public void NachPasswortAusloeserWirdNichtHingesehen(string text)
    {
        var (w, seen) = Build();

        Type(w, text);

        var geheim = seen.Single(s => s.Word == "geheim");
        Assert.True(geheim.Context.DoNotTouch);
        Assert.True(geheim.Context.IsSecret);
    }

    [Fact]
    public void PasswortKontextEndetNachDreiWoertern()
    {
        var (w, seen) = Build();

        Type(w, "passwort eins zwei drei vier ");

        Assert.True(seen.Single(s => s.Word == "drei").Context.IsSecret);
        Assert.False(seen.Single(s => s.Word == "vier").Context.IsSecret);
    }

    [Fact]
    public void PasswortKontextEndetMitEnter()
    {
        var (w, seen) = Build();

        Type(w, "passwort geheim");
        w.OnEnter();
        Type(w, "danach ");

        Assert.True(seen.Single(s => s.Word == "geheim").Context.IsSecret);
        Assert.False(seen.Single(s => s.Word == "danach").Context.IsSecret);
    }

    [Fact]
    public void DasAusloesewortSelbstDarfKorrigiertWerden()
    {
        var (w, seen) = Build();

        Type(w, "das passwort ist ");

        Assert.False(seen.Single(s => s.Word == "passwort").Context.DoNotTouch);
    }
}
