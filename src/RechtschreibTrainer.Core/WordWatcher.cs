using System.Text;

namespace RechtschreibTrainer.Core;

/// <summary>Ein fertig getipptes Wort samt Grenzzeichen und Kontext.</summary>
/// <param name="TypedAfterBoundary">
/// Zeichen, die nach dem Grenzzeichen schon auf dem Bildschirm stehen, bevor
/// das Wort gemeldet wurde. Normalerweise leer; nach <c>.</c> und <c>:</c>
/// wartet der Watcher auf das nächste Zeichen (E-Mail/URL oder Satzende?) und
/// meldet das Wort erst dann — dieses Zeichen steht hier.
/// </param>
public sealed record WordCompleted(string Word, char Boundary, WordContext Context, string TypedAfterBoundary = "");

/// <summary>
/// Verfolgt das gerade getippte Wort und den Satzanfang-Zustand aus einem
/// Strom einzelner Tastenereignisse. Kennt keine Win32-Details — Fokuswechsel,
/// Mausklick und Navigationstasten meldet der Aufrufer über <see cref="Invalidate"/>.
///
/// <see cref="Invalidate"/> verwirft ein angefangenes Wort und setzt den
/// Satzanfang-Zustand zurück (Kontext nach einem Cursor-Sprung unbekannt).
/// Ein danach vollständig frisch getipptes Wort wird ganz normal gemeldet.
///
/// Seit Runde 3 außerdem:
/// - **Technische Zeichenfolgen** (E-Mail, URL, Pfad, <c>user_name</c>,
///   <c>s24</c>) werden mit <see cref="WordContext.DoNotTouch"/> gemeldet —
///   der ganze Token bis zum nächsten Leerraum.
/// - **Satzanfang** nur, wenn nach <c>. ! ?</c> wirklich Leerraum kommt, nicht
///   nach Abkürzungen (<see cref="Abbreviations"/>) und Ordinalzahlen.
/// - **Passwort-Kontext**: nach einem Auslösewort (<see cref="SecretTriggers"/>)
///   werden die nächsten Wörter als <see cref="WordContext.IsSecret"/> gemeldet.
/// </summary>
public sealed class WordWatcher
{
    /// <summary>So viele Wörter nach einem Passwort-Auslösewort werden nicht angesehen.</summary>
    public const int SecretWordCount = 3;

    private readonly StringBuilder _word = new();
    private bool _sentenceStart = true;

    // Nach einer Unterbrechung kann der Cursor mitten in einem bestehenden Wort
    // stehen — das nächste fertige Wort ist dann womöglich nur ein Bruchstück.
    private bool _mayBeFragment;

    // Die zwei zuletzt fertig getippten Wörter — für den Substantiv-Beleg
    // (PrecededByDeterminer). Werden nach jedem Kontextbruch verworfen, genau
    // wie der Satzanfang-Zustand.
    private string? _previousWord;
    private string? _wordBeforePrevious;

    // Der aktuelle Token (alles seit dem letzten Leerraum) ist technisch:
    // E-Mail, URL, Pfad, Bezeichner. Dann wird darin nichts angefasst.
    private bool _tokenIsTechnical;

    // Wort, das an "." oder ":" endete und auf das nächste Zeichen wartet.
    private (string Word, char Boundary, WordContext Context)? _held;

    // Wie viele der nächsten Tokens noch zum Passwort-Kontext gehören.
    private int _secretWordsLeft;

    // Der aktuelle Token (bis zum nächsten Leerraum) ist geheim - auch über
    // Satzzeichen hinweg ("abc!def" bleibt als Ganzes geheim).
    private bool _tokenIsSecret;

    public event Action<WordCompleted>? WordCompleted;

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '-' or '\'';

    private static bool EndsSentence(char c) => c is '.' or '!' or '?';

    /// <summary>Zeichen, die einen Token sofort als technisch markieren.</summary>
    private static bool IsTechnicalChar(char c) => c is '@' or '/' or '\\' or '_' or '#' or '=' or '<' or '>'
        or '|' or '~' or '`' or '*' or '&' or '%' or '$' or '§' or '^' or '{' or '}' or '[' or ']' or '+';

