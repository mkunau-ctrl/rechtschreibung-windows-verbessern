namespace RechtschreibTrainer.Core;

/// <summary>
/// Wörterbuch fester Vertipper-Ersetzungen (falsch=richtig), aus Textzeilen
/// geladen. Lookup ist case-sensitiv; für ein großgeschriebenes Wort greift
/// ersatzweise der kleingeschriebene Eintrag und der Großbuchstabe am Anfang
/// bleibt erhalten (Satzanfang-Fall).
/// </summary>
public sealed class CorrectionDictionary
{
    private readonly Dictionary<string, string> _entries;

    private CorrectionDictionary(Dictionary<string, string> entries) => _entries = entries;

    public static CorrectionDictionary FromLines(IEnumerable<string> lines)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var sep = line.IndexOf('=');
            if (sep <= 0)
                continue;

            var wrong = line[..sep].Trim();
            var right = line[(sep + 1)..].Trim();
            if (wrong.Length == 0 || right.Length == 0)
                continue;

            entries[wrong] = right; // späterer Eintrag gewinnt
        }

        return new CorrectionDictionary(entries);
    }

    /// <summary>Steht für dieses Wort bereits ein Eintrag da (exakte Schreibweise)?</summary>
    public bool HasEntry(string word) => _entries.ContainsKey(word);

    public bool TryCorrect(string word, out string corrected)
    {
        if (_entries.TryGetValue(word, out var exact))
        {
            corrected = exact;
            return true;
        }

        if (word.Length > 0 && char.IsUpper(word[0]))
        {
            var lower = char.ToLowerInvariant(word[0]) + word[1..];
            if (_entries.TryGetValue(lower, out var mapped) && mapped.Length > 0)
            {
                corrected = char.ToUpperInvariant(mapped[0]) + mapped[1..];
                return true;
            }
        }

        corrected = word;
        return false;
    }
}
