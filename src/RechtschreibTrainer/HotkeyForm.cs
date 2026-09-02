namespace RechtschreibTrainer;

/// <summary>
/// Invisible window that only exists to own a message loop, so RegisterHotKey
/// has a window handle to bind to and WM_HOTKEY has somewhere to arrive.
/// </summary>
internal sealed class HotkeyForm : Form
{
    private const int WM_HOTKEY = 0x0312;

    public event Action? HotkeyPressed;

    public HotkeyForm()
    {
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        Location = new System.Drawing.Point(-2000, -2000);
        Opacity = 0;
        Size = new System.Drawing.Size(1, 1);
        Load += (_, _) => Hide();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            HotkeyPressed?.Invoke();
        }
        base.WndProc(ref m);
    }
}
