using System.Text;

namespace RechtschreibTrainer.Core;

/// <summary>Ein fertig getipptes Wort samt Grenzzeichen und Kontext.</summary>
public sealed record WordCompleted(string Word, char Boundary, WordContext Context);

/// <summary>
/// Verfolgt das gerade getippte Wort und den Satzanfang-Zustand aus einem
/// Strom einzelner Tastenereignisse. Kennt keine Win32-Details — Fokuswechsel,
/// Mausklick und Navigationstasten meldet der Aufrufer über <see cref="Invalidate"/>.
///
/// Nach einem <see cref="Invalidate"/> ist der Puffer erst wieder
/// vertrauenswürdig, sobald ein volles Wort frisch an Ort und Stelle getippt
/// wurde; das erste Wort danach löst kein <see cref="WordCompleted"/> aus.
/// </summary>
public sealed class WordWatcher
{
    private readonly StringBuilder _word = new();

    // Beim Start steht der Cursor bekannt dort, wo getippt wird — erst ein
    // Invalidate() (Klick, Fokuswechsel, Navigationstaste) macht ihn unsicher.
    private bool _valid = true;
    private bool _sentenceStart = true;

    public event Action<WordCompleted>? WordCompleted;

    public bool BufferValid => _valid;

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
        _valid = true;

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
        _valid = true;
    }

    public void Invalidate()
    {
        _word.Clear();
        _valid = false;
        _sentenceStart = false;
    }

    private bool Flush(char boundary)
    {
        var emit = _word.Length > 0 && _valid;
        if (emit)
        {
            WordCompleted?.Invoke(new WordCompleted(
                _word.ToString(), boundary, new WordContext(_sentenceStart)));
        }

        _word.Clear();
        return emit;
    }
}
