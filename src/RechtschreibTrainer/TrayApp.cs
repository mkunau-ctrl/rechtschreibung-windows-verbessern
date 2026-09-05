using System.Text;
using RechtschreibTrainer.Core;

namespace RechtschreibTrainer;

/// <summary>
/// Trägt Tray-Icon, Hotkeys und den Lebenszyklus der Hooks. Die Live-Korrektur
/// läuft, solange sie nicht pausiert ist; der Mitschreib-Modus (Strg+Alt+R)
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
    private readonly HotkeySettings _keys;

    private readonly System.Windows.Forms.Timer _focusTimer;
    private readonly System.Windows.Forms.Timer _debounce;

    private IntPtr _lastForeground;
    private DateTime _lastActivity = DateTime.Now;
    private WordCompleted? _pendingWord;
    private int _correctionCount;

    /// <summary>
    /// Zeichen, die seit der Wortgrenze getippt wurden, solange die Korrektur
    /// noch aussteht. Sie stehen auf dem Bildschirm zwischen Cursor und Wort
    /// und müssen bei der Ersetzung mitbehandelt werden.
    /// </summary>
    private readonly StringBuilder _typedSinceWord = new();

    public TrayApp()
    {
        _ = _hotkeyForm.Handle; // Fenster-Handle sofort erzwingen — dient auch als Marshalling-Ziel

        AppPaths.EnsureDataDir();
        _keys = HotkeySettings.Load();
        var dictionary = DictionaryLoader.Load(out var learned);
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
        _pauseItem = new ToolStripMenuItem($"Live-Korrektur pausieren ({_keys.ToggleCorrection})", null, (_, _) => TogglePause());
        menu.Items.Add(_pauseItem);
        menu.Items.Add($"Letzte Korrektur rückgängig ({_keys.Undo})", null, (_, _) => _controller.Undo());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Wörterbuch öffnen", null, (_, _) => OpenInEditor(AppPaths.UserDictionary));
        menu.Items.Add("Tasten ändern", null, (_, _) => OpenInEditor(AppPaths.HotkeyFile));
        menu.Items.Add($"Mitschreiben umschalten ({_keys.Recording})", null, (_, _) => ToggleRecording());
        menu.Items.Add("Log-Ordner öffnen", null, (_, _) => OpenLogFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => ExitApp());
        _trayIcon.ContextMenuStrip = menu;

        _keyboard.CharacterTyped += c => Post(() => OnChar(c));
        _keyboard.BackspacePressed += () => Post(OnBackspace);
        _keyboard.EnterPressed += () => Post(OnEnter);
        _keyboard.NavigationKeyPressed += () => Post(DropContext);
        _mouse.ClickDetected += () => Post(DropContext);
        _watcher.WordCompleted += w =>
        {
            DebugLog.Write($"WordCompleted '{w.Word}' boundary={(w.Boundary == '\n' ? "\\n" : w.Boundary.ToString())} satzanfang={w.Context.IsSentenceStart}");
            _pendingWord = w;
            _typedSinceWord.Clear();
        };

        // So schnell wie möglich ersetzen. Früher wurde auf 130 ms Tastenruhe
        // gewartet — das war doppelt falsch: Der gemessene Tastenabstand des
        // Nutzers liegt im Median bei 188 ms, die Ersetzung feuerte also
        // mitten im Tippen; und bei durchgehendem Tippen gäbe es überhaupt
        // keine Pause, in der korrigiert werden dürfte. Stattdessen werden
        // jetzt die inzwischen getippten Zeichen mitgezählt (_typedSinceWord)
        // und mitersetzt. Je kürzer der Takt, desto weniger sammelt sich an.
        _debounce = new System.Windows.Forms.Timer { Interval = 25 };
        _debounce.Tick += (_, _) => MaybeCorrectPendingWord();
        _debounce.Start();

        _focusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _focusTimer.Tick += (_, _) => CheckContextStillValid();
        _focusTimer.Start();

        _hotkeyForm.HotkeyPressed += OnHotkey;
        RegisterHotkeys();

        StartLiveCorrection();
        DebugLog.Write($"TrayApp bereit. Wörterbuch geladen. keyboardHook={_keyboard.IsInstalled} mouseHook={_mouse.IsInstalled}");

        if (learned.Count > 0)
        {
            var words = string.Join(", ", learned.Select(c => c.Before));
            _trayIcon.BalloonTipTitle = $"{learned.Count} neue Wörter gelernt";
            _trayIcon.BalloonTipText = $"Aus wiederholten Korrekturen: {words}. Steht jetzt in woerterbuch.txt.";
        }
        else
        {
            _trayIcon.BalloonTipTitle = "Live-Korrektur läuft";
            _trayIcon.BalloonTipText =
                $"Vertipper werden beim Tippen korrigiert. {_keys.ToggleCorrection} pausiert " +
                $"(z. B. vor Passwörtern), {_keys.Undo} macht rückgängig.";
        }
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

        // In einem Passwortfeld wird weder gelesen noch geschrieben.
        if (Win32.FocusedFieldIsPassword())
        {
            DropContext();
            return;
        }

        // Steht noch eine Korrektur aus, gehört dieses Zeichen zu dem, was
        // inzwischen auf dem Bildschirm hinter dem Wort steht.
        if (_pendingWord is not null)
            _typedSinceWord.Append(c);

        _watcher.OnChar(c);
        if (_recording.IsActive) _recording.AppendChar(c);
    }

    private void OnBackspace()
    {
        _lastActivity = DateTime.Now;
        // Der Nutzer bessert selbst nach — nichts mehr automatisch ersetzen.
        ClearPending();
        _watcher.OnBackspace();
        if (_recording.IsActive) _recording.AppendBackspace();
    }

    private void ClearPending()
    {
        _pendingWord = null;
        _typedSinceWord.Clear();
    }

    private void DropContext()
    {
        ClearPending();
        _watcher.Invalidate();
    }

    private void MaybeCorrectPendingWord()
    {
        if (_pendingWord is not { } word)
            return;

        // Nicht in Passwortfelder hineinschreiben.
        if (Win32.FocusedFieldIsPassword())
        {
            DropContext();
            return;
        }

        var typedSince = _typedSinceWord.ToString();
        ClearPending();
        _controller.HandleWord(word, typedSince);
    }

    private void OnEnter()
    {
        _lastActivity = DateTime.Now;
        // Nach Enter wird nicht mehr korrigiert (der Zeilenumbruch lässt sich
        // nicht gefahrlos neu tippen) — also auch nichts offen halten.
        ClearPending();
        _watcher.OnEnter();
        if (_recording.IsActive) _recording.AppendNewline();
    }

    private void CheckContextStillValid()
    {
        var current = Win32.GetForegroundWindow();
        if (current != _lastForeground)
        {
            _lastForeground = current;
            DropContext();
        }
        else if ((DateTime.Now - _lastActivity).TotalSeconds > 4)
        {
            DropContext();
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
        var failed = new List<string>();

        if (!Register(HotkeyRecord, _keys.Recording)) failed.Add($"Mitschreiben ({_keys.Recording})");
        if (!Register(HotkeyUndo, _keys.Undo)) failed.Add($"Rückgängig ({_keys.Undo})");
        if (!Register(HotkeyPause, _keys.ToggleCorrection)) failed.Add($"Korrektur ({_keys.ToggleCorrection})");

        DebugLog.Write($"Hotkeys: Mitschreiben={_keys.Recording} Rückgängig={_keys.Undo} Korrektur={_keys.ToggleCorrection} — fehlgeschlagen: {(failed.Count == 0 ? "keine" : string.Join(", ", failed))}");

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
            _pauseItem.Text = $"Live-Korrektur fortsetzen ({_keys.ToggleCorrection})";
            if (!_recording.IsActive)
            {
                _keyboard.Uninstall();
                _mouse.Uninstall();
            }
            Notify("Live-Korrektur pausiert", "Es wird gerade nichts korrigiert und nichts mitgelesen.");
        }
        else
        {
            _pauseItem.Text = $"Live-Korrektur pausieren ({_keys.ToggleCorrection})";
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
                "Alles Getippte landet in keystrokes.log — keine Passwörter eingeben.");
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
        _debounce.Stop();
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
