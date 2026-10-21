using System.Runtime.InteropServices;
using System.Text;
using RechtschreibTrainer.Core;

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
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_CAPITAL = 0x14;
    private const int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3;
    private const int VK_LMENU = 0xA4, VK_RMENU = 0xA5, VK_LWIN = 0x5B, VK_RWIN = 0x5C;

    /// <summary>ToUnicode-Flag: Tastaturzustand (Tottasten) nicht verändern — sonst stört der Hook ^ und ´ des Nutzers.</summary>
    private const uint ToUnicodeKeepState = 0x4;

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private static bool IsModifier(uint vk) => vk is VK_SHIFT or VK_CONTROL or VK_MENU or VK_CAPITAL
        or VK_LSHIFT or VK_RSHIFT or VK_LCONTROL or VK_RCONTROL or VK_LMENU or VK_RMENU or VK_LWIN or VK_RWIN;

    /// <summary>
    /// Baut den Tastaturzustand aus dem echten Zustand der Zusatztasten.
    /// <c>GetKeyboardState</c> taugt im Low-Level-Hook nicht (liefert den
    /// Zustand des eigenen Threads) — damit kamen bis Runde 3 nie
    /// Großbuchstaben, "?", "@" usw. an, siehe <see cref="KeyInput"/>.
    /// </summary>
    private static byte[] CurrentKeyState()
    {
        var state = new byte[256];
        foreach (var vk in new[] { VK_SHIFT, VK_LSHIFT, VK_RSHIFT, VK_CONTROL, VK_LCONTROL, VK_RCONTROL, VK_MENU, VK_LMENU, VK_RMENU })
            if (IsDown(vk)) state[vk] = 0x80;
        if ((GetKeyState(VK_CAPITAL) & 1) != 0) state[VK_CAPITAL] = 0x01;
        return state;
    }

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

    /// <summary>Tastenkürzel (Strg+V, Alt+Tab …), Steuerzeichen oder Tottaste — Wortpuffer verwerfen.</summary>
    public event Action? ContextBroken;

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
        DebugLog.Write($"KeyboardHook.Install -> hookId={_hookId} lastError={Marshal.GetLastWin32Error()}");
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
            // Einzelne Anschläge werden bewusst NICHT mehr protokolliert (auch
            // nicht im Debug-Log): Das wäre ein Tastenprotokoll inklusive
            // Passwörtern. Nutzerwunsch 2026-10-21: Tastenlog bleibt aus.
            if (hookStruct.dwExtraInfo != InjectedMarker && !IsModifier(hookStruct.vkCode))
            {
                var ctrl = IsDown(VK_CONTROL);
                var alt = IsDown(VK_MENU);
                var win = IsDown(VK_LWIN) || IsDown(VK_RWIN);
                var shortcut = win || (ctrl != alt); // Strg+… oder Alt+…, aber nicht AltGr (= beide)

                if (hookStruct.vkCode == VK_BACK)
                {
                    // Strg+Rücktaste löscht ein ganzes Wort - nicht nachbildbar.
                    if (shortcut) ContextBroken?.Invoke();
                    else BackspacePressed?.Invoke();
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
                    var sb = new StringBuilder(8);
                    int result = ToUnicode(hookStruct.vkCode, hookStruct.scanCode, CurrentKeyState(), sb, sb.Capacity, ToUnicodeKeepState);
                    var input = KeyInput.Classify(result > 0 ? sb.ToString(0, result) : null, ctrl, alt, win, deadKey: result < 0);
                    switch (input.Kind)
                    {
                        case KeyInputKind.Text:
                            foreach (char c in input.Text)
                                CharacterTyped?.Invoke(c);
                            break;
                        case KeyInputKind.ContextBreak:
                            ContextBroken?.Invoke();
                            break;
                    }
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
