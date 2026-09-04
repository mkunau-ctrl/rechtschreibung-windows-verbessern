using RechtschreibTrainer.Core;

namespace RechtschreibTrainer;

/// <summary>
/// Trägt Tray-Icon, Hotkeys und den Lebenszyklus der Hooks. Die Live-Korrektur
/// läuft, solange sie nicht pausiert ist; der Mitschreib-Modus (F9)
/// bleibt als separates Opt-in erhalten.
/// </summary>
internal sealed class TrayApp : ApplicationContext
{
    private const int HotkeyRecord = 1;
    private const int HotkeyPause = 2;
    private const int HotkeyUndo = 3;

    private readonly HotkeyForm _hotkeyForm = new();
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _pauseItem;

    private readonly KeyboardHook _keyboard = new();
    private readonly MouseHook _mouse = new();
    private readonly WordWatcher _watcher = new();
    private readonly Replacer _replacer = new();
    private readonly RecordingSession _recording = new();
    private readonly LiveCorrectionController _controller;
    private readonly NeverCorrectStore _neverCorrect;

    private readonly System.Windows.Forms.Timer _focusTimer;

    private IntPtr _lastForeground;
    private DateTime _lastActivity = DateTime.Now;
    private int _correctionCount;

    public TrayApp()
    {
        _ = _hotkeyForm.Handle; // Fenster-Handle sofort erzwingen — dient auch als Marshalling-Ziel

        AppPaths.EnsureDataDir();
        var dictionary = DictionaryLoader.Load();
        var spelling = DictionaryLoader.LoadSpelling();
        _neverCorrect = new NeverCorrectStore(AppPaths.NeverCorrectList);

        _controller = new LiveCorrectionController(
            new OfflineCorrector(dictionary, spelling, _neverCorrect.Words),
            RunReplacement,
            record => LearnStore.Append(AppPaths.CorrectionLog, record));
        _controller.CorrectionApplied += OnCorrectionApplied;
        _controller.CorrectionRejected += OnCorrectionRejected;

        _trayIcon = new NotifyIcon
        {
            Icon = IconFactory.Create(TrayState.Ready, recording: false),
            Visible = true,
            Text = "Rechtschreib-Trainer — Live-Korrektur aktiv",
        };

        var menu = new ContextMenuStrip();
        _pauseItem = new ToolStripMenuItem("Live-Korrektur pausieren (F11)", null, (_, _) => TogglePause());
        menu.Items.Add(_pauseItem);
        menu.Items.Add("Letzte Korrektur rückgängig (F10)", null, (_, _) => _controller.Undo());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Wörterbuch öffnen", null, (_, _) => OpenInEditor(AppPaths.UserDictionary));
        menu.Items.Add("Tasten ändern", null, (_, _) => OpenInEditor(AppPaths.HotkeyFile));
        menu.Items.Add("Mitschreiben umschalten (F9)", null, (_, _) => ToggleRecording());
        menu.Items.Add("Log-Ordner öffnen", null, (_, _) => OpenLogFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => ExitApp());
        _trayIcon.ContextMenuStrip = menu;

        _keyboard.CharacterTyped += c => Post(() => OnChar(c));
        _keyboard.BackspacePressed += () => Post(OnBackspace);
        _keyboard.EnterPressed += () => Post(OnEnter);
        _keyboard.NavigationKeyPressed += () => Post(_watcher.Invalidate);
        _mouse.ClickDetected += () => Post(_watcher.Invalidate);
        _watcher.WordCompleted += w =>
        {
            DebugLog.Write($"WordCompleted '{w.Word}' boundary={(w.Boundary == '\n' ? "\\n" : w.Boundary.ToString())} satzanfang={w.Context.IsSentenceStart}");
            _controller.HandleWord(w);
        };

        _focusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _focusTimer.Tick += (_, _) => CheckContextStillValid();
        _focusTimer.Start();

        _hotkeyForm.HotkeyPressed += OnHotkey;
        RegisterHotkeys();

        StartLiveCorrection();
        DebugLog.Write($"TrayApp bereit. Wörterbuch geladen. keyboardHook={_keyboard.IsInstalled} mouseHook={_mouse.IsInstalled}");

        _trayIcon.BalloonTipTitle = "Live-Korrektur läuft";
        _trayIcon.BalloonTipText =
            "Bekannte Vertipper werden beim Tippen sofort korrigiert. " +
            "F11 pausiert (z. B. vor Passwörtern), F10 macht rückgängig.";
        _trayIcon.ShowBalloonTip(4000);
    }

    private void Post(Action action)
    {
        if (_hotkeyForm.IsHandleCreated)
            _hotkeyForm.BeginInvoke(action);
    }

    // --- Tasteneingaben (bereits auf dem UI-Thread) ---

    private void OnChar(char c)
    {
        _lastActivity = DateTime.Now;
        _watcher.OnChar(c);
        if (_recording.IsActive) _recording.AppendChar(c);
    }

    private void OnBackspace()
    {
        _lastActivity = DateTime.Now;
        _watcher.OnBackspace();
        if (_recording.IsActive) _recording.AppendBackspace();
    }

    private void OnEnter()
    {
        _lastActivity = DateTime.Now;
        _watcher.OnEnter();
        if (_recording.IsActive) _recording.AppendNewline();
    }

    private void CheckContextStillValid()
    {
        var current = Win32.GetForegroundWindow();
        if (current != _lastForeground)
        {
            _lastForeground = current;
            _watcher.Invalidate();
        }
        else if ((DateTime.Now - _lastActivity).TotalSeconds > 4)
        {
            _watcher.Invalidate();
        }
    }

    // --- Ersetzen ---

