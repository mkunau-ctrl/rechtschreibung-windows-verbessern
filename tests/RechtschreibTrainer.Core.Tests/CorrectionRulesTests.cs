using RechtschreibTrainer.Core;
using Xunit;

namespace RechtschreibTrainer.Core.Tests;

public class CorrectionRulesTests
{
    [Fact]
    public void ReplacesScgWithSch()
    {
        Assert.Equal("schauen", CorrectionRules.Apply("scgauen"));
    }

    [Fact]
    public void ReplacesTrailingCgWithCh()
    {
        Assert.Equal("noch", CorrectionRules.Apply("nocg"));
    }

    [Fact]
    public void DoesNotReplaceCgInWordMiddle()
    {
        // kein deutsches Wort mit "cg" in der Mitte, aber die Regel darf
        // nur am Wortende greifen, nicht mittendrin.
        Assert.Equal("abcgdef", CorrectionRules.Apply("abcgdef"));
    }

    [Fact]
    public void ReplacesLeadingCihWithIch()
    {
        Assert.Equal("ich", CorrectionRules.Apply("cih"));
    }

    [Fact]
    public void LeavesCorrectWordUnchanged()
    {
        Assert.Equal("Rechtschreibung", CorrectionRules.Apply("Rechtschreibung"));
    }
}
