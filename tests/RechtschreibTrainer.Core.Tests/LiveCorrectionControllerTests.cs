using RechtschreibTrainer.Core;
using Xunit;

namespace RechtschreibTrainer.Core.Tests;

public class LiveCorrectionControllerTests
{
    private static LiveCorrectionController Build(
        out List<ReplacementCommand> replacements,
        out List<CorrectionRecord> learned,
        params string[] dict)
    {
        var reps = new List<ReplacementCommand>();
        var learn = new List<CorrectionRecord>();
        replacements = reps;
        learned = learn;
        var corrector = new OfflineCorrector(CorrectionDictionary.FromLines(dict));
        return new LiveCorrectionController(corrector, reps.Add, learn.Add);
    }

    private static WordCompleted Word(string w, char boundary = ' ', bool sentenceStart = false)
        => new(w, boundary, new WordContext(sentenceStart));

    [Fact]
    public void ReplacesAMisspelledWordIncludingTheBoundaryChar()
    {
        var c = Build(out var reps, out _, "cih=ich");

        c.HandleWord(Word("cih"));

        var cmd = Assert.Single(reps);
        Assert.Equal(4, cmd.DeleteCount);   // "cih" + space
        Assert.Equal("ich ", cmd.Insert);
    }

    [Fact]
    public void DoesNothingForACorrectWord()
    {
        var c = Build(out var reps, out var learned, "cih=ich");

        c.HandleWord(Word("Haus"));

        Assert.Empty(reps);
        Assert.Empty(learned);
    }

    [Fact]
    public void RecordsEveryAppliedCorrection()
    {
        var c = Build(out _, out var learned, "cih=ich");

        c.HandleWord(Word("cih"));

        var rec = Assert.Single(learned);
        Assert.Equal("cih", rec.Before);
        Assert.Equal("ich", rec.After);
        Assert.Equal(CorrectionSource.Dictionary, rec.Source);
    }

    [Fact]
    public void DoesNothingWhilePaused()
    {
        var c = Build(out var reps, out _, "cih=ich");
        c.Paused = true;

        c.HandleWord(Word("cih"));

        Assert.Empty(reps);
    }

    [Fact]
    public void UndoRestoresTheOriginalWord()
    {
        var c = Build(out var reps, out _, "cih=ich");
        c.HandleWord(Word("cih"));
        reps.Clear();

        c.Undo();

        var cmd = Assert.Single(reps);
        Assert.Equal(4, cmd.DeleteCount);   // "ich " that was inserted
        Assert.Equal("cih ", cmd.Insert);
    }

    [Fact]
    public void UndoReportsTheWordSoItIsNeverCorrectedAgain()
    {
        var c = Build(out _, out _, "kunau=Kuna");
        var rejected = new List<string>();
        c.CorrectionRejected += rejected.Add;

        c.HandleWord(Word("kunau"));
        c.Undo();

        Assert.Equal(["kunau"], rejected);
    }

    [Fact]
    public void ReportsNothingRejectedWhenThereWasNoCorrection()
    {
        var c = Build(out _, out _, "cih=ich");
        var rejected = new List<string>();
        c.CorrectionRejected += rejected.Add;

        c.Undo();

        Assert.Empty(rejected);
    }

    [Fact]
    public void UndoDoesNothingWhenNothingWasReplaced()
    {
        var c = Build(out var reps, out _, "cih=ich");

        c.Undo();

        Assert.Empty(reps);
    }

    [Fact]
    public void KorrigiertNachEnterGarNicht()
    {
        // Nach Enter steht der Zeilenumbruch zwischen Cursor und Wort. Um an das
        // Wort zu kommen, müsste er mitgelöscht und neu getippt werden — in
        // Chat-Fenstern verschickt das die Nachricht ein zweites Mal. Lieber
        // nicht korrigieren.
        var c = Build(out var reps, out var learned, "cih=ich");

        c.HandleWord(Word("cih", boundary: '\n'));

        Assert.Empty(reps);
        Assert.Empty(learned);
    }

