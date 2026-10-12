using System.Text.Encodings.Web;
using System.Text.Json;

namespace RechtschreibTrainer.Core;

/// <summary>
/// Zählt angewandte Korrekturen und baut die status.json für den PC-Hub.
/// Enthält nur Zähler und Zustand, niemals getippten Text.
/// </summary>
public sealed class StatusTracker
{
    private static readonly JsonSerializerOptions Optionen = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly object _lock = new();
    private readonly DateTime _start;
    private DateTime _tag;
    private int _heute;
    private int _gesamt;
    private bool _pausiert;

    public StatusTracker(DateTime jetzt, int heute, int gesamt)
    {
        _start = jetzt;
        _tag = jetzt.Date;
        _heute = heute;
        _gesamt = gesamt;
    }

    public void KorrekturAngewandt(DateTime jetzt)
    {
        lock (_lock)
        {
            Tageswechsel(jetzt);
            _heute++;
            _gesamt++;
        }
    }

    public void SetzePause(bool pausiert)
    {
        lock (_lock) _pausiert = pausiert;
    }

    public string BuildJson(DateTime jetzt)
    {
        int heute, gesamt;
        bool pausiert;
        lock (_lock)
        {
            Tageswechsel(jetzt);
            heute = _heute;
            gesamt = _gesamt;
            pausiert = _pausiert;
        }
        var dauer = jetzt - _start;
        var dokument = new
        {
            schemaVersion = 1,
            zeit = jetzt.ToString("yyyy-MM-dd'T'HH:mm:ss"),
            kacheln = new[]
            {
                new { label = "Live-Korrektur", wert = pausiert ? "pausiert" : "aktiv" },
                new { label = "Korrekturen heute", wert = heute.ToString() },
                new { label = "Korrekturen gesamt", wert = gesamt.ToString() },
                new { label = "Läuft seit (Std:Min)", wert = $"{(int)dauer.TotalHours}:{dauer.Minutes:00}" },
            },
        };
        return JsonSerializer.Serialize(dokument, Optionen);
    }

    public static (int Heute, int Gesamt) Zaehle(IEnumerable<CorrectionRecord> records, DateTime jetzt)
    {
        int heute = 0, gesamt = 0;
        foreach (var r in records)
        {
            gesamt++;
            if (r.At.Date == jetzt.Date) heute++;
        }
        return (heute, gesamt);
    }

    private void Tageswechsel(DateTime jetzt)
    {
        if (jetzt.Date == _tag) return;
        _tag = jetzt.Date;
        _heute = 0;
    }
}
