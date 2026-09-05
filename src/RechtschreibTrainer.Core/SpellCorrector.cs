namespace RechtschreibTrainer.Core;

/// <summary>Stellschrauben, wie mutig geraten werden darf.</summary>
public readonly record struct SpellSettings(int MinLength, double Dominance)
{
    /// <summary>
    /// Bewusst vorsichtig: Wörter unter 4 Zeichen liegen neben zu vielen
    /// anderen, und der beste Kandidat muss den zweitbesten klar schlagen —
    /// sonst lieber nichts tun als falsch ersetzen.
    /// </summary>
    public static SpellSettings Default => new(MinLength: 4, Dominance: 1.6);
}

/// <summary>
/// Schlägt zu einem unbekannten Wort die wahrscheinlichste bekannte Schreibweise
/// vor — oder <c>null</c>, wenn die Sache nicht eindeutig genug ist.
///
/// Betrachtet nur Kandidaten mit Damerau-Abstand 1 (ein vergessener,
/// überzähliger, vertauschter oder falscher Buchstabe). Die Art des Fehlers
/// geht in die Bewertung ein: vergessene und vertauschte Buchstaben sind beim
/// schnellen Tippen viel häufiger als ein danebengegriffener.
/// </summary>
public sealed class SpellCorrector
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzäöüß";

    /// <summary>Wie plausibel die jeweilige Vertipper-Art ist.</summary>
    private const double WeightTransposition = 1.0; // Buchstaben vertauscht
    private const double WeightOmission = 1.0;      // Buchstabe vergessen
    private const double WeightInsertion = 0.9;     // Buchstabe zu viel
    private const double WeightSubstitution = 0.45; // falscher Buchstabe, keine Nachbartaste

    /// <summary>
    /// Falscher Buchstabe, aber auf der Tastatur direkt neben dem richtigen —
    /// so wahrscheinlich wie ein zu viel getippter Buchstabe. Das ist die
    /// Tastatur-Distanz aus dem Noisy-Channel-Modell: ein Danebengriff auf die
    /// Nachbartaste ist etwas anderes als ein Buchstabe von der anderen Seite
    /// der Tastatur.
    /// </summary>
    private const double WeightAdjacentSubstitution = WeightInsertion;

    private readonly WordList _words;
    private readonly SpellSettings _settings;

    public SpellCorrector(WordList words, SpellSettings settings)
    {
        _words = words;
        _settings = settings;
    }

    /// <summary>Ab hier darf der Fuzzy-Abgleich überhaupt raten.</summary>
    private const int MinFuzzyLength = 5;

    /// <summary>
    /// Ist das Wort (unabhängig von Groß-/Kleinschreibung) in der Wortliste
    /// bekannt? Für den <see cref="ReplacementTable"/>-Schritt: Ein Kandidat
    /// wird nur übernommen, wenn er dadurch zu einem echten Wort wird.
    /// </summary>
    public bool IsKnownWord(string word) => _words.Knows(word) || _words.ProperNoun(word) is not null;

    /// <param name="word">Das zu prüfende Wort.</param>
    /// <param name="precededByDeterminer">
    /// Stand direkt davor ein Artikel/Possessivpronomen? Entscheidet bei
    /// mehrdeutigen Substantiven (siehe <see cref="WordList.IsCapitalisedOnly"/>),
    /// ob großgeschrieben wird.
    /// </param>
    public string? Suggest(string word, bool precededByDeterminer = false)
    {
        if (word.Length < _settings.MinLength)
            return null;

        // Name/Eigenname (GitHub, iPhone, Montag) — exakte Schreibweise erzwingen.
        if (_words.ProperNoun(word) is { } proper && !string.Equals(proper, word, StringComparison.Ordinal))
            return proper;

        if (_words.Knows(word))
        {
            // Richtig geschrieben — aber vielleicht ein klein getipptes Substantiv?
            return _words.IsCapitalisedOnly(word, precededByDeterminer)
                ? char.ToUpperInvariant(word[0]) + word[1..]
                : null;
        }

        if (word.Length < MinFuzzyLength)
            return null; // zu kurz zum Raten — nur die exakten Listen greifen hier

        var candidates = Candidates(word.ToLowerInvariant());
        if (candidates.Count == 0)
            return null;

        // Die Art des Vertippers wiegt schwerer als die Worthäufigkeit: ein
        // vergessener Buchstabe ist kategorisch wahrscheinlicher als ein
        // danebengegriffener, auch wenn das andere Wort häufiger vorkommt.
        // Innerhalb derselben Fehler-Art entscheidet dann die Häufigkeit.
        var bestKind = candidates.Values.Max();
        var ranked = candidates
            .Where(c => c.Value == bestKind)
            .Select(c => (Word: c.Key, Frequency: Math.Max(_words.Frequency(c.Key), 1)))
            .OrderByDescending(x => x.Frequency)
            .ToList();

        if (ranked.Count > 1 && ranked[0].Frequency < ranked[1].Frequency * _settings.Dominance)
            return null;

        var best = ranked[0].Word;

        if (_words.ProperNoun(best) is { } properBest)
            return properBest;

        // Ein Substantiv gehört groß, egal wie der Nutzer es getippt hat.
        if (_words.IsCapitalisedOnly(best.ToLowerInvariant(), precededByDeterminer))
            return char.ToUpperInvariant(best[0]) + best[1..];

        return MatchLeadingCase(best, word);
    }

    /// <summary>
    /// Übernimmt die Groß-/Kleinschreibung des getippten Worts. Hier wird nur
    /// die Rechtschreibung korrigiert — ob ein Substantiv groß gehört, hängt
    /// vom Satzkontext ab und ist nicht Aufgabe dieser Klasse.
    /// </summary>
    private static string MatchLeadingCase(string candidate, string typed)
    {
        if (candidate.Length == 0 || typed.Length == 0)
            return candidate;

        return char.IsUpper(typed[0])
            ? char.ToUpperInvariant(candidate[0]) + candidate[1..]
            : char.ToLowerInvariant(candidate[0]) + candidate[1..];
    }

    /// <summary>Bekannte Wörter mit Abstand 1, jeweils mit dem besten Gewicht.</summary>
    private Dictionary<string, double> Candidates(string word)
    {
        var found = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        void Consider(string candidate, double weight)
        {
            if (candidate.Length == 0 || string.Equals(candidate, word, StringComparison.OrdinalIgnoreCase))
                return;
            if (!_words.Knows(candidate))
                return;
            if (!found.TryGetValue(candidate, out var best) || weight > best)
                found[candidate] = weight;
        }

        // Bei genau 5 Zeichen nur die „harmlosen" Kanten: einen vergessenen
        // Buchstaben ergänzen oder zwei vertauschte drehen. Streichen und
        // Ersetzen machen aus kurzen Wörtern zu leicht ein anderes echtes Wort
        // ("skill" -> "still"/"kill"). Ab 6 Zeichen ist alles erlaubt.
        var full = word.Length >= 6;

        for (var i = 0; i <= word.Length; i++)
        {
            var left = word[..i];
            var right = word[i..];

            if (right.Length > 0 && full)
                Consider(left + right[1..], WeightInsertion);              // ein Buchstabe zu viel getippt

            if (right.Length > 1)
                Consider(left + right[1] + right[0] + right[2..], WeightTransposition);

            foreach (var c in Alphabet)
            {
                if (right.Length > 0 && full)
                {
                    var weight = KeyboardLayout.AreNeighbours(c, right[0])
                        ? WeightAdjacentSubstitution
                        : WeightSubstitution;
                    Consider(left + c + right[1..], weight);                // falscher Buchstabe
                }

                Consider(left + c + right, WeightOmission);                // Buchstabe vergessen
            }
        }

        return found;
    }
}
