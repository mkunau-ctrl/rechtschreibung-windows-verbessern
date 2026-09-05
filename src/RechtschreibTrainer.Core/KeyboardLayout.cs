namespace RechtschreibTrainer.Core;

/// <summary>
/// Physische Tasten-Nachbarschaft auf der deutschen QWERTZ-Tastatur — nach
/// dem Vorbild der <c>.kbd</c>-Dateien von GNU Aspell: eine schlichte Liste,
/// welche Tasten nebeneinander liegen.
///
/// Vereinfachung: Nur Nachbarn in **derselben Reihe** zählen, keine
/// Diagonalen. Eine echte Tastatur hat einen leichten Versatz zwischen den
/// Reihen, aber die meisten Danebengriffe passieren seitlich in derselben
/// Reihe — das deckt den wichtigen Teil der Fälle ab, ohne dass echte
/// Tasten-Koordinaten gepflegt werden müssten.
/// </summary>
public static class KeyboardLayout
{
    private static readonly string[] Rows =
    [
        "1234567890ß",
        "qwertzuiopü",
        "asdfghjklöä",
        "yxcvbnm",
    ];

    private static readonly Dictionary<char, HashSet<char>> Neighbours = Build();

    private static Dictionary<char, HashSet<char>> Build()
    {
        var map = new Dictionary<char, HashSet<char>>();

        static HashSet<char> SetFor(Dictionary<char, HashSet<char>> m, char key) =>
            m.TryGetValue(key, out var set) ? set : m[key] = [];

        foreach (var row in Rows)
        {
            for (var i = 0; i < row.Length; i++)
            {
                if (i > 0) SetFor(map, row[i]).Add(row[i - 1]);
                if (i < row.Length - 1) SetFor(map, row[i]).Add(row[i + 1]);
            }
        }

        return map;
    }

    /// <summary>Liegen die beiden Tasten auf der Tastatur nebeneinander? Groß-/Kleinschreibung egal.</summary>
    public static bool AreNeighbours(char a, char b)
    {
        a = char.ToLowerInvariant(a);
        b = char.ToLowerInvariant(b);
        return Neighbours.TryGetValue(a, out var set) && set.Contains(b);
    }
}
