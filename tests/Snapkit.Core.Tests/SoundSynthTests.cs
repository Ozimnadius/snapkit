using Snapkit.Core.Sounds;

namespace Snapkit.Core.Tests;

public class SoundSynthTests
{
    private static short[] Samples(byte[] wav)
    {
        var count = BitConverter.ToInt32(wav, 40) / 2;
        return Enumerable.Range(0, count).Select(i => BitConverter.ToInt16(wav, 44 + i * 2)).ToArray();
    }

    [Theory]
    [InlineData(SoundKind.Shutter, 0.2)]
    [InlineData(SoundKind.Tick, 0.08)]
    [InlineData(SoundKind.Chime, 0.5)]
    public void Wav_IsShortQuietPcm(SoundKind kind, double maxSeconds)
    {
        var wav = SoundSynth.Wav(kind);

        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal(1, BitConverter.ToInt16(wav, 20));          // PCM
        Assert.Equal(1, BitConverter.ToInt16(wav, 22));          // моно
        Assert.Equal(44100, BitConverter.ToInt32(wav, 24));
        Assert.Equal(16, BitConverter.ToInt16(wav, 34));
        Assert.Equal(wav.Length - 44, BitConverter.ToInt32(wav, 40));

        var samples = Samples(wav);
        Assert.InRange(samples.Length / 44100.0, 0.02, maxSeconds);
        Assert.InRange(samples.Max(s => Math.Abs((int)s)), 2000, 16384);  // слышно, но не громче половины шкалы
        // Плавные начало и конец — без щелчков
        Assert.All(samples.Take(32), s => Assert.True(Math.Abs((int)s) < 1000, $"начало {s}"));
        Assert.All(samples.TakeLast(32), s => Assert.True(Math.Abs((int)s) < 1000, $"конец {s}"));
    }

    [Fact]
    public void Wav_IsSameEveryTime() => Assert.Equal(SoundSynth.Wav(SoundKind.Shutter), SoundSynth.Wav(SoundKind.Shutter));
}
