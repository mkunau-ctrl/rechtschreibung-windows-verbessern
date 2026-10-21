namespace RechtschreibTrainer.Core;

/// <summary>
/// Erkennt deutsche Zusammensetzungen, die in keiner Wortliste stehen
/// ("Transportwege", "Wartebereich", "Präventionsmaßnahmen",
/// "Nutzungslimit"): Lässt sich ein unbekanntes Wort in bekannte Teile
/// zerlegen, ist es kein Vertipper — dann wird nicht geraten. Ohne diesen
/// Schutz machte das Programm aus "Transportwege" das bekannte
/// "Transportweg" und aus "Wartebereich" ein "Wertebereich".
///
/// Absichtlich schlicht (Qualitätsrunde 2, Punkt 4a): zwei oder drei Teile,
/// jeder mindestens <see cref="MinPart"/> Zeichen, optional mit Fugenelement
/// (s, es, n, en, e), und der letzte Teil muss ein Substantiv oder ein
/// bekanntes Wort sein. Im Zweifel gewinnt der Schutz — lieber nicht raten.
/// </summary>
public sealed class CompoundSplitter
{
    /// <summary>Kürzester erlaubter Teil — darunter zerlegt fast alles in irgendwas.</summary>
    public const int MinPart = 4;

    private static readonly string[] Fugen = ["", "s", "es", "n", "en", "e"];

    private readonly Func<string, bool> _knows;

    public CompoundSplitter(Func<string, bool> knows) => _knows = knows;

    /// <summary>Lässt sich das (klein geschriebene) Wort in bekannte Teile zerlegen?</summary>
    public bool CanSplit(string lowerWord) => Split(lowerWord, depth: 0) is not null;

    /// <summary>Der letzte Teil der Zerlegung (der Kopf, bestimmt Wortart/Großschreibung) oder null.</summary>
    public string? Head(string lowerWord) => Split(lowerWord, depth: 0);

    private string? Split(string w, int depth)
    {
        if (w.Length < 2 * MinPart)
            return null;

        // Lange Köpfe zuerst: "transport|wege" vor "transportw|ege".
        for (var i = MinPart; i <= w.Length - MinPart; i++)
        {
            var tail = w[i..];
            // Funktionswörter sind keine Kompositum-Köpfe: "russ|sich" ist
            // ein Vertipper von "russisch", kein Kompositum.
            if (!_knows(tail) || FunctionWords.IsFunctionWord(tail))
                continue;

            var head = w[..i];
            if (FirstPartOk(head, depth))
                return tail;
        }
        return null;
    }

    private bool FirstPartOk(string head, int depth)
    {
        foreach (var fuge in Fugen)
        {
            if (fuge.Length > 0 && !head.EndsWith(fuge, StringComparison.Ordinal))
                continue;

            var stem = head[..^fuge.Length];
            if (stem.Length < MinPart)
                continue;

            if (_knows(stem))
                return true;

            // dreiteilig: "präventions|maß|nahmen" - der vordere Teil selbst ein Kompositum
            if (depth == 0 && Split(stem, depth + 1) is not null)
                return true;
        }
        return false;
    }
}
