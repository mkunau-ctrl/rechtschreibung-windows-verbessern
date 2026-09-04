using Xunit;

namespace RechtschreibTrainer.Core.Tests;

public class ReplacementTableTests
{
    [Theory]
    [InlineData("fuer", "für")]
    [InlineData("moechte", "möchte")]
    [InlineData("waehrend", "während")]
    [InlineData("gruen", "grün")]
    [InlineData("schoen", "schön")]
    public void ResolveAllLoestDieHaeufigenFaelleAuf(string typed, string expected)
    {
        Assert.Equal(expected, ReplacementTable.ResolveAll(typed));
    }

    [Fact]
    public void CandidatesEnthaeltDasWortSelbstNicht()
    {
        Assert.DoesNotContain("fuer", ReplacementTable.Candidates("fuer"));
    }

    [Fact]
    public void OhneErsatzformKeineKandidaten()
    {
        Assert.Empty(ReplacementTable.Candidates("haus"));
    }

    [Fact]
    public void MehrereFundstellenErgebenAuchEinzelneKandidaten()
    {
        // "gruesse" hat zwei "ue"-artige Stellen? Nein - ein Vorkommen von "ue"
        // und ein "ss". Beide sollen je einzeln UND zusammen als Kandidat kommen.
        var candidates = ReplacementTable.Candidates("gruesse").ToList();

        Assert.Contains("grüsse", candidates);   // nur ue->ü
        Assert.Contains("gruesse".Replace("ss", "ß"), candidates); // nur ss->ß  ("gruße")
        Assert.Contains("grüße", candidates);    // beides zusammen
    }
}
