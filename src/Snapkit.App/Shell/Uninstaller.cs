using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Snapkit.App.Shell;

/// <summary>
/// Удаление Snapkit (раздел 2.10 описания этапа 4): строка в «Параметры → Приложения» и само удаление —
/// программа, ярлык, автозапуск, лента, настройки и журнал. Сохранённые снимки в папке сохранения не трогаются.
/// Только профиль пользователя, без прав администратора.
/// </summary>
public static class Uninstaller
{
    public const string Argument = "--uninstall";

    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Snapkit";

    /// <summary>Строка в «Параметры → Приложения»: имя, версия, значок, издатель, команда удаления.</summary>
    public static void Register(string exePath, string version)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey, writable: true);
        key.SetValue("DisplayName", "Snapkit");
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "Ozimnadius");
        key.SetValue("DisplayIcon", $"\"{exePath}\",0");
        key.SetValue("UninstallString", $"\"{exePath}\" {Argument}");
        key.SetValue("InstallLocation", Path.GetDirectoryName(exePath) ?? "");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", (int)(new FileInfo(exePath).Length / 1024), RegistryValueKind.DWord);
    }

    /// <summary>Удалить содержимое папок данных, кроме keep и всего, что его содержит или лежит в нём.</summary>
    public static void DeleteData(IEnumerable<string> directories, string keep)
    {
        var kept = keep.Length > 0 ? Normalize(keep) : null;
        foreach (var directory in directories) Delete(Normalize(directory), kept);
    }

    /// <summary>Всё по разделу 2.10 описания; папку программы удаляет отложенная команда после выхода.</summary>
    public static void Run(string saveFolder)
    {
        AutoStart.Apply(enabled: false, exePath: "");
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);
        var shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Snapkit.lnk");
        if (File.Exists(shortcut)) File.Delete(shortcut);
        DeleteData(
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Snapkit"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Snapkit"),
        ], saveFolder);
        RemoveProgramFolderLater(AutoStart.InstallDirectory);
    }

    // Работающий exe сам себя не удалит — это сделает cmd через пару секунд после выхода
    private static void RemoveProgramFolderLater(string directory)
    {
        if (!Directory.Exists(directory)) return;
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{directory}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetTempPath(),
        });
    }

    private static void Delete(string directory, string? keep)
    {
        if (!Directory.Exists(directory)) return;
        if (keep is not null && IsSameOrInside(directory, keep)) return;
        if (keep is null || !IsSameOrInside(keep, directory))
        {
            Directory.Delete(directory, recursive: true);
            return;
        }
        // Папка сохранения внутри — удаляем всё вокруг неё
        foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
        foreach (var child in Directory.GetDirectories(directory)) Delete(child, keep);
    }

    private static bool IsSameOrInside(string path, string root)
        => path.Equals(root, StringComparison.OrdinalIgnoreCase)
           || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
}
