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

    public static bool Contains(string word) => Words.Contains(word);
}