    private void RunReplacement(ReplacementCommand cmd)
    {
        try
        {
            DebugLog.Write($"Replace delete={cmd.DeleteCount} insert='{cmd.Insert.Replace("\n", "\\n")}'");
            ShowWorking();
            _replacer.Replace(cmd.DeleteCount, cmd.Insert);
        }
        catch (Exception ex)
        {
            DebugLog.Write($"Replace FAILED: {ex}");
        }
        finally
        {
            RefreshIcon();
        }
    }

    private void OnCorrectionApplied(CorrectionResult result)
    {
        _correctionCount++;
        _trayIcon.Text = $"Rechtschreib-Trainer — {_correctionCount} Korrekturen";
    }

    private void OnCorrectionRejected(string word)
    {
        _neverCorrect.Add(word);
        DebugLog.Write($"zurückgewiesen: '{word}' -> nie-korrigieren.txt");
        Notify("Zurückgenommen", $"„{word}\" wird ab jetzt nicht mehr korrigiert.");
    }

    // --- Hotkeys ---

    private void RegisterHotkeys()
    {
        var keys = HotkeySettings.Load();
        var failed = new List<string>();

        if (!Register(HotkeyRecord, keys.Recording)) failed.Add($"Mitschreiben ({keys.Recording})");
        if (!Register(HotkeyUndo, keys.Undo)) failed.Add($"Rückgängig ({keys.Undo})");
        if (!Register(HotkeyPause, keys.ToggleCorrection)) failed.Add($"Korrektur ({keys.ToggleCorrection})");

        DebugLog.Write($"Hotkeys: Mitschreiben={keys.Recording} Rückgängig={keys.Undo} Korrektur={keys.ToggleCorrection} — fehlgeschlagen: {(failed.Count == 0 ? "keine" : string.Join(", ", failed))}");

        if (failed.Count > 0)
        {
            Notify("Taste konnte nicht belegt werden",
                $"{string.Join("; ", failed)}. Meist hält ein anderes Programm die Taste, oder auf dem Laptop braucht sie Fn. " +
                "Andere Taste im Tray-Menü unter 'Tasten ändern' eintragen und neu starten. Das Menü geht immer.");
        }
    }

    private bool Register(int id, HotkeySpec spec) =>
        Win32.RegisterHotKey(_hotkeyForm.Handle, id, spec.Modifiers | Win32.MOD_NOREPEAT, spec.VirtualKey);

    private void OnHotkey(int id)
    {
        DebugLog.Write($"Hotkey {id} ausgelöst");
        switch (id)
        {
            case HotkeyRecord: ToggleRecording(); break;
            case HotkeyPause: TogglePause(); break;
            case HotkeyUndo: _controller.Undo(); break;
        }
    }

    // --- Live-Korrektur an/aus ---

    private void StartLiveCorrection()
    {
        _keyboard.Install();
        _mouse.Install();
        _lastForeground = Win32.GetForegroundWindow();
    }

    private void TogglePause()
    {
        _controller.Paused = !_controller.Paused;

        if (_controller.Paused)
        {
            _pauseItem.Text = "Live-Korrektur fortsetzen (F11)";
            if (!_recording.IsActive)
            {
                _keyboard.Uninstall();
                _mouse.Uninstall();
            }
            Notify("Live-Korrektur pausiert", "Es wird gerade nichts korrigiert und nichts mitgelesen.");
        }
        else
        {
            _pauseItem.Text = "Live-Korrektur pausieren (F11)";
            StartLiveCorrection();
            _watcher.Invalidate();
            Notify("Live-Korrektur aktiv", "Bekannte Vertipper werden wieder sofort korrigiert.");
        }

        RefreshIcon();
    }

    // --- Mitschreib-Modus ---

    private void ToggleRecording()
    {
        if (_recording.IsActive)
        {
            var (_, length) = _recording.StopAndFlush(AppPaths.KeystrokeLog);
            if (_controller.Paused)
            {
                _keyboard.Uninstall();
                _mouse.Uninstall();
            }
            Notify("Mitschreiben gestoppt", $"{length} Zeichen in keystrokes.log gespeichert.");
        }
        else
        {
            _recording.Start();
            _keyboard.Install();
            Notify("Mitschreiben gestartet",
                "Alles Getippte landet in keystrokes.log — keine Passwörter eingeben. F9 zum Stoppen.");
        }

        RefreshIcon();
    }

    // --- Icon / Benachrichtigung ---

    private void ShowWorking() =>
        _trayIcon.Icon = IconFactory.Create(TrayState.Working, _recording.IsActive);

    private void RefreshIcon()
    {
        var state = _controller.Paused ? TrayState.Paused : TrayState.Ready;
        _trayIcon.Icon = IconFactory.Create(state, _recording.IsActive);
    }

    private void Notify(string title, string text)
    {
        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = text;
        _trayIcon.ShowBalloonTip(2500);
    }

    private static void OpenInEditor(string path)
    {
        if (!File.Exists(path)) File.WriteAllText(path, "");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private static void OpenLogFolder()
    {
        AppPaths.EnsureDataDir();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppPaths.DataDir) { UseShellExecute = true });
    }

    private void ExitApp()
    {
        if (_recording.IsActive)
            _recording.StopAndFlush(AppPaths.KeystrokeLog);

        _focusTimer.Stop();
        _keyboard.Dispose();
        _mouse.Dispose();
        Win32.UnregisterHotKey(_hotkeyForm.Handle, HotkeyRecord);
        Win32.UnregisterHotKey(_hotkeyForm.Handle, HotkeyPause);
        Win32.UnregisterHotKey(_hotkeyForm.Handle, HotkeyUndo);
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _hotkeyForm.Close();
        Application.Exit();
    }
}
