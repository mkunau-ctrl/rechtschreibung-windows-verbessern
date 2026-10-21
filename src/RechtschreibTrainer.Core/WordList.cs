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
    private readonly HashSet<string> _ambiguousNouns;     // selten gemeintes Substantiv, ohne Artikel eher nicht
    private readonly Dictionary<string, string> _properNouns; // klein -> exakte Schreibweise (GitHub, iPhone …)
    private readonly Dictionary<string, long> _frequency;
    private readonly Dictionary<string, (int Upper, int Lower)> _casing; // Korpus: wie oft mitten im Satz groß/klein
    private readonly HashSet<string> _lowercaseValid;     // klein ein gültiges Wort, groß ein Substantiv ("testen")

    /// <summary>Ab so vielen Korpus-Belegen zählt das Verhältnis groß/klein.</summary>
    private const int MinCasingEvidence = 5;

    /// <summary>Ab diesem Anteil „groß mitten im Satz" wird ohne weiteren Beleg großgeschrieben.</summary>
    private const double CapitaliseAlways = 0.9;

    /// <summary>Unter diesem Anteil wird nie großgeschrieben, dazwischen nur mit Substantiv-Beleg.</summary>
    private const double CapitaliseNever = 0.2;

    private WordList(
        HashSet<string> words,
        HashSet<string> nouns,
        HashSet<string> keepLowercase,
        HashSet<string> ambiguousNouns,
        Dictionary<string, string> properNouns,
        Dictionary<string, long> frequency,
        Dictionary<string, (int Upper, int Lower)> casing,
        HashSet<string> lowercaseValid)
    {
        _words = words;
        _nouns = nouns;
        _keepLowercase = keepLowercase;
        _ambiguousNouns = ambiguousNouns;
        _properNouns = properNouns;
        _frequency = frequency;
        _casing = casing;
        _lowercaseValid = lowercaseValid;
    }

    public int Count => _words.Count;

    public static WordList FromLines(
        IEnumerable<string> words,
        IEnumerable<string> frequencyLines,
        IEnumerable<string>? nouns = null,
        IEnumerable<string>? keepLowercase = null,
        IEnumerable<string>? properNouns = null,
        IEnumerable<string>? ambiguousNouns = null,
        IEnumerable<string>? casing = null,
        IEnumerable<string>? lowercaseValid = null)
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
        var ambiguous = ReadSet(ambiguousNouns, StringComparer.OrdinalIgnoreCase);

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

        var casingMap = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in casing ?? [])
        {
            var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 3 && int.TryParse(parts[1], out var up) && int.TryParse(parts[2], out var low))
                casingMap[parts[0]] = (up, low);
        }

        var lowerValid = ReadSet(lowercaseValid, StringComparer.OrdinalIgnoreCase);

        return new WordList(set, nounSet, keepLower, ambiguous, proper, freq, casingMap, lowerValid);
    }

    /// <summary>Exakte Schreibweise, wenn das Wort ein Name/Eigenname ist (GitHub, iPhone, Montag); sonst null.</summary>
    public string? ProperNoun(string word)
        => _properNouns.TryGetValue(word, out var exact) ? exact : null;

    /// <summary>
    /// Wie <see cref="ProperNoun"/>, aber nur, wenn das getippte Wort im Satz
    /// auch wirklich als Name gemeint ist. Vornamen wie „Anders", „Lasse",
    /// „Will" sind klein gewöhnliche Wörter („anders", „ich lasse"): Ist das
    /// Wort klein gültig und laut Korpus nicht fast immer groß, ist es kein Name.
    /// </summary>
    public string? ProperNounReading(string word)
    {
        if (ProperNoun(word) is not { } exact)
            return null;

        var lower = word.ToLowerInvariant();
        if (!_lowercaseValid.Contains(lower))
            return exact;

        return CasingShare(lower) is { } share && share >= CapitaliseAlways ? exact : null;
    }

    /// <summary>Ist das Wort klein ein gewöhnliches Wort (laut <c>klein-gueltig.txt</c>)?</summary>
    public bool IsLowercaseValid(string lowerWord) => _lowercaseValid.Contains(lowerWord);

    /// <summary>Anteil „groß mitten im Satz" im Korpus, oder null bei zu wenigen Belegen.</summary>
    private double? CasingShare(string lowerWord) =>
        _casing.TryGetValue(lowerWord, out var c) && c.Upper + c.Lower >= MinCasingEvidence
            ? (double)c.Upper / (c.Upper + c.Lower)
            : null;

    /// <summary>
    /// Sieht das Wort wie ein Verb im Infinitiv aus? „testen" (es gibt „teste",
    /// „testet"), „ändern" („ändere", „ändert") — nicht aber „ersten" oder
    /// „besten", zu denen es keine Verbformen gibt.
    /// </summary>
    private bool LooksLikeInfinitive(string lowerWord)
    {
        string? stem = lowerWord.EndsWith("ern") || lowerWord.EndsWith("eln")
            ? lowerWord[..^1]
            : lowerWord.EndsWith("en") ? lowerWord[..^2] : null;

        if (stem is null || stem.Length < 2)
            return false;

        return Knows(stem + "e") && (Knows(stem + "t") || Knows(stem + "et"));
    }

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
    /// Gehört das klein getippte Wort mitten im Satz groß?
    ///
    /// Bis Runde 3 hieß das nur: „Gibt es die großgeschriebene Form als
    /// Substantiv?" — damit wurden auch „würde", „leider", „testen", „senden"
    /// großgezogen (in den echten Daten ~15 % aller Großschreibungen mitten im
    /// Satz falsch). Jetzt in dieser Reihenfolge:
    /// 1. Gibt es kein Substantiv dazu → nein.
    /// 2. Steht direkt davor eine Kontraktion (<c>zum beim vom …</c>) und
    ///    sieht das Wort wie ein Infinitiv aus → ja („zum Testen").
    /// 3. Hat der Korpus (<c>schreibung.txt</c>) genug Belege: ≥ 90 % groß →
    ///    ja; 20–90 % → nur mit Beleg; darunter → nein. (20 % statt 30 %:
    ///    „essen" liegt bei 29,8 %, „das Essen" soll aber groß werden.)
    ///    Ohne Korpusbeleg: klein-bleiben-Liste → nein; klein auch gültig
    ///    (<c>klein-gueltig.txt</c>) → nur mit Beleg; sonst ja.
    /// 4. klein-bleiben- und Mehrdeutig-Liste verschärfen nur: steht das Wort
    ///    dort, braucht es in jedem Fall einen Beleg. Gegen den Korpus
    ///    erlauben sie nie eine Großschreibung („alle drei" bleibt klein).
    ///
    /// <paramref name="precededByDeterminer"/> ist der Substantiv-Beleg aus
    /// dem Satzkontext (Artikel, Kontraktion, Zahl, Artikel + Adjektiv — siehe
    /// <see cref="WordWatcher"/>), <paramref name="precededByContraction"/>
    /// sagt, ob es genau eine Kontraktion war.
    /// </summary>
    public bool IsCapitalisedOnly(string lowerWord, bool precededByDeterminer = false, bool precededByContraction = false)
    {
        if (lowerWord.Length == 0 || !char.IsLower(lowerWord[0]))
            return false;

        var capitalised = char.ToUpperInvariant(lowerWord[0]) + lowerWord[1..];
        if (!_nouns.Contains(capitalised))
            return false;

        if (precededByContraction && LooksLikeInfinitive(lowerWord))
            return true;

        var curated = _keepLowercase.Contains(lowerWord) || _ambiguousNouns.Contains(lowerWord);

        bool needsEvidence;
        if (CasingShare(lowerWord) is { } share)
        {
            if (share < CapitaliseNever) return false;
            needsEvidence = share < CapitaliseAlways;
        }
        else
        {
            if (_keepLowercase.Contains(lowerWord)) return false;
            needsEvidence = _lowercaseValid.Contains(lowerWord);
        }

        return needsEvidence || curated ? precededByDeterminer : true;
    }
}
