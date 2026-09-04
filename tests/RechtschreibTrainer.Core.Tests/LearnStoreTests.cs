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
}
