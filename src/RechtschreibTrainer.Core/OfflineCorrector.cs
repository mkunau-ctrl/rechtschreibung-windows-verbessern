namespace RechtschreibTrainer.Core;

/// <summary>Kontext zum gerade fertig getippten Wort.</summary>
/// <param name="IsSentenceStart">Erstes Wort eines Satzes — wird großgeschrieben.</param>
/// <param name="AllowSpellGuess">
/// Ob die große Wortliste raten darf. Nach einem Cursor-Sprung steht das
/// getippte womöglich nur als Bruchstück im Feld; dann sind exakte
/// Wörterbuch-Treffer noch sicher, geratene Kandidaten aber nicht.
/// </param>
/// <param name="PrecededByDeterminer">
/// Substantiv-Beleg (der Name ist historisch): Stand direkt davor ein Artikel
/// oder Possessivpronomen (der/die/das/ein/mein/…), eine Kontraktion
/// (am/im/zum/…), eine Zahl, oder Artikel + dekliniertes Adjektiv ("die
/// ganze …")? Recht sicheres Indiz, dass hier ein Substantiv folgt —
/// entscheidet bei mehrdeutigen Wörtern über die Großschreibung.
/// </param>
/// <param name="DoNotTouch">
/// Gar nicht anfassen: Teil einer E-Mail/URL/eines Pfads, Abkürzung, Wort mit
/// Ziffern, oder Passwort-Kontext.
/// </param>
/// <param name="IsSecret">
/// Wort direkt nach einem Passwort-Auslösewort — darf auch nirgends
/// protokolliert werden (impliziert <paramref name="DoNotTouch"/>).
/// </param>
/// <param name="PrecededByContraction">
/// Stand direkt davor eine Kontraktion (zum/beim/vom/…)? Dann ist ein
/// Infinitiv ein Substantiv („zum Testen").
/// </param>
public readonly record struct WordContext(
    bool IsSentenceStart,
    bool AllowSpellGuess = true,
    bool PrecededByDeterminer = false,
    bool DoNotTouch = false,
    bool IsSecret = false,
    bool PrecededByContraction = false);

/// <summary>Woher eine Korrektur stammt — für Benachrichtigung und Lern-Log.</summary>
public enum CorrectionSource
{
    None,
    Dictionary,
    Rule,
    /// <summary>Aus der großen Wortliste erschlossen (Damerau-Abstand 1).</summary>
    Spelling,
    /// <summary>Feste Ersatzschreibung aufgelöst (ue→ü, oe→ö, ae→ä, ss→ß).</summary>
    Replacement,
    Capitalization,
}

/// <summary>Ergebnis eines Korrektur-Durchgangs für ein Wort.</summary>
public sealed record CorrectionResult(string Original, string Corrected, CorrectionSource Source)
{
    public bool HasCorrection => !string.Equals(Original, Corrected, StringComparison.Ordinal);
}

/// <summary>
/// Reiner Offline-Korrektor: Wörterbuch, dann Muster-Regeln, dann
/// Satzanfang-Großschreibung. Gibt nur bei sicheren Treffern eine Änderung
/// zurück; im Zweifel bleibt das Wort stehen.
/// </summary>
public sealed class OfflineCorrector
{
    private readonly CorrectionDictionary _dictionary;
    private readonly SpellCorrector? _spelling;
    private readonly IReadOnlySet<string> _neverCorrect;

    /// <param name="dictionary">Persönliche und mitgelieferte Vertipper — haben immer Vorrang.</param>
    /// <param name="spelling">Optionale große Wortliste für unbekannte Vertipper.</param>
    /// <param name="neverCorrect">Wörter, die der Nutzer per Undo zurückgewiesen hat.</param>
    public OfflineCorrector(
        CorrectionDictionary dictionary,
        SpellCorrector? spelling = null,
        IReadOnlySet<string>? neverCorrect = null)
    {
        _dictionary = dictionary;
        _spelling = spelling;
        _neverCorrect = neverCorrect ?? new HashSet<string>();
    }