    [Fact]
    public void BeruecksichtigtWasSeitDerWortgrenzeGetipptWurde()
    {
        // Der Nutzer tippt weiter, während die Korrektur noch aussteht. Auf dem
        // Bildschirm steht dann schon "cih ab" — es müssen also 6 Zeichen weg
        // und "ich ab" neu getippt werden. Sonst fressen die Rücktasten das
        // nächste Wort an.
        var c = Build(out var reps, out _, "cih=ich");

        c.HandleWord(Word("cih"), typedSince: "ab");

        var cmd = Assert.Single(reps);
        Assert.Equal(6, cmd.DeleteCount);   // "cih" + Leerzeichen + "ab"
        Assert.Equal("ich ab", cmd.Insert);
    }

    [Fact]
    public void UndoStelltAuchDasInzwischenGetippteWiederHer()
    {
        var c = Build(out var reps, out _, "cih=ich");
        c.HandleWord(Word("cih"), typedSince: "ab");
        reps.Clear();

        c.Undo();

        var cmd = Assert.Single(reps);
        Assert.Equal(6, cmd.DeleteCount);   // "ich ab"
        Assert.Equal("cih ab", cmd.Insert);
    }

    [Fact]
    public void OhneNachgetippteZeichenBleibtEsBeimEinfachenFall()
    {
        var c = Build(out var reps, out _, "cih=ich");

        c.HandleWord(Word("cih"), typedSince: "");

        var cmd = Assert.Single(reps);
        Assert.Equal(4, cmd.DeleteCount);
        Assert.Equal("ich ", cmd.Insert);
    }

    // ---- Runde 3: Ruecktaste direkt nach einer Korrektur = Ablehnung ----

    [Fact]
    public void RuecktasteInsKorrigierteWortHineinIstEineAblehnung()
    {
        var c = Build(out _, out var learned, "cih=ich");
        var abgelehnt = new List<CorrectionRecord>();
        c.CorrectionRejectedByUser += abgelehnt.Add;

        c.HandleWord(Word("cih"));   // Bildschirm: "ich "
        c.OnBackspace();             // Leerzeichen weg - noch keine Ablehnung
        Assert.Empty(abgelehnt);
        c.OnBackspace();             // jetzt im korrigierten Wort

        var r = Assert.Single(abgelehnt);
        Assert.Equal("cih", r.Before);
        Assert.Contains(learned, x => x.Source == CorrectionSource.Rejected && x.Before == "cih");
    }

    [Fact]
    public void NachgetippteZeichenZaehlenMit()
    {
        var c = Build(out _, out _, "cih=ich");
        var abgelehnt = new List<CorrectionRecord>();
        c.CorrectionRejectedByUser += abgelehnt.Add;

        c.HandleWord(Word("cih"));   // "ich "
        c.OnCharTyped();             // "ich x"
        c.OnBackspace();
        c.OnBackspace();             // wieder "ich" - Leerzeichen geloescht
        Assert.Empty(abgelehnt);
        c.OnBackspace();

        Assert.Single(abgelehnt);
    }

    [Fact]
    public void NachDemNaechstenWortGiltDieRuecktasteNichtMehrAlsAblehnung()
    {
        var c = Build(out _, out _, "cih=ich");
        var abgelehnt = new List<CorrectionRecord>();
        c.CorrectionRejectedByUser += abgelehnt.Add;

        c.HandleWord(Word("cih"));
        c.HandleWord(Word("habe"));  // weitergetippt - Korrektur angenommen
        for (var i = 0; i < 10; i++) c.OnBackspace();

        Assert.Empty(abgelehnt);
    }

    [Fact]
    public void KontextbruchBeendetDieBeobachtung()
    {
        var c = Build(out _, out _, "cih=ich");
        var abgelehnt = new List<CorrectionRecord>();
        c.CorrectionRejectedByUser += abgelehnt.Add;

        c.HandleWord(Word("cih"));
        c.ForgetLastCorrection();    // z. B. Mausklick
        c.OnBackspace();
        c.OnBackspace();

        Assert.Empty(abgelehnt);
    }
}
