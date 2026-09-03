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
    public void BackspacePastTheWordStartInvalidatesTheBuffer()
    {
        var (w, seen) = Build();

        Type(w, "hallo welt "); // buffer now valid
        w.OnBackspace();        // deletes the space -> editing earlier text
        Type(w, "en ");

        // "welten" must NOT be emitted: the buffer was invalidated
        Assert.DoesNotContain(seen, x => x.Word == "welten");
    }

    [Fact]
    public void DoesNotEmitTheFirstWordAfterInvalidate()
    {
        var (w, seen) = Build();

        w.Invalidate();
        Type(w, "mitten ");   // typed where the cursor jumped - not trustworthy
        Type(w, "danach ");   // fully typed in place - trustworthy

        Assert.Single(seen);
        Assert.Equal("danach", seen[0].Word);
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
