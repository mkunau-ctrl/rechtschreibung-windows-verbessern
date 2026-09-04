using System.Runtime.InteropServices;

namespace RechtschreibTrainer;

/// <summary>
/// Minimaler WH_MOUSE_LL-Hook: meldet nur Maustasten-Klicks, damit der
/// Wortpuffer verworfen wird, sobald der Nutzer den Cursor umsetzt.
/// </summary>
internal sealed class MouseHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private readonly LowLevelMouseProc _proc;
    private IntPtr _hookId = IntPtr.Zero;

    public event Action? ClickDetected;

    public bool IsInstalled => _hookId != IntPtr.Zero;

    public MouseHook() => _proc = HookCallback;

    public void Install()
    {
        if (IsInstalled) return;
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
    }

    public void Uninstall()
    {
        if (!IsInstalled) return;
        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN)
                ClickDetected?.Invoke();
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
