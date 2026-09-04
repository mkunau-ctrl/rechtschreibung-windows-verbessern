namespace RechtschreibTrainer.Core;

/// <summary>
/// Feste Ersatzschreibungen nach dem Vorbild der <c>REP</c>-Tabelle im
/// deutschen Hunspell-Wörterbuch: Buchstabenfolgen, die für ein anderes
/// Zeichen einstehen, weil man es auf der Tastatur nicht bequem erreicht
/// oder weil die Umlaut-Taste nicht getroffen wurde.
///
/// Der Sinn: Das ist **kein Raten**. Wer <c>fuer</c> tippt, meint <c>für</c> —
/// da muss keine Statistik befragt werden. Hunspell wendet seine REP-Tabelle
/// deshalb mit höchster Priorität an, noch vor jeder Ähnlichkeitssuche.
/// Genau so wird sie hier benutzt.
///
/// Sicherung gegen Fehlgriffe: Der Aufrufer übernimmt einen Vorschlag nur,
/// wenn er ein **bekanntes Wort** ergibt und das getippte Wort selbst
/// unbekannt ist. Dadurch bleiben echte Wörter mit derselben Buchstabenfolge
/// unangetastet (<c>Steuer</c>, <c>neue</c>, <c>dass</c>).
/// </summary>
public static class ReplacementTable
{
    /// <summary>
    /// Die deutschen Ersatzschreibungen. Bewusst nur die eindeutigen
    /// Tastatur-Ersatzformen — die übrigen REP-Paare des Hunspell-Wörterbuchs
    /// (f/ph, d/t, ch/k …) betreffen Rechtschreibunsicherheit, nicht das
    /// Tippen, und würden hier nur Fehlgriffe produzieren.
    /// </summary>
    public static readonly (string From, string To)[] German =
    [
        ("ue", "ü"), ("oe", "ö"), ("ae", "ä"),
        ("Ue", "Ü"), ("Oe", "Ö"), ("Ae", "Ä"),
        ("ss", "ß"),
    ];

    /// <summary>
    /// Alle Schreibweisen, die sich aus dem Wort ergeben, wenn man die
    /// Ersatzformen auflöst — jede einzelne Fundstelle für sich, und zusätzlich
    /// alle Fundstellen auf einmal. Das getippte Wort selbst ist nie dabei.
    /// </summary>
    public static IEnumerable<string> Candidates(string word)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { word };

        // Jede einzelne Fundstelle für sich ersetzen.
        foreach (var (from, to) in German)
        {
            var start = 0;
            while (true)
            {
                var at = word.IndexOf(from, start, StringComparison.Ordinal);
                if (at < 0) break;

                var candidate = word[..at] + to + word[(at + from.Length)..];
                if (seen.Add(candidate))
                    yield return candidate;

                start = at + 1;
            }
        }

        // Und alles auf einmal — für Wörter mit mehreren Ersatzformen.
        var all = word;
        foreach (var (from, to) in German)
            all = all.Replace(from, to, StringComparison.Ordinal);

        if (seen.Add(all))
            yield return all;
    }

    /// <summary>Alle Fundstellen auf einmal aufgelöst; gleich dem Wort, wenn nichts passt.</summary>
    public static string ResolveAll(string word)
    {
        foreach (var (from, to) in German)
            word = word.Replace(from, to, StringComparison.Ordinal);
        return word;
    }
}