    public CorrectionResult Correct(string word, WordContext context)
    {
        var current = word;
        var source = CorrectionSource.None;

        // Technisch/geheim, oder vom Nutzer zurückgewiesen — schlägt jede andere Quelle.
        if (context.DoNotTouch || context.IsSecret || _neverCorrect.Contains(word))
            return new CorrectionResult(word, word, CorrectionSource.None);

        if (_dictionary.TryCorrect(current, out var fromDict))
        {
            current = fromDict;
            source = CorrectionSource.Dictionary;
        }
        else
        {
            var fromRule = CorrectionRules.Apply(current);
            if (!string.Equals(fromRule, current, StringComparison.Ordinal))
            {
                current = fromRule;
                source = CorrectionSource.Rule;
            }
            else if (_spelling is not null && TryReplacement(current, _spelling, out var fromReplacement))
            {
                current = fromReplacement;
                source = CorrectionSource.Replacement;
            }
            else if (context.AllowSpellGuess && _spelling?.Suggest(current, context.PrecededByDeterminer, context.PrecededByContraction) is { } fromSpelling)
            {
                current = fromSpelling;
                source = CorrectionSource.Spelling;
            }
            // Zwei Fehler auf einmal: Ersatzschreibung UND ein weiterer
            // Vertipper ("zustaedig" -> "zustädig" (Ersatzschreibung, aber
            // unbekannt) -> "zuständig" (Raten)). Das ist ein echtes Raten
            // (kombiniert zwei Fehlerquellen), deshalb hinter AllowSpellGuess.
            else if (context.AllowSpellGuess && _spelling is not null
                && TryReplacementThenSpelling(current, _spelling, context.PrecededByDeterminer, out var viaChain))
            {
                current = viaChain;
                source = CorrectionSource.Spelling;
            }
        }

        // Schlussprüfung (Runde 2, Punkt 1): Was geraten oder per Regel
        // erzeugt wurde, muss aus bekannten Wörtern bestehen - nie ein
        // Nicht-Wort wie "naheste" -> "nahesteh" tippen. Wörterbuch-Einträge
        // sind handgeprüft und bleiben davon unberührt.
        if (source is CorrectionSource.Spelling or CorrectionSource.Rule or CorrectionSource.Replacement
            && _spelling is not null
            && current.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(t => !_spelling.IsKnownWord(t)))
        {
            current = word;
            source = CorrectionSource.None;
        }

        if (context.IsSentenceStart && current.Length > 0 && char.IsLower(current[0]))
        {
            current = char.ToUpperInvariant(current[0]) + current[1..];
            if (source == CorrectionSource.None)
                source = CorrectionSource.Capitalization;
        }

        return new CorrectionResult(word, current, source);
    }

    /// <summary>
    /// Versucht, eine feste Ersatzschreibung (<see cref="ReplacementTable"/>)
    /// aufzulösen. Absicherung: Ist <paramref name="word"/> selbst schon ein
    /// bekanntes Wort, wird nichts angefasst — sonst würde z. B. "Masse"
    /// (korrekt) zu "Maße" (ein anderes Wort) verfälscht. Nur wenn das
    /// getippte Wort unbekannt ist UND genau eine Auflösung ein bekanntes
    /// Wort ergibt, wird sie übernommen.
    /// </summary>
    private static bool TryReplacement(string word, SpellCorrector spelling, out string resolved)
    {
        resolved = word;

        if (spelling.IsKnownWord(word))
            return false; // schon richtig — nicht dran rühren

        foreach (var candidate in ReplacementTable.Candidates(word))
        {
            if (!spelling.IsKnownWord(candidate))
                continue;
            resolved = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Löst eine Ersatzschreibung auf und lässt den Rest vom Fuzzy-Raten
    /// erledigen — für Wörter mit zwei Fehlern auf einmal (Ersatzschreibung
    /// plus ein zusätzlicher Vertipper). Läuft nur, wenn die direkte
    /// Ersatzschreibung (<see cref="TryReplacement"/>) schon gescheitert ist,
    /// also nichts kostet, solange das Wort keine Ersatzform enthält.
    /// </summary>
    private static bool TryReplacementThenSpelling(
        string word, SpellCorrector spelling, bool precededByDeterminer, out string resolved)
    {
        resolved = word;

        // Dieselbe Absicherung wie bei TryReplacement: Ist das Wort selbst
        // schon richtig, wird nichts angefasst (sonst z. B. "besser" ->
        // "beßer" -> geraten zu einem ganz anderen, selten bekannten Wort).
        if (spelling.IsKnownWord(word))
            return false;

        foreach (var candidate in ReplacementTable.Candidates(word))
        {
            if (spelling.Suggest(candidate, precededByDeterminer) is { } suggestion)
            {
                resolved = suggestion;
                return true;
            }
        }

        return false;
    }
}
