namespace RechtschreibTrainer.Core;

/// <summary>Kontext zum gerade fertig getippten Wort.</summary>
public readonly record struct WordContext(bool IsSentenceStart);

/// <summary>Woher eine Korrektur stammt — für Benachrichtigung und Lern-Log.</summary>
public enum CorrectionSource
{
    None,
    Dictionary,
    Rule,
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

    public OfflineCorrector(CorrectionDictionary dictionary) => _dictionary = dictionary;

    public CorrectionResult Correct(string word, WordContext context)
    {
        var current = word;
        var source = CorrectionSource.None;

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
