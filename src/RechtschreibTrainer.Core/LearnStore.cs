using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RechtschreibTrainer.Core;

/// <summary>Eine angewandte Korrektur, wie sie im Lern-Log landet.</summary>
public sealed record CorrectionRecord(DateTime At, string Before, string After, CorrectionSource Source);

/// <summary>
/// Hängt angewandte Korrekturen als je eine JSON-Zeile an eine Datei an
/// (JSON Lines). Speichert nur vorher/nachher echter Treffer, keinen Fließtext.
/// </summary>
public static class LearnStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Append(string filePath, CorrectionRecord record)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var payload = new
        {
            at = record.At.ToString("o"),
            before = record.Before,
            after = record.After,
            source = record.Source,
        };

        var line = JsonSerializer.Serialize(payload, Options);
        File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
    }

    /// <summary>
    /// Liest alle protokollierten Korrekturen zurück. Fehlt die Datei, kommt
    /// eine leere Liste; eine kaputte einzelne Zeile wird übersprungen statt
    /// die ganze Auswertung scheitern zu lassen.
    /// </summary>
    public static IEnumerable<CorrectionRecord> ReadAll(string filePath)
    {
        if (!File.Exists(filePath))
            yield break;

        foreach (var raw in File.ReadLines(filePath))
        {
            var line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0)
                continue;

            var record = TryParse(line);
            if (record is not null)
                yield return record;
        }
    }

    private static CorrectionRecord? TryParse(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var at = DateTime.Parse(
                root.GetProperty("at").GetString()!,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind);
            var before = root.GetProperty("before").GetString()!;
            var after = root.GetProperty("after").GetString()!;
            var source = Enum.Parse<CorrectionSource>(root.GetProperty("source").GetString()!);
            return new CorrectionRecord(at, before, after, source);
        }
        catch
        {
            return null; // kaputte Log-Zeile - nie die Auswertung abbrechen lassen
        }
    }
}
