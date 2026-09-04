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
    public void HandleWord(WordCompleted word, string typedSince = "")
    {
        if (Paused)
            return;

        // Nach Enter steht der Zeilenumbruch zwischen Cursor und Wort. Ihn
        // mitzulöschen und neu zu tippen würde in Chat-Fenstern die Nachricht
        // ein zweites Mal abschicken — also gar nicht anfassen.
        if (word.Boundary == '\n')
            return;

        var result = _corrector.Correct(word.Word, word.Context);
        if (!result.HasCorrection)
            return;

        var tail = word.Boundary + typedSince;
        var deleteCount = word.Word.Length + tail.Length;
        var insert = result.Corrected + tail;
        var originalInsert = word.Word + tail;

        _replace(new ReplacementCommand(deleteCount, insert));
        _lastUndo = new ReplacementCommand(insert.Length, originalInsert);
        _lastOriginalWord = word.Word;
        _learn(new CorrectionRecord(DateTime.Now, word.Word, result.Corrected, result.Source));
        CorrectionApplied?.Invoke(result);
    }

    public void Undo()
    {
        if (_lastUndo is null)
            return;

        _replace(_lastUndo);
        _lastUndo = null;

        if (_lastOriginalWord is { } rejected)
        {
            CorrectionRejected?.Invoke(rejected);
            _lastOriginalWord = null;
        }
    }
}
