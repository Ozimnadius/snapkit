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

    /// <summary>Всё по разделу 2.10 описания, кроме папки программы — её удаляет RemoveProgramFolderAfterExit.</summary>
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
    }

    /// <summary>
    /// Папку программы удалить после выхода: работающий Snapkit.exe держит свой файл. Вызывать последним,
    /// прямо перед выходом, — отдельный скрытый процесс дождётся завершения этого и сотрёт папку.
    /// </summary>
    public static void RemoveProgramFolderAfterExit()
    {
        if (!Directory.Exists(AutoStart.InstallDirectory)) return;
        Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"{RemoveFolderCommand(Environment.ProcessId, AutoStart.InstallDirectory)}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetTempPath(),
        });
    }

    /// <summary>Команда PowerShell: дождаться выхода процесса и удалить папку.</summary>
    public static string RemoveFolderCommand(int processId, string directory)
        => $"Wait-Process -Id {processId} -ErrorAction SilentlyContinue; " +
           $"Remove-Item -LiteralPath '{directory.Replace("'", "''")}' -Recurse -Force -ErrorAction SilentlyContinue";

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
