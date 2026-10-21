namespace RechtschreibTrainer.Core;

/// <summary>
/// Wörter, nach denen meist ein Geheimnis folgt ("das Passwort ist …",
/// "PIN: …"). Nutzerwunsch 2026-10-21: „wenn du erkennst, dass es um ein
/// Passwort geht, das nächste Wort nicht weiter angucken". Der
/// <see cref="WordWatcher"/> meldet die folgenden Wörter deshalb als geheim:
/// nicht korrigieren, nicht protokollieren, nicht als Kontext merken.
///
/// Bewusst nicht dabei: "code" — der Nutzer schreibt sehr oft "Claude Code",
/// das würde den Schutz ständig grundlos auslösen. "zugangscode" ist dabei.
/// </summary>
public static class SecretTriggers
{
    private static readonly HashSet<string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        "passwort", "passworts", "passwörter", "passwortes", "kennwort", "kennworts",
        "kennwörter", "password", "passwords", "passwd", "pw", "pwd", "pin", "pincode",
        "tan", "puk", "passcode", "passphrase", "geheimzahl", "zugangscode",
        "zugangsdaten", "login", "logindaten", "anmeldedaten", "wlan-passwort",
        "wlan-schlüssel", "wifi-passwort", "sicherheitscode", "cvc", "cvv",
    };

    /// <summary>Ab dieser Länge zählt auch ein Auslösewort mit einem Tippfehler ("pasword", "kenwort").</summary>
    private const int FuzzyMinLength = 6;

    /// <summary>
    /// Ist das Wort ein Auslösewort — exakt, oder bei längeren Auslösewörtern
    /// auch mit einem Tippfehler (Sicherheits-Review 2026-10-22: sonst hebelt
    /// ein vertipptes "pasword" den Schutz aus)?
    /// </summary>
    public static bool Contains(string word)
    {
        if (Words.Contains(word))
            return true;
        if (word.Length < FuzzyMinLength - 1)
            return false;

        var lower = word.ToLowerInvariant();
        foreach (var t in Words)
            if (t.Length >= FuzzyMinLength && Math.Abs(t.Length - lower.Length) <= 1 && WithinOneEdit(lower, t))
                return true;
        return false;
    }

    /// <summary>Damerau-Abstand höchstens 1 (ein Buchstabe vertauscht, zu viel, zu wenig oder falsch).</summary>
    private static bool WithinOneEdit(string a, string b)
    {
        if (a == b) return true;
        if (a.Length == b.Length)
        {
            var diff = Enumerable.Range(0, a.Length).Where(i => a[i] != b[i]).ToList();
            if (diff.Count == 1) return true;
            return diff.Count == 2 && diff[1] == diff[0] + 1 && a[diff[0]] == b[diff[1]] && a[diff[1]] == b[diff[0]];
        }

        var (shortS, longS) = a.Length < b.Length ? (a, b) : (b, a);
        for (var i = 0; i < longS.Length; i++)
            if (longS.Remove(i, 1) == shortS)
                return true;
        return false;
    }
}