    /// <summary>Nach diesen Zeichen hängt es vom nächsten Zeichen ab: "gut. " vs. "gmx.de".</summary>
    private static bool WaitsForNextChar(char c) => c is '.' or ':';

    /// <summary>Gibt es gerade ein Wort, das auf das nächste Zeichen wartet?</summary>
    public bool HasHeldWord => _held is not null;

    public void OnChar(char c)
    {
        if (_held is not null)
        {
            if (IsWordChar(c) || IsTechnicalChar(c))
            {
                // "gmx.de", "z.B", "https://", "c:\" - der Punkt/Doppelpunkt
                // steckt mitten in einer Zeichenfolge, also ist der ganze Token
                // technisch.
                _tokenIsTechnical = true;
                ReleaseHeld(nextCharContinuesToken: true, typedAfter: "");
                if (IsWordChar(c))
                {
                    _word.Append(c);
                    return;
                }
                Flush(c);
                return;
            }

            ReleaseHeld(nextCharContinuesToken: false, typedAfter: c.ToString());
            // weitere Satzzeichen ("gut.)", "Ja.“") laufen unten normal weiter -
            // ohne Wort im Puffer wird dabei nichts gemeldet.
            if (char.IsWhiteSpace(c))
            {
                EndToken();
                return;
            }
        }

        if (IsWordChar(c))
        {
            _word.Append(c);
            return;
        }

        if (WaitsForNextChar(c) && _word.Length > 0)
        {
            var word = _word.ToString();
            _word.Clear();
            _held = (word, c, BuildContext(word));
            return;
        }

        if (IsTechnicalChar(c))
        {
            _tokenIsTechnical = true;
            Flush(c);
            _sentenceStart = false;
            return;
        }

        var emitted = Flush(c);

        if (char.IsWhiteSpace(c))
            EndToken();

        if (EndsSentence(c))
            _sentenceStart = true;
        else if (emitted)
            _sentenceStart = false;
        else if (c == ',' && _sentenceStart)
            // Satzzeichen, dann (evtl. über ein schließendes Anführungszeichen
            // hinweg) ein Komma, ohne dass dazwischen ein Wort getippt wurde:
            // wörtliche Rede mit Redebegleitsatz ("Wie war dein Tag?", fragte
            // er) - kein neuer Satz. Ohne diese Ausnahme würde "fragte"
            // fälschlich großgeschrieben.
            _sentenceStart = false;
        // reines Leerzeichen ohne Wort: Satzanfang-Zustand unverändert lassen
    }

    public void OnBackspace()
    {
        if (_held is { } held)
        {
            // Der Punkt/Doppelpunkt wurde wieder gelöscht - das Wort ist wieder offen.
            _held = null;
            _word.Clear().Append(held.Word);
            return;
        }

        if (_word.Length > 0)
            _word.Length--;
        else
            Invalidate();
    }

    public void OnEnter()
    {
        if (_held is not null)
            ReleaseHeld(nextCharContinuesToken: false, typedAfter: "\n");
        else
            Flush('\n');
        _sentenceStart = true;
        EndToken();
        _secretWordsLeft = 0;
    }

    /// <summary>Leerraum/Enter: der Token ist zu Ende.</summary>
    private void EndToken()
    {
        _tokenIsTechnical = false;
        _tokenIsSecret = false;
    }

    /// <summary>
    /// Meldet ein wartendes Wort (Ende auf <c>.</c>/<c>:</c>), wenn nach einer
    /// kurzen Tipppause kein weiteres Zeichen kam — sonst würde das letzte Wort
    /// einer Nachricht ("… ist gut.") nie geprüft. Der Aufrufer entscheidet,
    /// wann die Pause lang genug ist.
    /// </summary>
    public void ReleaseHeldWord()
    {
        if (_held is not null)
            ReleaseHeld(nextCharContinuesToken: false, typedAfter: "");
    }

    public void Invalidate()
    {
        _word.Clear();
        _held = null;
        _sentenceStart = false;
        _mayBeFragment = true;
        _previousWord = null;
        _wordBeforePrevious = null;
        _tokenIsTechnical = false;
        // Passwort-Kontext (auch _tokenIsSecret) bleibt bewusst bestehen: ein
        // Klick ins Passwortfeld direkt nach "Passwort:" darf den Schutz nicht
        // aufheben.
    }

