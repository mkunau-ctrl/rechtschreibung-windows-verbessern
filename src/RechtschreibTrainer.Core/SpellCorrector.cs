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

    /// <summary>
    /// Falscher Buchstabe, aber ein bekanntes Verwechslungspaar (siehe
    /// <see cref="LetterConfusions"/>), z.B. i/e — genauso plausibel wie ein
    /// Danebengriff auf die Nachbartaste, auch wenn die Tasten selbst weit
    /// auseinander liegen. Gilt bewusst auch bei genau 5 Zeichen (im
    /// Unterschied zum generischen Ersetzen), weil es nur ein einziges,
    /// belegtes Paar ist und keine Tür für beliebige Verwechslungen öffnet.
    /// </summary>
    private const double WeightConfusable = WeightAdjacentSubstitution;

    private readonly WordList _words;
    private readonly SpellSettings _settings;
    private readonly CompoundSplitter _compounds;

    public SpellCorrector(WordList words, SpellSettings settings)
    {
        _words = words;
        _settings = settings;
        _compounds = new CompoundSplitter(words.Knows);
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
    /// <param name="precededByContraction">Stand direkt davor zum/beim/vom/…? (substantivierter Infinitiv)</param>
    public string? Suggest(string word, bool precededByDeterminer = false, bool precededByContraction = false)
    {
        if (word.Length < MinTypoLength)
            return null;

        var lower = word.ToLowerInvariant();

        if (word.Length < _settings.MinLength)
        {
            // Kurze Wörter (3 Zeichen): bekannte nie anfassen - außer sie sind
            // nur als seltener Name bekannt ("wil" = Ort Wil) -, unbekannte
            // nur mit den sicheren Kurzwort-Regeln.
            if (_words.Knows(word))
                return Finish(RareNameTypo(word), word, precededByDeterminer, precededByContraction);
            return Finish(CollapseRepeats(lower) ?? ShortWordGuess(lower), word, precededByDeterminer, precededByContraction);
        }

        // Name/Eigenname (GitHub, iPhone, Montag) — exakte Schreibweise erzwingen.
        // Aber nicht bei Vornamen, die klein ein gewöhnliches Wort sind ("anders").
        if (_words.ProperNounReading(word) is { } proper && !string.Equals(proper, word, StringComparison.Ordinal))
            return proper;

        if (_words.Knows(word))
        {
            // Bekannt, aber so selten, dass es fast sicher ein Vertipper ist?
            if ((RealWordError(word) ?? RareNameTypo(word)) is { } meant)
                return Finish(meant, word, precededByDeterminer, precededByContraction);

            // Richtig geschrieben — aber vielleicht ein klein getipptes Substantiv?
            return _words.IsCapitalisedOnly(word, precededByDeterminer, precededByContraction)
                ? char.ToUpperInvariant(word[0]) + word[1..]
                : null;
        }

        // Kein Vertipper, sondern ein nicht gelistetes, aber gültiges Wort:
        // Bindestrich-Wort, Genitiv eines Namens, zerlegbares Kompositum.
        // Ausnahme: Ein häufiges bekanntes Wort ist nur einen Buchstaben
        // entfernt - dann war es doch ein Vertipper ("mittlerweilen" ließe
        // sich in "mittler|weilen" zerlegen, gemeint ist "mittlerweile").
        if (LooksValidAnyway(word, lower) == true && !HasCommonNeighbour(lower))
            return CompoundCase(word, lower, precededByDeterminer);

        // Doppelt getippte Buchstaben ("kannn", "allles") sind der eindeutigste
        // Vertipper überhaupt — vor allem anderen prüfen.
        if (CollapseRepeats(lower) is { } collapsed)
            return Finish(collapsed, word, precededByDeterminer, precededByContraction);

        if (word.Length < MinFuzzyLength)
            return Finish(ShortWordGuess(lower), word, precededByDeterminer, precededByContraction);

        var candidates = Candidates(lower);
        if (candidates.Count == 0)
            return MissingSpace(lower, word) ?? Finish(TwoEdits(lower), word, precededByDeterminer, precededByContraction);

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

        // Ausnahme (Runde 3): Ist ein Kandidat einer FAST gleich plausiblen
        // Fehler-Art (höchstens 0,1 Gewicht schwächer) riesig viel häufiger,
        // gewinnt er - "anderern" -> "anderen" (zu viel getippt, 86.205)
        // statt "naderern" (vertauscht, praktisch nie benutzt).
        var nearby = candidates
            .Where(c => c.Value < bestKind && c.Value >= bestKind - 0.1 - 1e-9)
            .Select(c => (Word: c.Key, Frequency: _words.Frequency(c.Key)))
            .OrderByDescending(x => x.Frequency)
            .FirstOrDefault();
        if (nearby.Word is not null && nearby.Frequency >= ranked[0].Frequency * FrequencyOverride)
            return Finish(nearby.Word, word, precededByDeterminer, precededByContraction);

        if (ranked.Count > 1 && ranked[0].Frequency < ranked[1].Frequency * _settings.Dominance)
            return null;

        return Finish(ranked[0].Word, word, precededByDeterminer, precededByContraction);
    }

    /// <summary>So viel häufiger muss ein Kandidat der nächstschwächeren Fehler-Art sein, um zu gewinnen.</summary>
    private const double FrequencyOverride = 1_000;

    /// <summary>Ab dieser Länge (getippt und Ergebnis) dürfen zwei Fehler auf einmal korrigiert werden.</summary>
    private const int TwoEditMinLength = 6;

    /// <summary>
    /// true = gültiges, nur nicht gelistetes Wort (nicht raten); null = keine Aussage.
    /// </summary>
    private bool? LooksValidAnyway(string word, string lower)
    {
        // "Fleisch- und Wurstwaren": abgetrenntes Kompositum-Stück
        if (word.EndsWith('-') || word.StartsWith('-'))
            return true;

        // "E-Mails", "Kühl-Gefrier-Kombination": jeder Teil bekannt
        if (word.Contains('-'))
        {
            var parts = word.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1 && parts.All(p => _words.Knows(p) || _compounds.CanSplit(p.ToLowerInvariant()) || p.Length <= 2))
                return true;
        }

        // Genitiv eines Namens: "Lenas", "Peters"
        if (lower.EndsWith('s') && _words.ProperNoun(word[..^1]) is not null)
            return true;

        return _compounds.CanSplit(lower) ? true : null;
    }

    /// <summary>Ab dieser Häufigkeit schlägt ein Nachbar mit Abstand 1 den Kompositum-Schutz.</summary>
    private const long CompoundOverrideFrequency = 20;

    private bool HasCommonNeighbour(string lower) =>
        lower.Length >= MinFuzzyLength
        && Candidates(lower).Keys.Any(c => _words.Frequency(c) >= CompoundOverrideFrequency);

    /// <summary>Ein erkanntes Kompositum wird großgeschrieben, wenn sein Kopf ein Substantiv ist.</summary>
    private string? CompoundCase(string word, string lower, bool precededByDeterminer)
    {
        if (!char.IsLower(word[0]) || word.Contains('-'))
            return null;
        if (_compounds.Head(lower) is { } head && _words.IsCapitalisedOnly(head, precededByDeterminer))
            return char.ToUpperInvariant(word[0]) + word[1..];
        return null;
    }

    /// <summary>
    /// Kurzes Wort, das nur als seltener Name bekannt ist ("wil" → Ort Wil),
    /// klein getippt: dann ist meist das sehr häufige Nachbarwort gemeint
    /// ("will"). Nicht bei Namen, die klein ein gewöhnliches Wort sind.
    /// </summary>
    private string? RareNameTypo(string word)
    {
        if (word.Length >= MinFuzzyLength || !char.IsLower(word[0]))
            return null;

        var lower = word.ToLowerInvariant();
        if (_words.ProperNoun(lower) is null || _words.IsLowercaseValid(lower))
            return null;

        var own = _words.Frequency(lower);
        if (own > RealWordMaxFrequency)
            return null;

        var guess = ShortWordGuess(lower);
        return guess is not null && _words.Frequency(guess) >= Math.Max(own, 1) * RealWordRatio ? guess : null;
    }

    /// <summary>
    /// Zwei Fehler auf einmal, nur bei längeren Wörtern (ab
    /// <see cref="TwoEditMinLength"/> Zeichen) und nur mit den häufigen
    /// Fehler-Arten (vertauscht, vergessen, zu viel — kein Ersetzen): "vileicht"
    /// → "vielleicht", "sytsme" → "system". Das Ergebnis muss eindeutig sein
    /// (zehnfach häufiger als der Zweite). Am 2026-09-06 war ein ähnlicher
    /// Versuch an der Präzision gescheitert — der lief aber auch über kurze
    /// Wörter und mit Ersetzen; hier wird er mit allen Benchmarks gemessen.
    /// </summary>
    private string? TwoEdits(string lower)
    {
        if (lower.Length < TwoEditMinLength)
            return null;

        var first = CheapEdits(lower).Distinct().ToList();
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (e1, k1) in first)
            foreach (var (e2, k2) in CheapEdits(e1))
            {
                // "zu viel" + "zu viel"/"vergessen" verschiebt oder ersetzt in
                // Wahrheit einen Buchstaben - so entstanden die Fehlgriffe
                // "gebaüde" -> "gebadet" und "auffaekt" -> "Affekt".
                if (k1 == EditKind.Extra && k2 is EditKind.Extra or EditKind.Missing
                    || k2 == EditKind.Extra && k1 == EditKind.Missing)
                    continue;
                if (e2.Length >= TwoEditMinLength && e2 != lower && _words.Knows(e2))
                    found.Add(e2);
            }

        return PickStrict(found, minFrequency: 50);
    }

    private enum EditKind { Extra, Swapped, Missing }

    /// <summary>Alle Varianten mit einem überzähligen, vertauschten oder vergessenen (nicht vorn) Buchstaben.</summary>
    private static IEnumerable<(string Word, EditKind Kind)> CheapEdits(string w)
    {
        for (var i = 0; i <= w.Length; i++)
        {
            var left = w[..i];
            var right = w[i..];
            if (right.Length > 0)
                yield return (left + right[1..], EditKind.Extra);
            if (right.Length > 1)
                yield return (left + right[1] + right[0] + right[2..], EditKind.Swapped);
            if (i > 0)
                foreach (var c in Alphabet)
                    yield return (left + c + right, EditKind.Missing);
        }
    }

    /// <summary>Namen exakt, Substantive groß, sonst die Großschreibung des Getippten.</summary>
    private string? Finish(string? best, string typed, bool precededByDeterminer, bool precededByContraction)
    {
        if (best is null)
            return null;

        if (_words.ProperNounReading(best) is { } properBest)
            return properBest;

        // Ein Substantiv gehört groß, egal wie der Nutzer es getippt hat.
        if (_words.IsCapitalisedOnly(best.ToLowerInvariant(), precededByDeterminer, precededByContraction))
            return char.ToUpperInvariant(best[0]) + best[1..];

        return MatchLeadingCase(best, typed);
    }

    // ---- Runde 3: Regeln aus den echten Tippdaten ----

    /// <summary>Ab dieser Länge werden Vertipper überhaupt betrachtet ("ihc", "mla").</summary>
    private const int MinTypoLength = 3;

    /// <summary>Ein kurzes Wort (3–4 Zeichen) wird nur zu einem so häufigen Wort korrigiert.</summary>
    private const long ShortWordMinFrequency = 5_000;

    /// <summary>
    /// So viel häufiger als der Zweitplatzierte muss das Ziel bei kurzen Wörtern
    /// und Doppelbuchstaben sein — viel strenger als <see cref="SpellSettings.Dominance"/>.
    /// </summary>
    private const double StrictDominance = 10;

    /// <summary>Ein bekanntes Wort mit höchstens dieser Häufigkeit kann ein Vertipper sein …</summary>
    private const long RealWordMaxFrequency = 200;

    /// <summary>… wenn ein Nachbar mindestens so viel häufiger ist.</summary>
    private const double RealWordRatio = 500;

    /// <summary>
    /// Doppelt/mehrfach getippte Buchstaben: jede Buchstabenwiederholung wird
    /// testweise um einen Buchstaben gekürzt (und Dreifache auf einen). Ergibt
    /// genau ein bekanntes Wort (oder eines, das klar häufiger ist) → das.
    /// </summary>
    private string? CollapseRepeats(string lower)
    {
        var variants = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < lower.Length - 1; i++)
        {
            if (lower[i] != lower[i + 1] || !char.IsLetter(lower[i]))
                continue;

            var end = i;
            while (end + 1 < lower.Length && lower[end + 1] == lower[i])
                end++;
            var run = end - i + 1;

            variants.Add(lower[..i] + lower[(i + 1)..]);                     // einen weniger
            if (run >= 3)
            {
                variants.Add(lower[..i] + lower[i] + lower[(end + 1)..]);    // auf einen
                variants.Add(lower[..i] + lower[i] + lower[i] + lower[(end + 1)..]); // auf zwei
            }
            i = end;
        }

        // Mehrere Wiederholungen auf einmal ("kommmmt" -> "kommt"): alle Läufe
        // von drei und mehr auf zwei kürzen.
        var squashed = System.Text.RegularExpressions.Regex.Replace(lower, @"(\p{L})\1{2,}", "$1$1");
        if (squashed != lower)
            variants.Add(squashed);

        return PickStrict(variants.Where(v => v.Length >= 2 && _words.Knows(v)), minFrequency: 0);
    }

    /// <summary>
    /// Kurze Wörter (3–4 Zeichen): nur vertauschte oder vergessene Buchstaben
    /// (vergessen nicht am Wortanfang), nur zu einem sehr häufigen Wort, das
    /// den Zweitplatzierten klar schlägt. In den echten Daten die häufigsten
    /// übersehenen Vertipper: "ihc", "mla", "wil", "ghet", "kan", "noh".
    /// </summary>
    private string? ShortWordGuess(string lower)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i <= lower.Length; i++)
        {
            var left = lower[..i];
            var right = lower[i..];

            if (right.Length > 1)
                found.Add(left + right[1] + right[0] + right[2..]);

            if (i > 0)
                foreach (var c in Alphabet)
                    found.Add(left + c + right);
        }
        found.Remove(lower);

        return PickStrict(found.Where(_words.Knows), ShortWordMinFrequency);
    }

    /// <summary>
    /// „Echtwort-Fehler": Das Getippte ist zwar ein Wort, aber extrem selten,
    /// und ein vertauschter oder vergessener Buchstabe ergibt ein mindestens
    /// <see cref="RealWordRatio"/>-mal häufigeres ("genua" → "genau",
    /// "mienen" → "meinen"). Nur bei klein getippten Wörtern: Wer "Genua"
    /// groß schreibt, meint die Stadt.
    /// </summary>
    private string? RealWordError(string word)
    {
        if (word.Length < MinFuzzyLength || !char.IsLower(word[0]))
            return null;

        var lower = word.ToLowerInvariant();
        var own = _words.Frequency(lower);
        if (own > RealWordMaxFrequency)
            return null;

        var found = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i <= lower.Length; i++)
        {
            var left = lower[..i];
            var right = lower[i..];
            if (right.Length > 1)
                found.Add(left + right[1] + right[0] + right[2..]);
            if (i > 0)
                foreach (var c in Alphabet)
                    found.Add(left + c + right);
        }
        found.Remove(lower);

        var minFrequency = (long)(Math.Max(own, 1) * RealWordRatio);
        return PickStrict(found.Where(_words.Knows), minFrequency);
    }

    /// <summary>
    /// Fehlende Leertaste: "damitich" → "damit ich", "demspiel" → "dem Spiel".
    /// Links muss ein Funktionswort stehen (siehe <see cref="FunctionWords"/>),
    /// rechts ein bekanntes, häufiges Wort; nach einer Präposition nur ein
    /// weiteres Funktionswort (sonst wäre es eher ein Kompositum). Nur wenn es
    /// genau eine solche Trennung gibt.
    /// </summary>
    private string? MissingSpace(string lower, string typed)
    {
        string? result = null;
        for (var i = 2; i <= lower.Length - 2; i++)
        {
            var left = lower[..i];
            var right = lower[i..];
            if (!FunctionWords.CanStartSplit(left) || !_words.Knows(right))
                continue;
            if (FunctionWords.IsPreposition(left) && !FunctionWords.IsFunctionWord(left) && !FunctionWords.IsFunctionWord(right))
                continue;
            if (_words.Frequency(right) < 1_000 && !FunctionWords.IsFunctionWord(right))
                continue;

            if (result is not null)
                return null; // mehrdeutig

            var evidence = Determiners.Contains(left) || Determiners.IsContraction(left);
            var rightOut = _words.ProperNounReading(right)
                ?? (_words.IsCapitalisedOnly(right, evidence, Determiners.IsContraction(left))
                    ? char.ToUpperInvariant(right[0]) + right[1..]
                    : right);
            result = MatchLeadingCase(left, typed) + " " + rightOut;
        }
        return result;
    }

    /// <summary>Häufigster Kandidat, wenn er häufig genug ist und den Zweiten um <see cref="StrictDominance"/> schlägt.</summary>
    private string? PickStrict(IEnumerable<string> candidates, long minFrequency)
    {
        var ranked = candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(w => (Word: w, Frequency: _words.Frequency(w)))
            .OrderByDescending(x => x.Frequency)
            .Take(2)
            .ToList();

        if (ranked.Count == 0 || ranked[0].Frequency < minFrequency)
            return null;
        if (ranked.Count > 1 && ranked[0].Frequency < Math.Max(ranked[1].Frequency, 1) * StrictDominance)
            return null;
        return ranked[0].Word;
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

            // Verwechslungspaare (i/e) sind auch bei 5 Zeichen erlaubt - im
            // Gegensatz zum generischen Ersetzen unten ist das nur ein
            // einziges begründetes Paar, kein Buchstabe-gegen-jeden-Buchstaben.
            if (right.Length > 0)
            {
                foreach (var confused in LetterConfusions.ConfusedWith(right[0]))
                    Consider(left + confused + right[1..], WeightConfusable);
            }

            foreach (var c in Alphabet)
            {
                if (right.Length > 0 && full)
                {
                    var weight = KeyboardLayout.AreNeighbours(c, right[0])
                        ? WeightAdjacentSubstitution
                        : WeightSubstitution;
                    Consider(left + c + right[1..], weight);                // falscher Buchstabe
                }

                // Buchstabe vergessen - aber nicht am Wortanfang: vorn
                // "vergessene" Buchstaben sind in den echten Daten fast immer
                // Bruchstücke oder Fehlgriffe ("anderern" -> "Wanderern").
                if (i > 0)
                    Consider(left + c + right, WeightOmission);
            }
        }

        return found;
    }
}
