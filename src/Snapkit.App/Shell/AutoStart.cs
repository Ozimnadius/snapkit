using System.IO;
using Microsoft.Win32;

namespace Snapkit.App.Shell;

/// <summary>Запуск вместе с Windows — запись в HKCU\...\Run.</summary>
public static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Snapkit";

    /// <summary>Папка, куда ставит программу build.ps1.</summary>
    public static string InstallDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Snapkit");

    /// <summary>Запущена установленная копия, а не сборка из папки проекта.</summary>
    public static bool IsInstalledCopy(string exePath)
        => exePath.StartsWith(InstallDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public static void Apply(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled) key.SetValue(ValueName, $"\"{exePath}\"");
        else if (key.GetValue(ValueName) is not null) key.DeleteValue(ValueName);
    }
}
