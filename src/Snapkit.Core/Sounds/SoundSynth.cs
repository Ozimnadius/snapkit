namespace Snapkit.Core.Sounds;

public enum SoundKind { Shutter, Tick, Chime }

/// <summary>
/// Свои звуки программы, синтезированные в коде (раздел 12 описания: звуки Snapik не используются):
/// затвор при снимке, «тик», когда снимок лёг на ленту, мягкий сигнал после вставки.
/// </summary>
public static class SoundSynth
{
    public const int SampleRate = 44100;

    // Пик — около 45 % шкалы: звуки слышны, но не громче системных
    private const double Peak = 0.45;

    /// <summary>WAV (PCM 16 бит, моно, 44,1 кГц) звука.</summary>
    public static byte[] Wav(SoundKind kind) => Encode(kind switch
    {
        SoundKind.Shutter => Shutter(),
        SoundKind.Tick => Tick(),
        _ => Chime(),
    });

    // Затвор: два коротких шумовых щелчка (шторка открылась и закрылась) и низкий глухой удар
    private static double[] Shutter()
    {
        var samples = new double[Length(0.15)];
        var noise = new Random(7);
        var previous = 0.0;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (double)i / SampleRate;
            // Шум чуть приглушён простым фильтром — без резкого шипения
            previous = previous * 0.55 + (noise.NextDouble() * 2 - 1) * 0.45;
            var first = Math.Exp(-t / 0.012);
            var second = t >= 0.06 ? 0.6 * Math.Exp(-(t - 0.06) / 0.010) : 0;
            var thump = 0.5 * Math.Sin(2 * Math.PI * 140 * t) * Math.Exp(-t / 0.03);
            samples[i] = previous * (first + second) + thump;
        }
        return Shape(samples, fadeIn: 0.004, fadeOut: 0.01);
    }

    // «Тик»: тихий высокий тон с быстрым затуханием
    private static double[] Tick()
    {
        var samples = new double[Length(0.07)];
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (double)i / SampleRate;
            samples[i] = Math.Sin(2 * Math.PI * 1600 * t) * Math.Exp(-t / 0.018);
        }
        return Shape(samples, fadeIn: 0.003, fadeOut: 0.01, peak: Peak * 0.6);
    }

    // Сигнал после вставки: два мягких тона вверх, второй вступает чуть позже
    private static double[] Chime()
    {
        var samples = new double[Length(0.45)];
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (double)i / SampleRate;
            samples[i] = Bell(880, t) + (t >= 0.12 ? Bell(1320, t - 0.12) : 0);
        }
        return Shape(samples, fadeIn: 0.005, fadeOut: 0.03);
    }

    // Тон «колокольчика»: основной тон и тихий обертон, мягкое вступление и затухание
    private static double Bell(double frequency, double t)
    {
        var attack = Math.Min(1, t / 0.008);
        var decay = Math.Exp(-t / 0.12);
        return attack * decay * (Math.Sin(2 * Math.PI * frequency * t) + 0.2 * Math.Sin(2 * Math.PI * frequency * 2 * t));
    }

    private static int Length(double seconds) => (int)(seconds * SampleRate);

    // Нормировка к пику и плавные края (квадратичные), чтобы в начале и конце не было щелчков
    private static double[] Shape(double[] samples, double fadeIn, double fadeOut, double peak = Peak)
    {
        var inLength = Length(fadeIn);
        var outLength = Length(fadeOut);
        for (var i = 0; i < samples.Length; i++)
        {
            var gain = 1.0;
            if (i < inLength) gain *= Math.Pow((double)i / inLength, 2);
            var fromEnd = samples.Length - 1 - i;
            if (fromEnd < outLength) gain *= Math.Pow((double)fromEnd / outLength, 2);
            samples[i] *= gain;
        }
        var max = samples.Max(Math.Abs);
        if (max > 0) for (var i = 0; i < samples.Length; i++) samples[i] *= peak / max;
        return samples;
    }

    private static byte[] Encode(double[] samples)
    {
        var data = samples.Length * 2;
        var wav = new byte[44 + data];
        void Text(int offset, string text) => System.Text.Encoding.ASCII.GetBytes(text).CopyTo(wav, offset);
        void Int(int offset, int value) => BitConverter.TryWriteBytes(wav.AsSpan(offset), value);
        void Short(int offset, short value) => BitConverter.TryWriteBytes(wav.AsSpan(offset), value);
        Text(0, "RIFF");
        Int(4, 36 + data);
        Text(8, "WAVE");
        Text(12, "fmt ");
        Int(16, 16);                 // размер блока fmt
        Short(20, 1);                // PCM
        Short(22, 1);                // моно
        Int(24, SampleRate);
        Int(28, SampleRate * 2);     // байт в секунду
        Short(32, 2);                // байт на отсчёт
        Short(34, 16);               // бит на отсчёт
        Text(36, "data");
        Int(40, data);
        for (var i = 0; i < samples.Length; i++)
            Short(44 + i * 2, (short)Math.Round(Math.Clamp(samples[i], -1, 1) * short.MaxValue));
        return wav;
    }
}
