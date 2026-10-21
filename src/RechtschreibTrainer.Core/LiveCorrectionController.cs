namespace RechtschreibTrainer.Core;

/// <summary>Anweisung an den Ersetzer: so viele Zeichen löschen, diesen Text tippen.</summary>
public sealed record ReplacementCommand(int DeleteCount, string Insert);

/// <summary>
/// Bindeglied: nimmt fertige Wörter vom <see cref="WordWatcher"/>, lässt sie
/// vom <see cref="OfflineCorrector"/> prüfen und stößt bei einer echten
/// Korrektur die Ersetzung an, protokolliert sie und merkt sie für Undo.
/// Kennt keine Win32-Details — Ersetzen und Loggen sind eingereichte Callbacks.
/// </summary>
public sealed class LiveCorrectionController
{
    private readonly OfflineCorrector _corrector;
    private readonly Action<ReplacementCommand> _replace;
    private readonly Action<CorrectionRecord> _learn;

    private ReplacementCommand? _lastUndo;
    private string? _lastOriginalWord;

    // Beobachtung der letzten Korrektur für die Ablehnung per Rücktaste:
    // wie viele Zeichen stehen noch zwischen Cursor und korrigiertem Wort?
    private CorrectionRecord? _watched;
    private int _charsAfterCorrection;

    public LiveCorrectionController(
        OfflineCorrector corrector,
        Action<ReplacementCommand> replace,
        Action<CorrectionRecord> learn)
    {
        _corrector = corrector;
        _replace = replace;
        _learn = learn;
    }

    public bool Paused { get; set; }

    /// <summary>Wird nach jeder angewandten Korrektur gemeldet (für Benachrichtigung).</summary>
    public event Action<CorrectionResult>? CorrectionApplied;

    /// <summary>
    /// Der Nutzer hat eine Korrektur zurückgenommen. Das Wort gehört auf die
    /// „nie anfassen"-Liste, damit derselbe Fehlgriff nicht wiederkommt.
    /// </summary>
    public event Action<string>? CorrectionRejected;

    /// <param name="word">Das fertig getippte Wort samt Grenzzeichen und Kontext.</param>
    /// <param name="typedSince">
    /// Zeichen, die der Nutzer seit der Wortgrenze schon getippt hat. Sie stehen
    /// auf dem Bildschirm zwischen Cursor und zu korrigierendem Wort, müssen
    /// also mitgelöscht und danach wieder mitgetippt werden. Ohne das fressen
    /// die Rücktasten bei schnellem Tippen das nächste Wort an.
    /// </param>
    /// <summary>
    /// Der Nutzer hat direkt nach einer Korrektur mit der Rücktaste ins
    /// korrigierte Wort hineingelöscht — er wollte sie offenbar nicht. Im
    /// Lern-Log steht dann zusätzlich ein <see cref="CorrectionSource.Rejected"/>-
    /// Eintrag. (Strg+Alt+Z wurde in den echten Daten nie benutzt; die
    /// Rücktaste ist der natürliche Weg.)
    /// </summary>
    public event Action<CorrectionRecord>? CorrectionRejectedByUser;

    /// <summary>Ein Zeichen wurde getippt (für die Rücktasten-Beobachtung).</summary>
    public void OnCharTyped()
    {
        if (_watched is not null)
            _charsAfterCorrection++;
    }

    /// <summary>Rücktaste: erreicht sie das korrigierte Wort, gilt die Korrektur als abgelehnt.</summary>
    public void OnBackspace()
    {
        if (_watched is not { } record)
            return;

        _charsAfterCorrection--;
        if (_charsAfterCorrection >= 0)
            return;

        _watched = null;
        var rejection = record with { At = DateTime.Now, Source = CorrectionSource.Rejected };
        _learn(rejection);
        CorrectionRejectedByUser?.Invoke(rejection);
    }

    /// <summary>Kontextbruch (Klick, Fensterwechsel …): die Rücktaste bezieht sich nicht mehr auf die Korrektur.</summary>
    public void ForgetLastCorrection() => _watched = null;

    public void HandleWord(WordCompleted word, string typedSince = "")
    {
        // Ein weiteres fertiges Wort: die vorige Korrektur ist angenommen.
        _watched = null;

        if (Paused)
            return;

        // Nach Enter steht der Zeilenumbruch zwischen Cursor und Wort. Ihn
        // mitzulöschen und neu zu tippen würde in Chat-Fenstern die Nachricht
        // ein zweites Mal abschicken — also gar nicht anfassen.
        if (word.Boundary == '\n' || word.TypedAfterBoundary.Contains('\n'))
            return;

        if (word.Context.DoNotTouch || word.Context.IsSecret)
            return;

        var result = _corrector.Correct(word.Word, word.Context);
        if (!result.HasCorrection)
            return;

        var tail = word.Boundary + word.TypedAfterBoundary + typedSince;
        var deleteCount = word.Word.Length + tail.Length;
        var insert = result.Corrected + tail;
        var originalInsert = word.Word + tail;

        _replace(new ReplacementCommand(deleteCount, insert));
        _lastUndo = new ReplacementCommand(insert.Length, originalInsert);
        _lastOriginalWord = word.Word;
        var record = new CorrectionRecord(DateTime.Now, word.Word, result.Corrected, result.Source);
        _learn(record);
        _watched = record;
        _charsAfterCorrection = tail.Length;
        CorrectionApplied?.Invoke(result);
    }

    public void Undo()
    {
        if (_lastUndo is null)
            return;

        _replace(_lastUndo);
        _lastUndo = null;
        _watched = null;

        if (_lastOriginalWord is { } rejected)
        {
            CorrectionRejected?.Invoke(rejected);
            _lastOriginalWord = null;
        }
    }
}
