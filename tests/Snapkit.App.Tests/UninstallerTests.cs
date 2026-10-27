using System.IO;
using Snapkit.App.Shell;

namespace Snapkit.App.Tests;

public sealed class UninstallerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "snapkit-uninstall-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Make(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void DeleteData_RemovesEverything()
    {
        var data = Path.Combine(_root, "Snapkit");
        Make("Snapkit", "session.json");
        Make("Snapkit", "drag", "a", "Снимок A.png");

        Uninstaller.DeleteData([data], keep: Path.Combine(_root, "Pictures", "Snapkit"));

        Assert.False(Directory.Exists(data));
    }

    [Fact]
    public void DeleteData_KeepsSaveFolder()
    {
        // Папку сохранения выбрали внутри папки данных — снимки пользователя всё равно остаются
        var data = Path.Combine(_root, "Snapkit");
        var session = Make("Snapkit", "session.json");
        var drag = Make("Snapkit", "drag", "a", "Снимок A.png");
        var saved = Make("Snapkit", "Снимки", "2026-10-27_09-13-06.png");

        Uninstaller.DeleteData([data], keep: Path.Combine(_root, "Snapkit", "Снимки"));

        Assert.True(File.Exists(saved));
        Assert.False(File.Exists(session));
        Assert.False(File.Exists(drag));
    }

    [Fact]
    public void DeleteData_MissingDirectory_IsIgnored()
        => Uninstaller.DeleteData([Path.Combine(_root, "нет такой")], keep: "");

    [Fact]
    public void RemoveFolderCommand_WaitsForExitThenRemoves()
    {
        // Работающий Snapkit.exe держит свой файл — папку удаляем, только когда процесс завершился
        var command = Uninstaller.RemoveFolderCommand(1234, @"C:\Users\a'b\Programs\Snapkit");
        Assert.Contains("Wait-Process -Id 1234", command);
        Assert.Contains(@"Remove-Item -LiteralPath 'C:\Users\a''b\Programs\Snapkit' -Recurse -Force", command);
        Assert.True(command.IndexOf("Wait-Process", StringComparison.Ordinal) < command.IndexOf("Remove-Item", StringComparison.Ordinal));
    }
}
