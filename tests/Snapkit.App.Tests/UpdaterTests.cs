using Snapkit.App.Shell;

namespace Snapkit.App.Tests;

public class UpdaterTests
{
    [Fact]
    public void CurrentVersion_IsAppVersion()
    {
        // В 0.4.0 HTTP-клиент создавался раньше номера версии: заголовок «Snapkit/» без версии ронял
        // весь Updater — проверка обновлений всегда «не удавалась», а окно настроек не открывалось
        var assembly = typeof(Updater).Assembly.GetName().Version!;
        Assert.Equal(new Version(assembly.Major, assembly.Minor, assembly.Build), Updater.CurrentVersion);
    }
}
