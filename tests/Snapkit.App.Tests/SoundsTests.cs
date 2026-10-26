using System.IO;
using Snapkit.App.Notifications;
using Snapkit.Core.Sounds;

namespace Snapkit.App.Tests;

public sealed class SoundsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "snapkit-sounds-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Theory]
    [InlineData(SoundKind.Shutter)]
    [InlineData(SoundKind.Tick)]
    [InlineData(SoundKind.Chime)]
    public void EnsureFile_WritesWavOnce(SoundKind kind)
    {
        // Звук играет из файла, а не из памяти: управляемый массив во время звучания может переехать
        var path = Sounds.EnsureFile(_dir, kind);
        Assert.Equal(SoundSynth.Wav(kind), File.ReadAllBytes(path));
        var written = File.GetLastWriteTimeUtc(path);
        Assert.Equal(path, Sounds.EnsureFile(_dir, kind));
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void EnsureFile_RewritesDamagedFile()
    {
        var path = Sounds.EnsureFile(_dir, SoundKind.Tick);
        File.WriteAllBytes(path, [1, 2, 3]);
        Assert.Equal(SoundSynth.Wav(SoundKind.Tick), File.ReadAllBytes(Sounds.EnsureFile(_dir, SoundKind.Tick)));
    }
}
