namespace RechtschreibTrainer.Core;

/// <summary>
/// Eine Tastenkombination — Modifier-Bitmaske (wie Win32 RegisterHotKey sie
/// erwartet) plus virtueller Tastencode. Aus Text geparst, damit der Nutzer
/// die Belegung in einer Datei ändern kann.
/// </summary>
public readonly record struct HotkeySpec(uint Modifiers, uint VirtualKey)
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;

    public static HotkeySpec? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        uint mods = 0;
        uint? vk = null;

        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = raw.ToLowerInvariant();
            switch (part)
            {
                case "strg" or "ctrl" or "control" or "steuerung":
                    mods |= ModControl; break;
                case "alt":
                    mods |= ModAlt; break;
                case "shift" or "umschalt":
                    mods |= ModShift; break;
                default:
                    if (vk is not null) return null; // zwei Nicht-Modifier
                    vk = KeyCode(part);
                    if (vk is null) return null;
                    break;
            }
        }

        return vk is null ? null : new HotkeySpec(mods, vk.Value);
    }

    private static uint? KeyCode(string key)
    {
        if (key.Length == 1)
        {
            var c = char.ToUpperInvariant(key[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
                return c;
        }

        if (key.StartsWith('f') && int.TryParse(key[1..], out var n) && n is >= 1 and <= 12)
            return (uint)(0x70 + (n - 1)); // VK_F1 = 0x70

        return null;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & ModControl) != 0) parts.Add("Strg");
        if ((Modifiers & ModAlt) != 0) parts.Add("Alt");
        if ((Modifiers & ModShift) != 0) parts.Add("Shift");
        parts.Add(KeyName(VirtualKey));
        return string.Join('+', parts);
    }

    private static string KeyName(uint vk) => vk switch
    {
        >= 0x70 and <= 0x7B => "F" + (vk - 0x70 + 1),
        >= 'A' and <= 'Z' or >= '0' and <= '9' => ((char)vk).ToString(),
        _ => "?",
    };
}