    private void ReleaseHeld(bool nextCharContinuesToken, string typedAfter)
    {
        var (word, boundary, context) = _held!.Value;
        _held = null;

        var technical = nextCharContinuesToken || _tokenIsTechnical;
        var abbreviation = boundary == '.' && Abbreviations.Contains(word);
        if (technical || abbreviation)
            context = context with { DoNotTouch = true };

        var endsSentence = boundary == '.' && !technical && !abbreviation && !IsNumber(word);
        Emit(word, boundary, context, typedAfter, endsSentence);

        _sentenceStart = endsSentence;
        if (typedAfter.Contains('\n'))
            _sentenceStart = true;
    }

    private bool Flush(char boundary)
    {
        var emit = _word.Length > 0;
        if (emit)
        {
            var word = _word.ToString();
            var context = BuildContext(word);
            if (IsTechnicalChar(boundary))
                context = context with { DoNotTouch = true };
            Emit(word, boundary, context, "");
        }

        _word.Clear();

        // Ab der nächsten Wortgrenze steht der Cursor wieder nachweislich dort,
        // wo getippt wurde.
        _mayBeFragment = false;
        return emit;
    }

    private WordContext BuildContext(string word)
    {
        var secret = _secretWordsLeft > 0 || _tokenIsSecret;
        var doNotTouch = secret || _tokenIsTechnical || MixesDigitsAndLetters(word);
        return new WordContext(
            _sentenceStart,
            AllowSpellGuess: !_mayBeFragment,
            PrecededByDeterminer: HasNounEvidence(),
            DoNotTouch: doNotTouch,
            IsSecret: secret,
            PrecededByContraction: _previousWord is { } p && Determiners.IsContraction(p));
    }

    private void Emit(string word, char boundary, WordContext context, string typedAfter, bool endsSentence = false)
    {
        WordCompleted?.Invoke(new WordCompleted(word, boundary, context, typedAfter));

        if (context.IsSecret)
        {
            // gezählt wird pro Token, nicht pro Wort
            if (!_tokenIsSecret)
                _secretWordsLeft--;
            _tokenIsSecret = true;
        }
        else if (SecretTriggers.Contains(word))
            _secretWordsLeft = SecretWordCount;

        // Ein Satzende nach einem normalen Wort beendet den Passwort-Kontext.
        // Nach einem geheimen Wort nie: das Satzzeichen kann Teil des
        // Passworts sein (Sicherheits-Review 2026-10-22).
        if ((endsSentence || boundary is '!' or '?') && !context.IsSecret && !SecretTriggers.Contains(word))
            _secretWordsLeft = 0;

        // Technische und geheime Wörter taugen nicht als Kontext fürs nächste
        // Wort - und geheime sollen ohnehin nirgends hängen bleiben.
        _wordBeforePrevious = _previousWord;
        _previousWord = context.DoNotTouch ? null : word;
        _mayBeFragment = false;
    }

    /// <summary>
    /// Substantiv-Beleg für das nächste Wort: Artikel/Possessiv oder Kontraktion
    /// (<c>am im zum …</c>) direkt davor, eine Zahl davor (<c>30 sekunden</c>),
    /// oder Artikel + dekliniertes Adjektiv davor (<c>die ganze zeit</c>).
    /// </summary>
    private bool HasNounEvidence()
    {
        if (_previousWord is not { } p1)
            return false;

        if (Determiners.Contains(p1) || Determiners.IsContraction(p1) || IsNumber(p1))
            return true;

        return _wordBeforePrevious is { } p2
            && (Determiners.Contains(p2) || Determiners.IsContraction(p2))
            && LooksLikeDeclinedAdjective(p1);
    }

    private static bool LooksLikeDeclinedAdjective(string w) =>
        w.Length >= 4
        && char.IsLower(w[0])
        && !Determiners.Contains(w)
        && (w.EndsWith('e') || w.EndsWith("en") || w.EndsWith("er") || w.EndsWith("es") || w.EndsWith("em"));

    private static bool IsNumber(string w) => w.Length > 0 && w.All(char.IsDigit);

    private static bool MixesDigitsAndLetters(string w) => w.Any(char.IsDigit) && w.Any(char.IsLetter);
}
