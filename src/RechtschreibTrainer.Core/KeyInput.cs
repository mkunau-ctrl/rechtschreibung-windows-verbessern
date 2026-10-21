namespace RechtschreibTrainer.Core;

public enum KeyInputKind
{
    /// <summary>Getippter Text, an den WordWatcher weiterreichen.</summary>
    Text,
    /// <summary>Tastenkürzel, Steuerzeichen, Tottaste: Wortpuffer verwerfen.</summary>
    ContextBreak,
    /// <summary>Taste ohne Zeichen (Shift allein, F-Tasten …): nichts tun.</summary>
    Ignore,
}

public readonly record struct KeyInputResult(KeyInputKind Kind, string Text = "");

/// <summary>
/// Entscheidet, was ein Tastendruck für die Wortverfolgung bedeutet — die
/// Win32-Seite (<c>KeyboardHook</c>) liefert nur das von <c>ToUnicode</c>
/// erzeugte Zeichen und den Zustand der Zusatztasten.
///
/// Hintergrund (Runde 3): Bis 2026-10-21 las der Hook den Zustand der
/// Zusatztasten mit <c>GetKeyboardState</c> — im Low-Level-Hook ist das der
/// Zustand des eigenen Threads, also immer „nichts gedrückt". In 49.585
/// echten Anschlägen kamen 0 Großbuchstaben und 0 Fragezeichen an; aus
/// "@" (AltGr+Q) wurde "q", und Strg+A hängte ein "a" ans Wort.
/// </summary>
public static class KeyInput
{
    /// <param name="produced">Was ToUnicode geliefert hat, oder null bei keinem Zeichen.</param>
    /// <param name="deadKey">ToUnicode meldete eine Tottaste (^, ´, `).</param>
    public static KeyInputResult Classify(string? produced, bool ctrl, bool alt, bool win, bool deadKey = false)
    {
        if (deadKey)
            return new(KeyInputKind.ContextBreak);

        var altGr = ctrl && alt;
        if (win || (ctrl && !altGr) || (alt && !altGr))
            return new(KeyInputKind.ContextBreak);

        if (string.IsNullOrEmpty(produced))
            return new(KeyInputKind.Ignore);

        if (produced.Any(char.IsControl))
            return new(KeyInputKind.ContextBreak);

        return new(KeyInputKind.Text, produced);
    }
}
