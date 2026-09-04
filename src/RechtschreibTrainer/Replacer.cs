using System.Runtime.InteropServices;

namespace RechtschreibTrainer;

/// <summary>
/// Ersetzt Text im gerade fokussierten Feld: erst N Rücktasten, dann der
/// neue Text als Unicode-Eingabe. Alle Anschläge tragen den Injected-Marker,
/// damit der KeyboardHook sie nicht erneut verarbeitet.
/// </summary>
internal sealed class Replacer
{
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    // Das Union MUSS alle drei Varianten enthalten: SendInput prüft cbSize
    // gegen die Größe von MOUSEINPUT (der größten Variante) und verwirft die
    // Eingabe kommentarlos, wenn sie nicht passt.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const ushort VK_BACK = 0x08;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// <summary>Größe, die an SendInput als cbSize geht — muss der Win32-Definition entsprechen.</summary>
    public static int InputStructSize => Marshal.SizeOf<INPUT>();

    /// <summary>Löscht <paramref name="deleteCount"/> Zeichen und tippt <paramref name="text"/>.</summary>
    public void Replace(int deleteCount, string text)
    {
        var inputs = new List<INPUT>(deleteCount * 2 + text.Length * 2);

        for (int i = 0; i < deleteCount; i++)
        {
            inputs.Add(Key(VK_BACK, 0, up: false));
            inputs.Add(Key(VK_BACK, 0, up: true));
        }

        foreach (char c in text)
        {
            inputs.Add(Key(0, c, up: false, KEYEVENTF_UNICODE));
            inputs.Add(Key(0, c, up: true, KEYEVENTF_UNICODE));
        }

        var arr = inputs.ToArray();
        var sent = SendInput((uint)arr.Length, arr, Marshal.SizeOf<INPUT>());
        if (sent != arr.Length)
        {
            DebugLog.Write(
                $"SendInput unvollständig: {sent}/{arr.Length} akzeptiert, " +
                $"cbSize={Marshal.SizeOf<INPUT>()} lastError={Marshal.GetLastWin32Error()}");
        }
    }

    private static INPUT Key(ushort vk, ushort scan, bool up, uint extraFlags = 0) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = scan,
                dwFlags = extraFlags | (up ? KEYEVENTF_KEYUP : 0),
                time = 0,
                dwExtraInfo = KeyboardHook.InjectedMarker,
            },
        },
    };
}
