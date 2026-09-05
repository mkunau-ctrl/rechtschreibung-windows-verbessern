using System.Text;

namespace RechtschreibTrainer.Core;

/// <summary>Ein fertig getipptes Wort samt Grenzzeichen und Kontext.</summary>
public sealed record WordCompleted(string Word, char Boundary, WordContext Context);

/// <summary>
/// Verfolgt das gerade getippte Wort und den Satzanfang-Zustand aus einem
/// Strom einzelner Tastenereignisse. Kennt keine Win32-Details — Fokuswechsel,
/// Mausklick und Navigationstasten meldet der Aufrufer über <see cref="Invalidate"/>.
///
/// <see cref="Invalidate"/> verwirft ein angefangenes Wort und setzt den
/// Satzanfang-Zustand zurück (Kontext nach einem Cursor-Sprung unbekannt).
/// Ein danach vollständig frisch getipptes Wort wird ganz normal gemeldet.
/// </summary>
public sealed class WordWatcher
{
    private readonly StringBuilder _word = new();
    private bool _sentenceStart = true;

    // Nach einer Unterbrechung kann der Cursor mitten in einem bestehenden Wort
    // stehen — das nächste fertige Wort ist dann womöglich nur ein Bruchstück.
    private bool _mayBeFragment;

    // Das zuletzt fertig getippte Wort — für PrecededByDeterminer. Wird nach
    // jedem Kontextbruch verworfen, genau wie der Satzanfang-Zustand.
    private string? _previousWord;

    public event Action<WordCompleted>? WordCompleted;

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '-' or '\'';

    private static bool EndsSentence(char c) => c is '.' or '!' or '?';

    public void OnChar(char c)
    {
        if (IsWordChar(c))
        {
            _word.Append(c);
            return;
        }

        var emitted = Flush(c);

        if (EndsSentence(c))
            _sentenceStart = true;
        else if (emitted)
            _sentenceStart = false;
        // reines Leerzeichen ohne Wort: Satzanfang-Zustand unverändert lassen
    }

    public void OnBackspace()
    {
        if (_word.Length > 0)
            _word.Length--;
        else
            Invalidate();
    }

    public void OnEnter()
    {
        Flush('\n');
        _sentenceStart = true;
    }

    public void Invalidate()
    {
        _word.Clear();
        _sentenceStart = false;
        _mayBeFragment = true;
        _previousWord = null;
    }

    private bool Flush(char boundary)
    {
        var emit = _word.Length > 0;
        if (emit)
        {
            var word = _word.ToString();
            var precededByDeterminer = _previousWord is not null && Determiners.Contains(_previousWord);

            WordCompleted?.Invoke(new WordCompleted(
                word, boundary,
                new WordContext(_sentenceStart, AllowSpellGuess: !_mayBeFragment, PrecededByDeterminer: precededByDeterminer)));

            _previousWord = word;
        }

        _word.Clear();

        // Ab der nächsten Wortgrenze steht der Cursor wieder nachweislich dort,
        // wo getippt wurde.
        _mayBeFragment = false;
        return emit;
    }
}
