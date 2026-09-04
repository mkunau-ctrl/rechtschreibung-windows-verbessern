using System.Runtime.InteropServices;

namespace RechtschreibTrainer;

internal static class Win32
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;

    /// <summary>Verhindert, dass Gedrückthalten den Hotkey mehrfach auslöst.</summary>
    public const uint MOD_NOREPEAT = 0x4000;
}
