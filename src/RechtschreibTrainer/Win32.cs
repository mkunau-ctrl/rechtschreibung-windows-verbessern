using System.Runtime.InteropServices;
using System.Text;

namespace RechtschreibTrainer;

internal static class Win32
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    // ---- Passwortfeld-Erkennung ----

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public int left, top, right, bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    private const int GWL_STYLE = -16;

    /// <summary>Fensterstil eines Eingabefelds, das Zeichen als Punkte anzeigt.</summary>
    private const long ES_PASSWORD = 0x0020;

    /// <summary>
    /// Hat gerade ein klassisches Windows-Passwortfeld den Eingabefokus?
    ///
    /// Absichtlich vorsichtig: Nur echte Edit-Steuerelemente mit dem Stil
    /// ES_PASSWORD werden erkannt. In Browsern, Electron- und WPF-Oberflächen
    /// gibt es kein eigenes Fensterhandle je Feld — dort liefert das hier
    /// <c>false</c>, obwohl ein Passwortfeld aktiv ist. Das ist also eine
    /// zusätzliche Absicherung und **kein Ersatz** für den Pause-Hotkey.
    /// </summary>
    public static bool FocusedFieldIsPassword()
    {
        try
        {
            var info = new GUITHREADINFO();
            info.cbSize = Marshal.SizeOf<GUITHREADINFO>();

            // 0 = der Thread des Vordergrundfensters.
            if (!GetGUIThreadInfo(0, ref info) || info.hwndFocus == IntPtr.Zero)
                return false;

            // Das Stil-Bit 0x20 bedeutet nur bei Edit-Feldern "Passwort";
            // andere Fensterklassen belegen es anders. Deshalb erst die Klasse prüfen.
            var cls = new StringBuilder(64);
            if (GetClassName(info.hwndFocus, cls, cls.Capacity) == 0)
                return false;
            if (!cls.ToString().Contains("edit", StringComparison.OrdinalIgnoreCase))
                return false;

            return (GetWindowLongPtr(info.hwndFocus, GWL_STYLE).ToInt64() & ES_PASSWORD) != 0;
        }
        catch
        {
            // Erkennung darf nie den Betrieb stören — im Zweifel nicht blockieren.
            return false;
        }
    }

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;

    /// <summary>Verhindert, dass Gedrückthalten den Hotkey mehrfach auslöst.</summary>
    public const uint MOD_NOREPEAT = 0x4000;
}
