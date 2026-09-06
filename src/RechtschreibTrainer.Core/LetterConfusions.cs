namespace RechtschreibTrainer.Core;

/// <summary>
/// Buchstabenpaare, die beim Tippen leicht verwechselt werden, obwohl sie auf
/// der Tastatur nicht nebeneinander liegen — im Unterschied zu
/// <see cref="KeyboardLayout"/>, das nur die physische Tastenlage abbildet.
///
/// Grund für "i"/"e": im Deutschen sehr häufige Verwechslung (z.B. "necht"
/// statt "nicht"), vermutlich weil beide unbetonte, ähnlich klingende Vokale
/// sind — nicht weil die Tasten benachbart wären (sind sie auf QWERTZ nicht).
/// Bewusst nur dieses eine, belegte Paar: eine lange Liste geratener
/// Verwechslungen würde mehr falsche Treffer erzeugen, als sie Vertipper löst.
/// </summary>
public static class LetterConfusions
{
    private static readonly (char A, char B)[] Pairs = [('i', 'e')];

    /// <summary>Alle Buchstaben, mit denen <paramref name="c"/> häufig verwechselt wird.</summary>
    public static IEnumerable<char> ConfusedWith(char c)
    {
        c = char.ToLowerInvariant(c);
        foreach (var (a, b) in Pairs)
        {
            if (a == c) yield return b;
            if (b == c) yield return a;
        }
    }
}
