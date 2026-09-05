using System.Text.Json;
using RechtschreibTrainer.Core;
using Xunit;

namespace RechtschreibTrainer.Core.Tests;

public class LearnStoreTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"lernstore-{Guid.NewGuid():N}.jsonl");

    public void Dispose()
    {
        if (File.Exists(_file)) File.Delete(_file);
    }

    [Fact]
    public void AppendsOneJsonLinePerRecord()
    {
        LearnStore.Append(_file, new CorrectionRecord(DateTime.Now, "cih", "ich", CorrectionSource.Dictionary));
        LearnStore.Append(_file, new CorrectionRecord(DateTime.Now, "nocg", "noch", CorrectionSource.Rule));

        var lines = File.ReadAllLines(_file);
        Assert.Equal(2, lines.Length);

        var first = JsonDocument.Parse(lines[0]).RootElement;
        Assert.Equal("cih", first.GetProperty("before").GetString());
        Assert.Equal("ich", first.GetProperty("after").GetString());
        Assert.Equal("Dictionary", first.GetProperty("source").GetString());
    }

    [Fact]
    public void CreatesTheDirectoryIfMissing()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"lernstore-{Guid.NewGuid():N}");
        var nested = Path.Combine(dir, "sub", "k.jsonl");
        try
        {
            LearnStore.Append(nested, new CorrectionRecord(DateTime.Now, "a", "b", CorrectionSource.Rule));
            Assert.True(File.Exists(nested));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---- ReadAll ----

    [Fact]
    public void ReadAllReturnsEveryAppendedRecord()
    {
        LearnStore.Append(_file, new CorrectionRecord(new DateTime(2026, 1, 1), "cih", "ich", CorrectionSource.Dictionary));
        LearnStore.Append(_file, new CorrectionRecord(new DateTime(2026, 1, 2), "nocg", "noch", CorrectionSource.Rule));

        var records = LearnStore.ReadAll(_file).ToList();

        Assert.Equal(2, records.Count);
        Assert.Equal("cih", records[0].Before);
        Assert.Equal("ich", records[0].After);
        Assert.Equal(CorrectionSource.Dictionary, records[0].Source);
        Assert.Equal("noch", records[1].After);
    }

    [Fact]
    public void ReadAllReturnsEmptyWhenFileIsMissing()
    {
        Assert.Empty(LearnStore.ReadAll(_file));
    }

    [Fact]
    public void ReadAllSkipsBrokenLinesInsteadOfThrowing()
    {
        File.WriteAllLines(_file,
        [
            """{"at":"2026-01-01T00:00:00","before":"cih","after":"ich","source":"Dictionary"}""",
            "kaputte zeile, kein json",
            """{"at":"2026-01-02T00:00:00","before":"nocg","after":"noch","source":"Rule"}""",
        ]);

        var records = LearnStore.ReadAll(_file).ToList();

        Assert.Equal(2, records.Count);
        Assert.Equal("ich", records[0].After);
        Assert.Equal("noch", records[1].After);
    }
}
