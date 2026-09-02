using System.Text;

namespace RechtschreibTrainer;

/// <summary>
/// Holds the text typed during one recording session (toggle-on to toggle-off)
/// and appends it to the log file when the session ends. Backspace edits the
/// in-memory buffer directly so the log reads as the final typed text, not a
/// raw keystroke stream.
/// </summary>
internal sealed class RecordingSession
{
    private readonly StringBuilder _buffer = new();
    private DateTime? _startedAt;

    public bool IsActive => _startedAt.HasValue;

    public void Start()
    {
        _buffer.Clear();
        _startedAt = DateTime.Now;
    }

    public void AppendChar(char c) => _buffer.Append(c);

    public void AppendBackspace()
    {
        if (_buffer.Length > 0)
            _buffer.Length--;
    }

    public void AppendNewline() => _buffer.Append('\n');

    public (string Text, int Length) StopAndFlush(string logFilePath)
    {
        var text = _buffer.ToString();
        var length = text.Length;
        var startedAt = _startedAt ?? DateTime.Now;

        Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
        using (var writer = new StreamWriter(logFilePath, append: true, Encoding.UTF8))
        {
            writer.WriteLine($"--- Sitzung {startedAt:yyyy-MM-dd HH:mm:ss} bis {DateTime.Now:HH:mm:ss} ---");
            writer.WriteLine(text);
            writer.WriteLine();
        }

        _buffer.Clear();
        _startedAt = null;
        return (text, length);
    }
}
