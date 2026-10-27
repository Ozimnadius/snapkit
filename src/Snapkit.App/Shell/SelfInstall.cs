using System.Diagnostics;
using System.IO;

namespace Snapkit.App.Shell;

public enum SelfInstallAction { RunHere, Install }

/// <summary>
/// Самоустановка exe из релиза: запущенный не из своей папки, он копирует себя
/// в %LOCALAPPDATA%\Programs\Snapkit, делает ярлык в «Пуске» и перезапускается оттуда.
/// Так же идёт обновление: новый exe закрывает работающую копию и заменяет её.
/// </summary>
public static class SelfInstall
{
    /// <summary>Аргумент, с которым установщик запускает установленную копию.</summary>
    public const string InstalledArgument = "--installed";

    /// <summary>Аргумент, с которым запускается установленная копия, если она заменила прежнюю (обновление).</summary>
    public const string UpdatedArgument = "--updated";

    private const string ExeName = "Snapkit.exe";
    private static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Ставит себя только однофайловый exe из релиза. Сборки для разработки (build.ps1, запуск
    /// из проекта) лежат папкой с библиотеками и работают там, где лежат.
    /// </summary>
    public static SelfInstallAction Decide(string exePath, string installDirectory, bool isSingleFile)
        => isSingleFile && !exePath.StartsWith(installDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? SelfInstallAction.Install
            : SelfInstallAction.RunHere;

    public static bool IsJustInstalled(IEnumerable<string> args) => args.Contains(InstalledArgument);

    public static bool IsJustUpdated(IEnumerable<string> args) => args.Contains(UpdatedArgument);

    /// <summary>Программа собрана одним файлом: у сборки нет своего пути на диске.</summary>
#pragma warning disable IL3000 // пустой Location — это и есть признак однофайловой сборки
    public static bool IsSingleFile => string.IsNullOrEmpty(typeof(SelfInstall).Assembly.Location);
#pragma warning restore IL3000

    /// <summary>Поставить exe в папку программы, сделать ярлык в «Пуске» и запустить установленную копию.</summary>
    public static void InstallAndLaunch(string exePath, string installDirectory)
    {
        Directory.CreateDirectory(installDirectory);
        var target = Path.Combine(installDirectory, ExeName);
        var updating = File.Exists(target);
        StopOtherCopies();
        CopyWithRetries(exePath, target);
        CreateStartMenuShortcut(target, installDirectory);
        Process.Start(new ProcessStartInfo(target, updating ? UpdatedArgument : InstalledArgument)
        {
            WorkingDirectory = installDirectory, UseShellExecute = false,
        });
    }

    // Работающий Snapkit держит exe и не дал бы запуститься новой копии — просим его закрыться
    internal static void StopOtherCopies()
    {
        var others = Process.GetProcessesByName("Snapkit").Where(p => p.Id != Environment.ProcessId).ToList();
        if (others.Count == 0) return;
        SingleInstance.RequestExit();
        foreach (var process in others)
        {
            if (process.WaitForExit(ExitWait)) continue;
            Log.Info($"Snapkit ({process.Id}) не закрылся за {ExitWait.TotalSeconds:0} с — завершаю");
            process.Kill();
            process.WaitForExit(ExitWait);
        }
    }

    // Windows отпускает файл закрывшейся программы не сразу
    private static void CopyWithRetries(string source, string target)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Copy(source, target, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 10)
            {
                Thread.Sleep(300);
            }
        }
    }

    private static void CreateStartMenuShortcut(string target, string workingDirectory)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Snapkit.lnk");
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Нет WScript.Shell");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(path);
        shortcut.TargetPath = target;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.Description = "Snapkit — снимки экрана с комментариями для ИИ";
        shortcut.Save();
    }
}
