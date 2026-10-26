using System.IO;
using System.Runtime.InteropServices;
using Snapkit.App.Shell;
using Snapkit.Core.Sounds;

namespace Snapkit.App.Notifications;

/// <summary>
/// Звуки программы: затвор, «тик» ленты, сигнал после вставки. Играют в фоне и не задерживают
/// снимок или вставку; если звук не проигрался (нет устройства), это только запись в журнале.
/// Звук играет из файла в папке программы: при игре из памяти Windows читает массив во время
/// звучания, а сборщик мусора может его переместить — вместо сигнала был бы шум.
/// </summary>
public static partial class Sounds
{
    private const uint SND_ASYNC = 0x0001;
    private const uint SND_NODEFAULT = 0x0002;
    private const uint SND_FILENAME = 0x00020000;

    private static readonly Dictionary<SoundKind, string> Files = [];

    /// <summary>Настройка «Звуки».</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Папка, куда записываются файлы звуков (задаёт App при запуске).</summary>
    public static string Directory { get; set; } = Path.Combine(Path.GetTempPath(), "Snapkit", "sounds");

    public static void Play(SoundKind kind)
    {
        if (!Enabled) return;
        try
        {
            if (!Files.TryGetValue(kind, out var path))
            {
                path = EnsureFile(Directory, kind);
                Files[kind] = path;
            }
            PlaySound(path, IntPtr.Zero, SND_FILENAME | SND_ASYNC | SND_NODEFAULT);
        }
        catch (Exception ex)
        {
            Log.Error($"звук {kind}", ex);
        }
    }

    /// <summary>WAV звука в папке directory: записывается, если его нет или он отличается; возвращает путь.</summary>
    public static string EnsureFile(string directory, SoundKind kind)
    {
        var wav = SoundSynth.Wav(kind);
        var path = Path.Combine(directory, $"{kind}.wav");
        if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(wav)) return path;
        System.IO.Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, wav);
        return path;
    }

    [LibraryImport("winmm.dll", EntryPoint = "PlaySoundW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PlaySound(string sound, IntPtr module, uint flags);
}
