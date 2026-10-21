namespace RechtschreibTrainer.Core;

/// <summary>
/// Geschlossene Wortklassen (Pronomen, Artikel, Konjunktionen, Partikeln,
/// Präpositionen) — für die Erkennung einer **fehlenden Leertaste**
/// ("damitich" → "damit ich", "vorallem" → "vor allem").
///
/// Warum nur diese? Ein deutsches Kompositum beginnt nie mit "damit", "wenn",
/// "meinem" oder "ich" — klebt so ein Wort vorn an einem anderen, fehlt fast
/// sicher die Leertaste. Präpositionen dagegen sind übliche Kompositum-Anfänge
/// ("Vorspiel", "Mitarbeiter"); hinter ihnen wird deshalb nur getrennt, wenn
/// auch der Rest ein Funktionswort ist ("vor allem", "mit dem").
/// </summary>
public static class FunctionWords
{
    private static readonly HashSet<string> Prepositions = new(StringComparer.OrdinalIgnoreCase)
    {
        "an", "auf", "aus", "bei", "bis", "durch", "für", "gegen", "hinter", "in", "mit",
        "nach", "neben", "ohne", "seit", "über", "um", "unter", "von", "vor", "zu", "zwischen",
        "wegen", "trotz", "während", "statt",
    };

    private static readonly HashSet<string> Others = new(StringComparer.OrdinalIgnoreCase)
    {
        // Personal-/Reflexivpronomen
        "ich", "du", "er", "sie", "es", "wir", "ihr", "mich", "dich", "sich", "uns", "euch",
        "mir", "dir", "ihm", "ihn", "ihnen", "man",
        // Artikel, Possessiv, Demonstrativ (siehe auch Determiners)
        "der", "die", "das", "den", "dem", "des", "ein", "eine", "einen", "einem", "einer", "eines",
        "kein", "keine", "keinen", "keinem", "keiner", "mein", "meine", "meinen", "meinem", "meiner",
        "dein", "deine", "deinen", "deinem", "deiner", "sein", "seine", "seinen", "seinem", "seiner",
        "ihre", "ihren", "ihrem", "ihrer", "unser", "unsere", "unseren", "unserem", "dieser", "diese",
        "dieses", "diesen", "diesem", "jeder", "jede", "jedes", "jeden", "jedem", "alle", "allem",
        "allen", "alles", "am", "im", "zum", "zur", "beim", "vom", "ins",
        // Konjunktionen, Frage- und Bindewörter
        "und", "oder", "aber", "denn", "doch", "sondern", "dass", "damit", "weil", "wenn", "ob",
        "als", "wie", "wo", "was", "wer", "warum", "wieso", "weshalb", "wann", "womit", "woher",
        "wohin", "obwohl", "falls", "sodass", "bevor", "nachdem", "sobald", "solange",
        // häufige Partikeln/Adverbien
        "ja", "nein", "nicht", "noch", "schon", "auch", "nur", "mal", "dann", "so", "gar", "sehr",
        "hier", "da", "dort", "jetzt", "gleich", "eben", "halt", "eh", "bitte", "danke", "okay",
        "ok", "genau", "immer", "nie", "oft", "bald", "erst", "wieder", "etwas", "nichts", "viel",
        "mehr", "ganz", "zu", "also", "eigentlich",
    };

    /// <summary>Darf links von einer fehlenden Leertaste stehen?</summary>
    public static bool CanStartSplit(string word) => Others.Contains(word) || Prepositions.Contains(word);

    public static bool IsPreposition(string word) => Prepositions.Contains(word);

    /// <summary>Ein Funktionswort (keine Präposition)?</summary>
    public static bool IsFunctionWord(string word) => Others.Contains(word);
}
