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
}
