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
        var records = Enumerable.Repeat(Record("a", "A"), 3)
            .Concat(Enumerable.Repeat(Record("b", "B"), 7))
            .ToArray();

        var result = DictionaryDistiller.Distill(records, NoExclusions, threshold: 3);

        Assert.Equal(["b", "a"], result.Select(r => r.Before));
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
        // Im Unterschied zu Capitalization: "ordner" -> "Ordner" ist ein
        // eindeutiges Substantiv (immer gross, unabhaengig von der Position)
        // und darf fest vorgeschlagen werden.
        var records = Enumerable.Repeat(Record("ordner", "Ordner", CorrectionSource.Spelling), 3).ToArray();

        var result = DictionaryDistiller.Distill(records, NoExclusions, threshold: 3);

        var only = Assert.Single(result);
        Assert.Equal("ordner", only.Before);
        Assert.Equal("Ordner", only.After);
    }
}
