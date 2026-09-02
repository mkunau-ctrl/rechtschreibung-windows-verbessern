namespace RechtschreibTrainer;

/// <summary>
/// Owns the tray icon, the toggle hotkey and the keyboard hook lifecycle.
/// The hook is installed only between a toggle-on and the matching
/// toggle-off — there is no always-on capture path.
/// </summary>
internal sealed class TrayApp : ApplicationContext
{
    private const int HotkeyId = 1;

    private readonly HotkeyForm _hotkeyForm = new();
    private readonly NotifyIcon _trayIcon;
    private readonly KeyboardHook _hook = new();
    private readonly RecordingSession _session = new();
    private readonly string _logFilePath;

    public TrayApp()
    {
        _logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "RechtschreibTrainer", "keystrokes.log");

        _trayIcon = new NotifyIcon
        {
            Icon = IconFactory.CreateIcon(recording: false),
            Visible = true,
            Text = "Rechtschreib-Trainer (Aufnahme aus)"
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Aufnahme umschalten (Strg+Alt+R)", null, (_, _) => ToggleRecording());
        menu.Items.Add("Log-Ordner öffnen", null, (_, _) => OpenLogFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => ExitApp());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ToggleRecording();

        _hook.CharacterTyped += c => { if (_session.IsActive) _session.AppendChar(c); };
        _hook.BackspacePressed += () => { if (_session.IsActive) _session.AppendBackspace(); };
        _hook.EnterPressed += () => { if (_session.IsActive) _session.AppendNewline(); };

        _hotkeyForm.HotkeyPressed += ToggleRecording;
        _hotkeyForm.HandleCreated += (_, _) =>
        {
            if (!Win32.RegisterHotKey(_hotkeyForm.Handle, HotkeyId, Win32.MOD_CONTROL | Win32.MOD_ALT, Win32.VK_R))
            {
                _trayIcon.BalloonTipTitle = "Hotkey konnte nicht registriert werden";
                _trayIcon.BalloonTipText = "Strg+Alt+R ist evtl. von einem anderen Programm belegt. Umschalten geht weiterhin über das Tray-Menü.";
                _trayIcon.ShowBalloonTip(4000);
            }
        };

        // Force window handle creation so HandleCreated fires and RegisterHotKey has a target.
        _ = _hotkeyForm.Handle;
    }

    private void ToggleRecording()
    {
        if (_session.IsActive)
        {
            _hook.Uninstall();
            var (_, length) = _session.StopAndFlush(_logFilePath);

            _trayIcon.Icon = IconFactory.CreateIcon(recording: false);
            _trayIcon.Text = "Rechtschreib-Trainer (Aufnahme aus)";
            _trayIcon.BalloonTipTitle = "Aufnahme gestoppt";
            _trayIcon.BalloonTipText = $"{length} Zeichen gespeichert.";
            _trayIcon.ShowBalloonTip(2500);
        }
        else
        {
            _session.Start();
            _hook.Install();

            _trayIcon.Icon = IconFactory.CreateIcon(recording: true);
            _trayIcon.Text = "Rechtschreib-Trainer (Aufnahme AN)";
            _trayIcon.BalloonTipTitle = "Aufnahme gestartet";
            _trayIcon.BalloonTipText = "Tippe normal weiter. Keine Passwörter eingeben, solange die Aufnahme läuft — Strg+Alt+R zum Stoppen.";
            _trayIcon.ShowBalloonTip(2500);
        }
    }

    private void OpenLogFolder()
    {
        var dir = Path.GetDirectoryName(_logFilePath)!;
        Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
    }

    private void ExitApp()
    {
        if (_session.IsActive)
            _session.StopAndFlush(_logFilePath);

        _hook.Dispose();
        Win32.UnregisterHotKey(_hotkeyForm.Handle, HotkeyId);
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _hotkeyForm.Close();
        Application.Exit();
    }
}
