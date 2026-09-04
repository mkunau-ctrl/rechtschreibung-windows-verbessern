namespace RechtschreibTrainer.Core;

/// <summary>
/// Reine Muster-Regeln, die aus einzelnen Vertippern verallgemeinern.
/// Jede Regel ist bewusst eng gefasst, damit sie kein korrektes Wort zerstört.
/// </summary>
public static class CorrectionRules
{
    public static string Apply(string word)
    {
        var result = word;

        // "scg" ist keine deutsche Buchstabenfolge -> immer "sch" gemeint.
        result = result.Replace("scg", "sch");

        // Motorik-Dreher h->g am Wortende: "nocg" -> "noch".
        if (result.EndsWith("cg"))
            result = result[..^2] + "ch";

        // Klassischer Wortanfang-Dreher: "cih..." -> "ich...".
        if (result.StartsWith("cih"))
            result = "ich" + result[3..];

        return result;
    }
}
