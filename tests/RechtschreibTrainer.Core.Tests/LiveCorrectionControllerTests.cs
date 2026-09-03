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
    public void UndoDoesNothingWhenNothingWasReplaced()
    {
        var c = Build(out var reps, out _, "cih=ich");

        c.Undo();

        Assert.Empty(reps);
    }

    [Fact]
    public void OnANewlineBoundaryKeepsTheNewlineAndOnlyRewritesTheWord()
    {
        var c = Build(out var reps, out _, "cih=ich");

        c.HandleWord(Word("cih", boundary: '\n'));

        var cmd = Assert.Single(reps);
        Assert.Equal(3, cmd.DeleteCount);   // only "cih", newline stays
        Assert.Equal("ich", cmd.Insert);
    }
}
