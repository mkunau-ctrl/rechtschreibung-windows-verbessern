namespace RechtschreibTrainer.Core;

/// <summary>Kontext zum gerade fertig getippten Wort.</summary>
/// <param name="IsSentenceStart">Erstes Wort eines Satzes — wird großgeschrieben.</param>
/// <param name="AllowSpellGuess">
/// Ob die große Wortliste raten darf. Nach einem Cursor-Sprung steht das
/// getippte womöglich nur als Bruchstück im Feld; dann sind exakte
/// Wörterbuch-Treffer noch sicher, geratene Kandidaten aber nicht.
/// </param>
public readonly record struct WordContext(bool IsSentenceStart, bool AllowSpellGuess = true);

/// <summary>Woher eine Korrektur stammt — für Benachrichtigung und Lern-Log.</summary>
public enum CorrectionSource
{
    None,
    Dictionary,
    Rule,
    /// <summary>Aus der großen Wortliste erschlossen (Damerau-Abstand 1).</summary>
    Spelling,
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

        // Vom Nutzer zurückgewiesen — schlägt jede andere Quelle.
        if (_neverCorrect.Contains(word))
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
            else if (context.AllowSpellGuess && _spelling?.Suggest(current) is { } fromSpelling)
            {
                current = fromSpelling;
                source = CorrectionSource.Spelling;
            }
        }

        if (context.IsSentenceStart && current.Length > 0 && char.IsLower(current[0]))
        {
            current = char.ToUpperInvariant(current[0]) + current[1..];
            if (source == CorrectionSource.None)
                source = CorrectionSource.Capitalization;
        }

        return new CorrectionResult(word, current, source);
    }
}
