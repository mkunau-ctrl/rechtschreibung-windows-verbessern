using System.Runtime.InteropServices;
using System.Text;

namespace RechtschreibTrainer;

/// <summary>
/// Wraps a WH_KEYBOARD_LL hook. Only ever installed while live correction is
/// active or a recording session is running — never runs in the background
/// otherwise. Selbst erzeugte (injizierte) Eingaben werden am Marker in
/// dwExtraInfo erkannt und ignoriert.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    /// <summary>Marker in dwExtraInfo, mit dem der Replacer seine eigenen Tastenanschläge kennzeichnet.</summary>
    public static readonly IntPtr InjectedMarker = new(0x52_54_00); // "RT\0"

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const uint VK_BACK = 0x08;
    private const uint VK_RETURN = 0x0D;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] lpKeyState);

    [DllImport("user32.dll")]
    private static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState,
        StringBuilder pwszBuff, int cchBuff, uint wFlags);

    // Keeps the delegate alive for the lifetime of the hook (GC otherwise
    // collects it and SetWindowsHookEx starts calling into freed memory).
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;

    public event Action<char>? CharacterTyped;
    public event Action? BackspacePressed;
    public event Action? EnterPressed;

    /// <summary>Pfeil-, Pos1/Ende-, Bild-auf/ab- oder Entf-Taste — Signal, den Wortpuffer zu verwerfen.</summary>
    public event Action? NavigationKeyPressed;

    public bool IsInstalled => _hookId != IntPtr.Zero;

    public KeyboardHook()
    {
        _proc = HookCallback;
    }

    public void Install()
    {
        if (IsInstalled) return;
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
    }

    public void Uninstall()
    {
        if (!IsInstalled) return;
        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private static bool IsNavigationKey(uint vk) => vk switch
    {
        0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 or 0x2E => true, // PgUp/PgDn/End/Home/Left/Up/Right/Down/Delete
        _ => false,
    };

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            // Eigene injizierte Tastenanschläge nicht erneut verarbeiten.
            if (hookStruct.dwExtraInfo != InjectedMarker)
            {
                if (hookStruct.vkCode == VK_BACK)
                {
                    BackspacePressed?.Invoke();
                }
                else if (hookStruct.vkCode == VK_RETURN)
                {
                    EnterPressed?.Invoke();
                }
                else if (IsNavigationKey(hookStruct.vkCode))
                {
                    NavigationKeyPressed?.Invoke();
                }
                else
                {
                    var keyboardState = new byte[256];
                    GetKeyboardState(keyboardState);
                    var sb = new StringBuilder(8);
                    int result = ToUnicode(hookStruct.vkCode, hookStruct.scanCode, keyboardState, sb, sb.Capacity, 0);
                    if (result > 0)
                    {
                        foreach (char c in sb.ToString())
                            CharacterTyped?.Invoke(c);
                    }
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
