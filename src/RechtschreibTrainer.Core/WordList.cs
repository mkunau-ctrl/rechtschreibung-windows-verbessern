namespace RechtschreibTrainer.Core;

/// <summary>
/// Die bekannten deutschen Wortformen plus Häufigkeiten. Zwei Aufgaben:
/// entscheiden, ob ein getipptes Wort überhaupt falsch ist, und Kandidaten
/// nach Häufigkeit sortieren.
///
/// Vergleiche laufen kleingeschrieben, weil der Nutzer beim schnellen Tippen
/// alles klein schreibt — "haus" muss als bekanntes Wort durchgehen.
/// </summary>
public sealed class WordList
{
    private readonly HashSet<string> _words;
    private readonly Dictionary<string, long> _frequency;

    private WordList(HashSet<string> words, Dictionary<string, long> frequency)
    {
        _words = words;
        _frequency = frequency;
    }

    public int Count => _words.Count;

    public static WordList FromLines(IEnumerable<string> words, IEnumerable<string> frequencyLines)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in words)
        {
            var w = raw.Trim();
            if (w.Length > 0)
                set.Add(w);
        }

        var freq = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in frequencyLines)
        {
            var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && long.TryParse(parts[1], out var n))
                freq[parts[0]] = n;
        }

        return new WordList(set, freq);
    }

    public bool Knows(string word) => word.Trim().Length > 0 && _words.Contains(word);

    public long Frequency(string word) => _frequency.TryGetValue(word, out var n) ? n : 0;
}
