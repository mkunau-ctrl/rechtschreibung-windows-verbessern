using Xunit;

namespace RechtschreibTrainer.Core.Tests;

public class DictionaryDistillerTests
{
    private static readonly IReadOnlySet<string> NoExclusions = new HashSet<string>();

    private static CorrectionRecord Record(string before, string after, CorrectionSource source = CorrectionSource.Spelling)
        => new(DateTime.Now, before, after, source);

    [Fact]
    public void SchlaegtEinPaarErstAbDemSchwellwertVor()
    {
        var records = new[] { Record("moechte", "möchte"), Record("moechte", "möchte") };

        var result = DictionaryDistiller.Distill(records, NoExclusions, threshold: 3);

        Assert.Empty(result);
    }

    [Fact]
    public void SchlaegtEinPaarAbGenauDemSchwellwertVor()
    {
        var records = new[] { Record("moechte", "möchte"), Record("moechte", "möchte"), Record("moechte", "möchte") };

        var result = DictionaryDistiller.Distill(records, NoExclusions, threshold: 3);

        var only = Assert.Single(result);
        Assert.Equal("moechte", only.Before);
        Assert.Equal("möchte", only.After);
        Assert.Equal(3, only.Count);
    }

    [Fact]
    public void ZaehltNurGleicheVorherNachherPaare()
    {
        // Zweimal "moechte" -> "möchte", einmal (versehentlich) "moechte" -> "mochte":
        // zaehlt getrennt, keins erreicht allein den Schwellwert.
        var records = new[]
        {
            Record("moechte", "möchte"), Record("moechte", "möchte"), Record("moechte", "mochte"),
        };

        var result = DictionaryDistiller.Distill(records, NoExclusions, threshold: 3);

        Assert.Empty(result);
    }

    [Fact]
    public void UeberspringtBereitsAusgeschlosseneWoerter()
    {
        // Steht schon im Woerterbuch ODER auf der nie-korrigieren-Liste -
        // nicht erneut vorschlagen.
        var records = Enumerable.Repeat(Record("moechte", "möchte"), 5).ToArray();
        var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "moechte" };

        var result = DictionaryDistiller.Distill(records, exclude, threshold: 3);

        Assert.Empty(result);
    }

    [Fact]
    public void SortiertNachHaeufigkeitAbsteigend()
    {
        var records = Enumerable.Repeat(Record("nocg", "noch"), 3)
            .Concat(Enumerable.Repeat(Record("cih", "ich"), 7))
            .ToArray();

        var result = DictionaryDistiller.Distill(records, NoExclusions, threshold: 3);

        Assert.Equal(["cih", "nocg"], result.Select(r => r.Before));
    }

    [Fact]
    public void KeineVorschlaegeOhneAufzeichnungen()
    {
        Assert.Empty(DictionaryDistiller.Distill([], NoExclusions, threshold: 3));
    }

    [Fact]
    public void SchlaegtSatzanfangGrossschreibungNiemalsAlsFestenEintragVor()
    {
        // "ich" -> "Ich" ist eine reine Positionsregel (Satzanfang), keine
        // Rechtschreibkorrektur. Als "ich=Ich" fest im Woerterbuch wuerde
        // "ich" ploetzlich UEBERALL im Satz gross geschrieben - Belegfund
        // aus dem echten Log des Nutzers (siehe PROJEKT-LOG.md).
        var records = Enumerable.Repeat(Record("ich", "Ich", CorrectionSource.Capitalization), 5).ToArray();

        Assert.Empty(DictionaryDistiller.Distill(records, NoExclusions, threshold: 3));
    }

    [Fact]
    public void SchlaegtEchteRechtschreibkorrekturenAusDerFuzzySucheWeiterhinVor()
    {
        var records = Enumerable.Repeat(Record("porjekt", "Projekt", CorrectionSource.Spelling), 3).ToArray();

        var result = DictionaryDistiller.Distill(records, NoExclusions, threshold: 3);

        var only = Assert.Single(result);
        Assert.Equal("porjekt", only.Before);
        Assert.Equal("Projekt", only.After);
    }

    [Theory]
    [InlineData("würde", "Würde")]
    [InlineData("ordner", "Ordner")]
    [InlineData("github", "GitHub")]
    public void LerntNieEineReineGrossKleinKorrektur(string before, string after)
    {
        // Runde 3: Ob "würde"/"Würde" gross gehoert, entscheidet der Satz.
        // Fest gelernt hatte "würde=Würde" jedes "ich würde" zerstoert.
        var records = Enumerable.Repeat(Record(before, after, CorrectionSource.Spelling), 5).ToArray();

        Assert.Empty(DictionaryDistiller.Distill(records, NoExclusions, threshold: 3));
    }

    [Fact]
    public void LerntNieEinPaarDasDerNutzerAbgelehntHat()
    {
        var records = Enumerable.Repeat(Record("anderern", "Wanderern"), 3)
            .Append(Record("anderern", "Wanderern", CorrectionSource.Rejected))
            .ToArray();

        Assert.Empty(DictionaryDistiller.Distill(records, NoExclusions, threshold: 3));
    }

    [Fact]
    public void ZweimalAbgelehntWirdNieMehrKorrigiert()
    {
        var records = new[]
        {
            Record("kunde", "Kunde"),
            Record("kunde", "Kunde", CorrectionSource.Rejected),
            Record("abc", "Abc", CorrectionSource.Rejected),
            Record("kunde", "Kunde", CorrectionSource.Rejected),
        };

        Assert.Equal(["kunde"], DictionaryDistiller.RejectedOften(records, threshold: 2));
    }
}
