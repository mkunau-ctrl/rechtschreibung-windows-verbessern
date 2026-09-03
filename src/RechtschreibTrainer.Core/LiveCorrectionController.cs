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

    public void HandleWord(WordCompleted word)
    {
        if (Paused)
            return;

        var result = _corrector.Correct(word.Word, word.Context);
        if (!result.HasCorrection)
            return;

        var keepsBoundary = word.Boundary == '\n';
        var deleteCount = keepsBoundary ? word.Word.Length : word.Word.Length + 1;
        var insert = keepsBoundary ? result.Corrected : result.Corrected + word.Boundary;
        var originalInsert = keepsBoundary ? word.Word : word.Word + word.Boundary;

        _replace(new ReplacementCommand(deleteCount, insert));
        _lastUndo = new ReplacementCommand(insert.Length, originalInsert);
        _learn(new CorrectionRecord(DateTime.Now, word.Word, result.Corrected, result.Source));
        CorrectionApplied?.Invoke(result);
    }

    public void Undo()
    {
        if (_lastUndo is null)
            return;

        _replace(_lastUndo);
        _lastUndo = null;
    }
}
