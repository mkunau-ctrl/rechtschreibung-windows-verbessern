using System.Text;

namespace RechtschreibTrainer.Core;

/// <summary>Schreibt die status.json atomar (Temp-Datei + Ersetzen). Wirft nie.</summary>
public static class StatusFileWriter
{
    public static void Write(string pfad, string json)
    {
        try
        {
            var ordner = Path.GetDirectoryName(pfad);
            if (!string.IsNullOrEmpty(ordner)) Directory.CreateDirectory(ordner);
            var tmp = pfad + ".tmp";
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            File.Move(tmp, pfad, true);
        }
        catch
        {
            // Best effort: eine Status-Datei darf die Korrektur nie stören.
        }
    }
}
