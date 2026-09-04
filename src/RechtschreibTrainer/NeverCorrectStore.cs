namespace RechtschreibTrainer;

/// <summary>
/// Die Wörter, die der Nutzer per F10 zurückgewiesen hat — Eigennamen,
/// Fachbegriffe, englische Wörter. Wird beim Start geladen und bei jeder
/// Rücknahme sofort ergänzt, damit derselbe Fehlgriff nicht wiederkommt.
///
/// Das Set wird als lebende Referenz an den Korrektor gegeben: was hier
/// dazukommt, wirkt ab dem nächsten getippten Wort.
/// </summary>
internal sealed class NeverCorrectStore
{
    private const string Header =
        "# Wörter, die nie korrigiert werden sollen - eines pro Zeile.\n" +
        "# Wächst automatisch, wenn du eine Korrektur mit F10 zurücknimmst.\n" +
        "# Du kannst hier auch selbst Wörter eintragen.\n";

    private readonly HashSet<string> _words = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path;

    public NeverCorrectStore(string path)
    {
        _path = path;
        Load();
    }

    public IReadOnlySet<string> Words => _words;

    private void Load()
    {
        if (!File.Exists(_path))
            return;

        foreach (var raw in File.ReadLines(_path))
        {
            var line = raw.Trim();
            if (line.Length > 0 && !line.StartsWith('#'))
                _words.Add(line);
        }
    }

    /// <summary>Nimmt ein Wort auf und schreibt es sofort in die Datei.</summary>
    public void Add(string word)
    {
        var w = word.Trim();
        if (w.Length == 0 || !_words.Add(w))
            return;

        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(_path))
            File.WriteAllText(_path, Header);

        File.AppendAllText(_path, w + Environment.NewLine);
    }
}
