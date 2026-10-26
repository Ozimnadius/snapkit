using System.IO;

namespace Snapkit.App.Shell;

/// <summary>Журнал ошибок в %LOCALAPPDATA%\Snapkit\log.txt — чтобы разбирать сбои после.</summary>
public static class Log
{
    private static readonly object Gate = new();

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Snapkit", "log.txt");

    /// <summary>Тесты выключают журнал, чтобы не засорять настоящий журнал программы.</summary>
    public static bool Enabled { get; set; } = true;

    public static void Error(string context, Exception ex) => Write($"ОШИБКА {context}: {ex}");

    public static void Info(string message) => Write(message);

    private static void Write(string line)
    {
        if (!Enabled) return;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                // Журнал не растёт бесконечно: больше 1 МБ — начинаем заново
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 1_000_000) File.Delete(FilePath);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}");
            }
        }
        catch
        {
            // Журнал не должен ронять программу
        }
    }
}
