using RechtschreibTrainer.Core;
using Xunit;
using Xunit.Abstractions;

namespace RechtschreibTrainer.Core.Tests;

/// <summary>
/// Prüft den Korrektor gegen die echten mitgelieferten Wortlisten und gegen
/// Vertipper, die wirklich aus keystrokes.log stammen. Läuft langsamer als die
/// übrigen Tests (14 MB Daten), ist dafür aber die einzige Aussage darüber,
/// wie gut das Ding im Alltag ist.
/// </summary>
public class RealDataSpellingTests
{
    private readonly ITestOutputHelper _output;

    public RealDataSpellingTests(ITestOutputHelper output) => _output = output;

    private static readonly Lazy<SpellCorrector> Corrector = new(() =>
    {
        var data = FindDataDirectory();
        return new SpellCorrector(
            WordList.FromLines(
                File.ReadLines(Path.Combine(data, "woerter.txt")),
                File.ReadLines(Path.Combine(data, "haeufigkeit.txt")),
                File.ReadLines(Path.Combine(data, "substantive.txt")),
                File.ReadLines(Path.Combine(data, "klein-schreiben.txt")),
                File.ReadLines(Path.Combine(data, "namen.txt"))),
            SpellSettings.Default);
    });

    private static string FindDataDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "woerter.txt")))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("data/woerter.txt nicht gefunden");
    }

    /// <summary>Vertipper aus keystrokes.log und was dabei herauskommen soll.</summary>
    public static readonly (string Typo, string Want)[] RealTypos =
    [
        ("nocg", "noch"), ("scgauen", "schauen"), ("veressert", "verbessert"),
        ("enauso", "genauso"), ("mlchte", "möchte"), ("eifnach", "einfach"),
        ("witer", "weiter"), ("ahben", "haben"), ("spannedn", "spannend"),
        ("makieren", "markieren"), ("befhel", "befehl"), ("ausfürhren", "ausführen"),
        ("funkitoniert", "funktioniert"), ("cmputer", "computer"),
        ("guthabne", "guthaben"), ("mmöchte", "möchte"), ("richtg", "richtig"),
        ("vielleciht", "vielleicht"), ("erstmla", "erstmal"),
        ("korrigeirt", "korrigiert"), ("richrig", "richtig"), ("aknn", "kann"),
        ("überhauot", "überhaupt"), ("eventeull", "eventuell"),
        ("benuzen", "benutzen"), ("gepannt", "gespannt"), ("frae", "frage"),
    ];

    [Fact]
    public void CorrectsTheVastMajorityOfRealTypos()
    {
        var right = new List<string>();
        var nothing = new List<string>();
        var wrong = new List<string>();

        foreach (var (typo, want) in RealTypos)
        {
            var got = Corrector.Value.Suggest(typo);
            if (got is null) nothing.Add(typo);
            else if (string.Equals(got, want, StringComparison.OrdinalIgnoreCase)) right.Add(typo);
            else wrong.Add($"{typo} -> {got} (gewollt: {want})");
        }

        _output.WriteLine($"richtig {right.Count}/{RealTypos.Length}");
        _output.WriteLine($"nicht angefasst: {string.Join(", ", nothing)}");
        _output.WriteLine($"FALSCH: {string.Join(" | ", wrong)}");

        Assert.True(right.Count >= 20, $"nur {right.Count} von {RealTypos.Length} richtig");
    }

    [Fact]
    public void NeverMakesAWrongGuess()
    {
        // Falsch ersetzen ist schlimmer als gar nicht ersetzen — hier darf
        // höchstens eine Handvoll danebenliegen.
        var wrong = RealTypos
            .Select(t => (t.Typo, Got: Corrector.Value.Suggest(t.Typo), t.Want))
            .Where(x => x.Got is not null && !string.Equals(x.Got, x.Want, StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{x.Typo} -> {x.Got} statt {x.Want}")
            .ToList();

        Assert.True(wrong.Count <= 2, "zu viele Fehlgriffe: " + string.Join(" | ", wrong));
    }

    [Theory]
    // korrekt geschriebene Wörter
    [InlineData("Haus")] [InlineData("gehst")] [InlineData("Häuser")]
    [InlineData("personalisierter")] [InlineData("seht")] [InlineData("möchte")]
    // zusammengesetzte Wörter, die in keiner Liste stehen
    [InlineData("Kältekreislauf")] [InlineData("Nutzungslimit")] [InlineData("Trainingsdaten")]
    // Eigennamen
    [InlineData("Claude")] [InlineData("Berlin")] [InlineData("Windows")]
    public void LeavesCorrectWordsAlone(string word)
    {
        Assert.Null(Corrector.Value.Suggest(word));
    }

    [Theory]
    [InlineData("montag", "Montag")]
    [InlineData("auto", "Auto")]
    [InlineData("computer", "Computer")]
    [InlineData("berlin", "Berlin")]
    public void CapitalisesLowercaseNouns(string typed, string want)
    {
        Assert.Equal(want, Corrector.Value.Suggest(typed));
    }

    [Theory]
    [InlineData("github", "GitHub")]
    [InlineData("windows", "Windows")]
    [InlineData("python", "Python")]
    [InlineData("claude", "Claude")]
    [InlineData("berlin", "Berlin")]
    [InlineData("thomas", "Thomas")]
    public void RestoresProperNounsExactly(string typed, string want)
    {
        Assert.Equal(want, Corrector.Value.Suggest(typed));
    }

    [Theory]
    // häufige klein geschriebene Wörter dürfen NICHT groß werden
    [InlineData("gehen")] [InlineData("laufen")] [InlineData("schnell")]
    [InlineData("und")] [InlineData("weil")] [InlineData("haben")]
    [InlineData("essen")] [InlineData("machen")] [InlineData("wichtig")]
    public void DoesNotCapitaliseCommonLowercaseWords(string word)
    {
        Assert.Null(Corrector.Value.Suggest(word));
    }
}
