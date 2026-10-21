namespace RechtschreibTrainer.Core;

/// <summary>
/// Gängige deutsche Abkürzungen mit Punkt. Nach ihnen beginnt kein neuer Satz
/// ("das kostet ca. zehn Euro"), und sie selbst werden nie korrigiert.
/// Abkürzungen mit Punkt in der Mitte ("z.B.", "d.h.") braucht es hier nicht:
/// die erkennt der <see cref="WordWatcher"/> schon als zusammenhängenden Token.
/// </summary>
public static class Abbreviations
{
    private static readonly HashSet<string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        "bzw", "ca", "usw", "ggf", "evtl", "inkl", "exkl", "vgl", "etc", "etw", "sog",
        "bspw", "zb", "zzgl", "abzgl", "bzgl", "ggü", "nr", "str", "tel", "mio", "mrd",
        "std", "min", "sek", "mind", "max", "dr", "prof", "hr", "fr", "jh", "jhd", "lt",
        "ff", "chr", "geb", "gest", "allg", "anm", "ausg", "bd", "bes", "betr", "dt",
        "einschl", "engl", "frz", "gem", "hrsg", "insb", "jun", "kap", "lfd", "mtl",
        "nachm", "orig", "pkt", "rd", "sen", "tägl", "teilw", "u", "ugs", "urspr",
        "vorm", "wg", "zt", "zz", "zzt", "z", "d", "s", "o", "a", "v", "i",
    };

    public static bool Contains(string word) => Words.Contains(word);
}
