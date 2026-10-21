namespace RechtschreibTrainer.Core;

/// <summary>
/// Verdichtet den Korrektur-Log (<see cref="LearnStore"/>) zu neuen
/// Wörterbuch-Kandidaten: Wortpaare, die mehrfach gleich korrigiert wurden,
/// wandern automatisch ins persönliche Wörterbuch, damit beim nächsten Mal
/// der schnelle, sichere Wörterbuch-Weg greift statt jedes Mal neu geraten
/// zu werden.
///
/// Bewusst einfach gehalten: zählt nur exakte (vorher, nachher)-Paare, kein
/// Fuzzy-Abgleich. Ein Wort, das schon im Wörterbuch steht oder auf der
/// „nie korrigieren"-Liste, wird nie vorgeschlagen — der Aufrufer übergibt
/// beide als eine gemeinsame Ausschlussliste.
/// </summary>
public static class DictionaryDistiller
{
    /// <summary>Ein Wörterbuch-Kandidat mit der Anzahl seiner Belege im Log.</summary>
    public sealed record Candidate(string Before, string After, int Count);

    /// <param name="records">Der komplette Korrektur-Log, z. B. aus <see cref="LearnStore.ReadAll"/>.</param>
    /// <param name="excludeBefore">
    /// Wörter, die bereits im persönlichen Wörterbuch stehen oder nie
    /// korrigiert werden sollen — werden nie vorgeschlagen, unabhängig davon,
    /// wie oft sie im Log stehen.
    /// </param>
    /// <param name="threshold">Ab wie vielen gleichen Korrekturen ein Vorschlag entsteht.</param>
    /// <param name="stillValid">
    /// Würde die heutige Logik dieses Paar genauso entscheiden? Das Log enthält
    /// auch Korrekturen älterer, fehlerhafter Versionen — die sollen nicht
    /// nachträglich fest gelernt werden. Null = keine Prüfung.
    /// </param>
    public static IReadOnlyList<Candidate> Distill(
        IEnumerable<CorrectionRecord> records,
        IReadOnlySet<string> excludeBefore,
        int threshold,
        Func<Candidate, bool>? stillValid = null)
    {
        var counts = new Dictionary<(string Before, string After), int>();
        var rejected = new HashSet<(string, string)>();

        foreach (var r in records)
        {
            if (r.Source == CorrectionSource.Rejected)
            {
                rejected.Add((r.Before, r.After));
                continue;
            }

            // Satzanfang-Grossschreibung ist eine reine Positionsregel, keine
            // Rechtschreibkorrektur - "ich" darf nie fest als "ich=Ich" ins
            // Woerterbuch wandern, sonst wuerde es UEBERALL im Satz gross.
            if (r.Source == CorrectionSource.Capitalization)
                continue;

            // Runde 3: genauso jede andere reine Gross-/Kleinschreib-Korrektur.
            // Ob "würde"/"Würde" gross gehoert, entscheidet der Satzkontext
            // live; fest gelernt zerstoerte "würde=Würde" jedes "ich würde".
            if (string.Equals(r.Before, r.After, StringComparison.OrdinalIgnoreCase))
                continue;

            if (excludeBefore.Contains(r.Before))
                continue;

            var key = (r.Before, r.After);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts
            .Where(kv => kv.Value >= threshold && !rejected.Contains(kv.Key))
            .Select(kv => new Candidate(kv.Key.Before, kv.Key.After, kv.Value))
            .Where(c => stillValid is null || stillValid(c))
            .OrderByDescending(c => c.Count)
            .ToList();
    }

    /// <summary>
    /// Wörter, deren Korrektur der Nutzer mindestens <paramref name="threshold"/>
    /// mal per Rücktaste zurückgenommen hat — die gehören auf die
    /// „nie korrigieren"-Liste.
    /// </summary>
    public static IReadOnlyList<string> RejectedOften(IEnumerable<CorrectionRecord> records, int threshold) =>
        records
            .Where(r => r.Source == CorrectionSource.Rejected)
            .GroupBy(r => r.Before, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= threshold)
            .Select(g => g.Key)
            .ToList();
}
