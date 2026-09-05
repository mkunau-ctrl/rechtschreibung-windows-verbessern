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
    private readonly HashSet<string> _words;              // case-insensitiv: "kennt das Programm das Wort?"
    private readonly HashSet<string> _nouns;              // großgeschriebene Substantivformen
    private readonly HashSet<string> _keepLowercase;      // Wörter, die klein bleiben (Verben, Homographen …)
    private readonly Dictionary<string, string> _properNouns; // klein -> exakte Schreibweise (GitHub, iPhone …)
    private readonly Dictionary<string, long> _frequency;

    private WordList(
        HashSet<string> words,
        HashSet<string> nouns,
        HashSet<string> keepLowercase,
        Dictionary<string, string> properNouns,
        Dictionary<string, long> frequency)
    {
        _words = words;
        _nouns = nouns;
        _keepLowercase = keepLowercase;
        _properNouns = properNouns;
        _frequency = frequency;
    }

    public int Count => _words.Count;

    public static WordList FromLines(
        IEnumerable<string> words,
        IEnumerable<string> frequencyLines,
        IEnumerable<string>? nouns = null,
        IEnumerable<string>? keepLowercase = null,
        IEnumerable<string>? properNouns = null)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var exact = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in words)
        {
            var w = raw.Trim();
            if (w.Length == 0 || w.StartsWith('#'))
                continue;
            set.Add(w);
            exact.Add(w);
        }

        var nounSet = ReadSet(nouns, StringComparer.Ordinal);
        var keepLower = ReadSet(keepLowercase, StringComparer.OrdinalIgnoreCase);

        var proper = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in properNouns ?? [])
        {
            var w = raw.Trim();
            if (w.Length > 0 && !w.StartsWith('#'))
            {
                proper.TryAdd(w.ToLowerInvariant(), w);
                set.Add(w);
            }
        }

        // Ohne eigene Substantivliste dient die Wortliste selbst als Näherung:
        // ein Wort, das nur großgeschrieben vorkommt, gilt als Substantiv.
        if (nounSet.Count == 0)
        {
            foreach (var w in exact)
                if (char.IsUpper(w[0]) && !exact.Contains(char.ToLowerInvariant(w[0]) + w[1..]))
                    nounSet.Add(w);
        }

        var freq = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in frequencyLines)
        {
            var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && long.TryParse(parts[1], out var n))
                freq[parts[0]] = n;
        }

        return new WordList(set, nounSet, keepLower, proper, freq);
    }

    /// <summary>Exakte Schreibweise, wenn das Wort ein Name/Eigenname ist (GitHub, iPhone, Montag); sonst null.</summary>
    public string? ProperNoun(string word)
        => _properNouns.TryGetValue(word, out var exact) ? exact : null;

    private static HashSet<string> ReadSet(IEnumerable<string>? lines, StringComparer comparer)
    {
        var s = new HashSet<string>(comparer);
        if (lines is null) return s;
        foreach (var raw in lines)
        {
            var w = raw.Trim();
            if (w.Length > 0 && !w.StartsWith('#'))
                s.Add(w);
        }
        return s;
    }

    public bool Knows(string word) => word.Trim().Length > 0 && _words.Contains(word);

    public long Frequency(string word) => _frequency.TryGetValue(word, out var n) ? n : 0;

    /// <summary>
    /// Ist das (klein getippte) Wort ein Substantiv/Eigenname — also nur in
    /// großgeschriebener Form bekannt und klein kein gültiges Wort? Dann gehört
    /// es groß. Kontextfälle wie „das Essen" ↔ „wir essen" fallen hier raus,
    /// weil „essen" klein gültig ist.
    /// </summary>
    public bool IsCapitalisedOnly(string lowerWord)
    {
        if (lowerWord.Length == 0 || !char.IsLower(lowerWord[0]))
            return false;

        if (_keepLowercase.Contains(lowerWord))
            return false;

        var capitalised = char.ToUpperInvariant(lowerWord[0]) + lowerWord[1..];
        return _nouns.Contains(capitalised);
    }
}
