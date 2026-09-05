namespace RechtschreibTrainer.Core;

/// <summary>
/// Artikel, Possessiv- und Demonstrativpronomen — eine geschlossene,
/// grammatisch feste Wortklasse. Steht eines davon direkt vor einem Wort,
/// ist das nächste Wort im Deutschen fast immer ein Substantiv (oder ein
/// dekliniertes Adjektiv davor) — nie ein finites Verb oder ein Adverb.
///
/// Dient als schwaches, aber sicheres Indiz bei mehrdeutigen Wörtern, die
/// sowohl ein häufiges Nicht-Substantiv als auch ein selteneres Substantiv
/// sein können (siehe <see cref="WordList"/>, ambiguousNouns).
/// </summary>
public static class Determiners
{
    private static readonly HashSet<string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        "der", "die", "das", "den", "dem", "des",
        "ein", "eine", "einen", "einem", "einer", "eines",
        "kein", "keine", "keinen", "keinem", "keiner", "keines",
        "mein", "meine", "meinen", "meinem", "meiner", "meines",
        "dein", "deine", "deinen", "deinem", "deiner", "deines",
        "sein", "seine", "seinen", "seinem", "seiner", "seines",
        "ihr", "ihre", "ihren", "ihrem", "ihrer", "ihres",
        "unser", "unsere", "unseren", "unserem", "unserer", "unseres",
        "euer", "eure", "euren", "eurem", "eurer", "eures",
        "dieser", "diese", "dieses", "diesen", "diesem",
        "jener", "jene", "jenes", "jenen", "jenem",
        "jeder", "jede", "jedes", "jeden", "jedem",
        "welcher", "welche", "welches", "welchen", "welchem",
        "alle", "aller", "allem", "allen", "alles",
        "manche", "mancher", "manches", "manchen", "manchem",
    };

    public static bool Contains(string word) => Words.Contains(word);
}
