using RechtschreibTrainer.Core;

namespace RechtschreibTrainer;

/// <summary>
/// Lädt die Tastenbelegung aus Dokumente\RechtschreibTrainer\tasten.txt.
/// Fehlt die Datei, wird sie mit den Standardwerten angelegt, damit der
/// Nutzer sieht, was er ändern kann.
/// </summary>
internal sealed class HotkeySettings
{
    public HotkeySpec Recording { get; private set; } = HotkeySpec.Parse("F9")!.Value;
    public HotkeySpec Undo { get; private set; } = HotkeySpec.Parse("F10")!.Value;
    public HotkeySpec ToggleCorrection { get; private set; } = HotkeySpec.Parse("F11")!.Value;

    private const string Template =
        "# Tastenbelegung des Rechtschreib-Trainers.\n" +
        "# Format:  aktion = taste   (z. B.  F9  oder  Strg+Alt+K )\n" +
        "# Erlaubt: Strg, Alt, Shift + eine Taste F1..F12, A..Z oder 0..9.\n" +
        "# Nach dem Ändern das Programm neu starten.\n" +
        "#\n" +
        "# Tipp: Auf manchen Laptops sind F9-F12 mit Fn belegt (Lautstärke usw.).\n" +
        "# Dann hier z. B. Strg+Alt+9 eintragen.\n" +
        "\n" +
        "mitschreiben  = F9\n" +
        "rueckgaengig  = F10\n" +
        "korrektur     = F11\n";

    public static HotkeySettings Load()
    {
        var settings = new HotkeySettings();

        if (!File.Exists(AppPaths.HotkeyFile))
        {
            AppPaths.EnsureDataDir();
            File.WriteAllText(AppPaths.HotkeyFile, Template);
            return settings;
        }

        foreach (var raw in File.ReadLines(AppPaths.HotkeyFile))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var action = line[..eq].Trim().ToLowerInvariant();
            var spec = HotkeySpec.Parse(line[(eq + 1)..].Trim());
            if (spec is null) continue;

            switch (action)
            {
                case "mitschreiben": settings.Recording = spec.Value; break;
                case "rueckgaengig" or "rückgängig": settings.Undo = spec.Value; break;
                case "korrektur": settings.ToggleCorrection = spec.Value; break;
            }
        }

        return settings;
    }
}
