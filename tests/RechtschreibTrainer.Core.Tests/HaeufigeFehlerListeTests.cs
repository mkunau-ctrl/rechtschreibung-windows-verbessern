using Xunit;
using Xunit.Abstractions;

namespace RechtschreibTrainer.Core.Tests;

/// <summary>
/// Prüft die automatisch erzeugte Liste
/// <c>src/RechtschreibTrainer/haeufige-fehler.txt</c> (aus der
/// Wikipedia-Tippfehlerliste, siehe <c>scripts/fetch-fehlerlisten.ps1</c>).
///
/// Die Liste ist nur so viel wert wie ihr Filter: ein Schlüssel, der in
/// Wahrheit ein korrektes Wort ist, würde beim Tippen zu einer echten
/// Verschlimmbesserung führen. Diese Tests sind die Absicherung dafür.
/// </summary>
public class HaeufigeFehlerListeTests
{
    private readonly ITestOutputHelper _out;
    public HaeufigeFehlerListeTests(ITestOutputHelper output) => _out = output;

    private static readonly string ListPath =
        Path.Combine(RepoFiles.BundledDir, "haeufige-fehler.txt");

    private static readonly string HoldoutPath = Path.Combine(
        RepoFiles.Root, "tests", "RechtschreibTrainer.Core.Tests", "gaengige-tippfehler-holdout.tsv");

    private sealed record Pair(string Wrong, string Right);

