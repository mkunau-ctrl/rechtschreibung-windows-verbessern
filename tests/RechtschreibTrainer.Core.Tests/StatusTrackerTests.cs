using System.Text.Json;
using RechtschreibTrainer.Core;
using Xunit;

namespace RechtschreibTrainer.Core.Tests;

public class StatusTrackerTests
{
    static readonly DateTime T0 = new(2026, 10, 12, 8, 0, 0);

    static string Wert(string json, string label)
        => JsonDocument.Parse(json).RootElement.GetProperty("kacheln").EnumerateArray()
            .First(k => k.GetProperty("label").GetString() == label).GetProperty("wert").GetString()!;

    [Fact]
    public void Baut_schemaVersion_Zeit_und_vier_Kacheln()
    {
        var json = new StatusTracker(T0, 3, 10).BuildJson(T0.AddMinutes(125));
        var root = JsonDocument.Parse(json).RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("2026-10-12T10:05:00", root.GetProperty("zeit").GetString());
        Assert.Equal(4, root.GetProperty("kacheln").GetArrayLength());
        Assert.Equal("aktiv", Wert(json, "Live-Korrektur"));
        Assert.Equal("3", Wert(json, "Korrekturen heute"));
        Assert.Equal("10", Wert(json, "Korrekturen gesamt"));
        Assert.Equal("2:05", Wert(json, "Läuft seit (Std:Min)"));
    }

    [Fact]
    public void Zaehlt_Korrekturen_heute_und_gesamt()
    {
        var s = new StatusTracker(T0, 0, 5);
        s.KorrekturAngewandt(T0.AddMinutes(1));
        s.KorrekturAngewandt(T0.AddMinutes(2));
        var json = s.BuildJson(T0.AddMinutes(3));
        Assert.Equal("2", Wert(json, "Korrekturen heute"));
        Assert.Equal("7", Wert(json, "Korrekturen gesamt"));
    }

    [Fact]
    public void Tageswechsel_setzt_heute_zurueck_gesamt_bleibt()
    {
        var s = new StatusTracker(T0, 4, 9);
        var json = s.BuildJson(T0.AddDays(1));
        Assert.Equal("0", Wert(json, "Korrekturen heute"));
        Assert.Equal("9", Wert(json, "Korrekturen gesamt"));
        s.KorrekturAngewandt(T0.AddDays(1));
        Assert.Equal("1", Wert(s.BuildJson(T0.AddDays(1)), "Korrekturen heute"));
    }

    [Fact]
    public void Pause_wird_angezeigt()
    {
        var s = new StatusTracker(T0, 0, 0);
        s.SetzePause(true);
        Assert.Equal("pausiert", Wert(s.BuildJson(T0), "Live-Korrektur"));
        s.SetzePause(false);
        Assert.Equal("aktiv", Wert(s.BuildJson(T0), "Live-Korrektur"));
    }

    [Fact]
    public void Zaehle_trennt_heute_von_gesamt()
    {
        var records = new[]
        {
            new CorrectionRecord(T0.AddHours(1), "teh", "the", (CorrectionSource)0),
            new CorrectionRecord(T0.AddDays(-1), "adn", "and", (CorrectionSource)0),
            new CorrectionRecord(T0.AddDays(-5), "fro", "for", (CorrectionSource)0),
        };
        Assert.Equal((1, 3), StatusTracker.Zaehle(records, T0));
        Assert.Equal((0, 0), StatusTracker.Zaehle(Array.Empty<CorrectionRecord>(), T0));
    }

    [Fact]
    public void Json_enthaelt_nie_Woerter_aus_den_Korrekturen()
    {
        var records = new[] { new CorrectionRecord(T0, "geheimwort", "Geheimwort", (CorrectionSource)0) };
        var (heute, gesamt) = StatusTracker.Zaehle(records, T0);
        var json = new StatusTracker(T0, heute, gesamt).BuildJson(T0);
        Assert.DoesNotContain("geheimwort", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Zaehlt_parallel_ohne_Verlust()
    {
        var s = new StatusTracker(T0, 0, 0);
        Parallel.For(0, 1000, _ => s.KorrekturAngewandt(T0));
        Assert.Equal("1000", Wert(s.BuildJson(T0), "Korrekturen gesamt"));
    }
}

public class StatusFileWriterTests
{
    [Fact]
    public void Schreibt_atomar_BOM_frei_und_legt_den_Ordner_an()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rt-status-" + Guid.NewGuid().ToString("N"));
        var pfad = Path.Combine(dir, "x", "status.json");
        try
        {
            StatusFileWriter.Write(pfad, "{\"a\":1}");
            var bytes = File.ReadAllBytes(pfad);
            Assert.Equal("{\"a\":1}", System.Text.Encoding.UTF8.GetString(bytes));
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB);
            Assert.False(File.Exists(pfad + ".tmp"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Wirft_nie_bei_unbeschreibbarem_Pfad()
        => StatusFileWriter.Write(@"Z:\gibt\es\nicht\status.json", "{}");
}
