using Snapkit.App.Shell;

namespace Snapkit.App.Tests;

public class SelfInstallTests
{
    private const string InstallDir = @"C:\Users\user\AppData\Local\Programs\Snapkit";

    [Fact]
    public void ReleaseExe_FromDownloads_InstallsItself()
        => Assert.Equal(SelfInstallAction.Install,
            SelfInstall.Decide(@"C:\Users\user\Downloads\Snapkit.exe", InstallDir, isSingleFile: true));

    [Fact]
    public void ReleaseExe_FromInstallFolder_RunsHere()
        => Assert.Equal(SelfInstallAction.RunHere,
            SelfInstall.Decide(@"c:\users\USER\appdata\local\programs\snapkit\Snapkit.exe", InstallDir, isSingleFile: true));

    [Fact]
    public void DevBuild_NeverInstallsItself()
    {
        // build.ps1 и запуск из проекта собирают программу папкой, а не одним файлом
        Assert.Equal(SelfInstallAction.RunHere,
            SelfInstall.Decide(@"C:\OSPanel\home\snapkit\src\Snapkit.App\bin\Debug\Snapkit.exe", InstallDir, isSingleFile: false));
    }

    [Fact]
    public void FolderNamedLikeInstallFolder_IsNotTheInstallFolder()
        => Assert.Equal(SelfInstallAction.Install,
            SelfInstall.Decide(@"C:\Users\user\AppData\Local\Programs\Snapkit-old\Snapkit.exe", InstallDir, isSingleFile: true));

    [Theory]
    [InlineData(new[] { "--installed" }, true)]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "--other" }, false)]
    public void JustInstalled_IsReadFromArguments(string[] args, bool expected)
        => Assert.Equal(expected, SelfInstall.IsJustInstalled(args));
}