    private static List<Pair> Load()
    {
        var pairs = new List<Pair>();
        foreach (var raw in File.ReadLines(ListPath))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var sep = line.IndexOf('=');
            Assert.True(sep > 0, $"Zeile ohne '=': {raw}");
            pairs.Add(new Pair(line[..sep].Trim(), line[(sep + 1)..].Trim()));
        }
        return pairs;
    }

    private static readonly Lazy<List<Pair>> Pairs = new(Load);
    private static readonly Lazy<OfflineCorrector> Corrector = new(RepoFiles.LoadCorrector);

    /// <summary>
    /// Alle bekannten Wortformen, klein normalisiert — dieselbe Vereinigung aus
    /// woerter.txt + substantive.txt + namen.txt, die auch
    /// scripts/fetch-fehlerlisten.ps1 beim Filtern benutzt.
    /// </summary>
    private static readonly Lazy<HashSet<string>> Known = new(() =>
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "woerter.txt", "substantive.txt", "namen.txt" })
        {
            var path = Path.Combine(RepoFiles.DataDir, name);
            if (!File.Exists(path)) continue;
            foreach (var raw in File.ReadLines(path))
            {
                var w = raw.Trim();
                if (w.Length == 0 || w.StartsWith('#')) continue;
                var sp = w.IndexOf(' ');
                if (sp > 0) w = w[..sp];
                set.Add(w);
            }
        }
        return set;
    });

    private static bool KnowsAnyCase(string word) => Known.Value.Contains(word);

    [Fact]
    public void DateiIstDaUndHatGenugEintraege()
    {
        Assert.True(File.Exists(ListPath), $"fehlt: {ListPath} — scripts/fetch-fehlerlisten.ps1 ausführen");
        Assert.True(Pairs.Value.Count >= 500,
            $"nur {Pairs.Value.Count} Einträge — zu wenig, Filter/Quelle prüfen");
        _out.WriteLine($"{Pairs.Value.Count} Tippfehler-Paare in der Auslieferungsliste");
    }

    [Fact]
    public void KeinSchluesselIstInWahrheitEinKorrektesWort()
    {
        Assert.True(RepoFiles.HasWordLists, "data/woerter.txt fehlt — siehe data/HERKUNFT.md");

        var treffer = Pairs.Value
            .Where(p => KnowsAnyCase(p.Wrong))
            .Select(p => p.Wrong)
            .ToList();

        Assert.True(treffer.Count == 0,
            "Diese Schlüssel sind echte Wörter und dürfen nicht in der Liste stehen: "
            + string.Join(", ", treffer));
    }

    [Fact]
    public void JederZielwertIstEinBelegtesWort()
    {
        Assert.True(RepoFiles.HasWordLists, "data/woerter.txt fehlt — siehe data/HERKUNFT.md");

        var schlecht = new List<string>();
        foreach (var p in Pairs.Value)
        {
            var tokens = p.Right.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Any(t => !KnowsAnyCase(t)))
                schlecht.Add($"{p.Wrong}={p.Right}");
        }

        Assert.True(schlecht.Count == 0,
            "Zielwerte ohne Wörterbuch-Beleg: " + string.Join(", ", schlecht));
    }

    [Fact]
    public void KeineDoppeltenOderLeerenSchluessel()
    {
        var doppelt = Pairs.Value
            .GroupBy(p => p.Wrong, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(doppelt.Count == 0, "Doppelte Schlüssel: " + string.Join(", ", doppelt));
        Assert.DoesNotContain(Pairs.Value, p => p.Wrong.Length == 0 || p.Right.Length == 0);
    }

    [Fact]
    public void KeinReinerGrossKleinUnterschied()
    {
        var casing = Pairs.Value
            .Where(p => string.Equals(p.Wrong, p.Right, StringComparison.OrdinalIgnoreCase))
            .Select(p => $"{p.Wrong}={p.Right}")
            .ToList();

        Assert.True(casing.Count == 0,
            "Reine Groß-/Kleinschreib-Unterschiede brauchen Satzkontext, nicht hierher: "
            + string.Join(", ", casing));
    }

    [Fact]
    public void KeineUeberschneidungMitDenHandgepflegtenListen()
    {
        var hand = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "klassische-fehler.txt", "standard-vertipper.txt", "denglisch-verben.txt" })
        {
            var path = Path.Combine(RepoFiles.BundledDir, name);
            if (!File.Exists(path)) continue;
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var sep = line.IndexOf('=');
                hand.Add(sep > 0 ? line[..sep].Trim() : line);
            }
        }

        var kollision = Pairs.Value.Where(p => hand.Contains(p.Wrong)).Select(p => p.Wrong).ToList();
        Assert.True(kollision.Count == 0,
            "Schon von Hand gepflegt, gehört nicht doppelt in die Auto-Liste: "
            + string.Join(", ", kollision));
    }

    [Fact]
    public void JederEintragWirdImBetriebAuchWirklichSoKorrigiert()
    {
        Assert.True(RepoFiles.HasWordLists, "data/woerter.txt fehlt — siehe data/HERKUNFT.md");

        var ctx = new WordContext(IsSentenceStart: false);
        var fehler = new List<string>();

        foreach (var p in Pairs.Value)
        {
            var got = Corrector.Value.Correct(p.Wrong, ctx);
            if (!string.Equals(got.Corrected, p.Right, StringComparison.Ordinal))
                fehler.Add($"{p.Wrong} -> {got.Corrected} (erwartet: {p.Right})");
        }

        Assert.True(fehler.Count == 0,
            "Einträge, die im Betrieb nicht wie erwartet greifen:\n" + string.Join("\n", fehler));
    }

    [Fact]
    public void HoldoutFaelleSindBewusstNichtInDerAuslieferungsliste()
    {
        Assert.True(File.Exists(HoldoutPath), $"fehlt: {HoldoutPath}");

        var holdout = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadLines(HoldoutPath))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            holdout.Add(line.Split('\t')[0].Trim());
        }

        Assert.True(holdout.Count >= 50, $"Holdout-Datei hat nur {holdout.Count} Fälle");

        var drin = Pairs.Value.Where(p => holdout.Contains(p.Wrong)).Select(p => p.Wrong).ToList();
        Assert.True(drin.Count == 0,
            "Holdout-Fälle dürfen nicht ausgeliefert werden (sonst misst der Trefferquoten-Test sich selbst): "
            + string.Join(", ", drin));
    }
}
