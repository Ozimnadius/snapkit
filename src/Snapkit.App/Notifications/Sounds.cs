using System.IO;
using System.Media;
using Snapkit.App.Shell;
using Snapkit.Core.Sounds;

namespace Snapkit.App.Notifications;

/// <summary>
/// Звуки программы: затвор, «тик» ленты, сигнал после вставки. Играют в фоне и не задерживают
/// снимок или вставку; если звук не проигрался (нет устройства), это только запись в журнале.
/// </summary>
public static class Sounds
{
    private static readonly Dictionary<SoundKind, SoundPlayer> Players = [];

    /// <summary>Настройка «Звуки».</summary>
    public static bool Enabled { get; set; } = true;

    public static void Play(SoundKind kind)
    {
        if (!Enabled) return;
        try
        {
            if (!Players.TryGetValue(kind, out var player))
            {
                // Звук синтезируется один раз и дальше берётся готовым
                player = new SoundPlayer(new MemoryStream(SoundSynth.Wav(kind)));
                player.Load();
                Players[kind] = player;
            }
            player.Play();
        }
        catch (Exception ex)
        {
            Log.Error($"звук {kind}", ex);
        }
    }
}
