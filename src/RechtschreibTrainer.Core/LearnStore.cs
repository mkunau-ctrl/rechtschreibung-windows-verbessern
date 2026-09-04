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
}
