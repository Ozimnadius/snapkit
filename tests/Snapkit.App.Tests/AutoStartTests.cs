using System.IO;
using Snapkit.App.Shell;

namespace Snapkit.App.Tests;

public class AutoStartTests
{
    [Fact]
    public void IsInstalledCopy_TrueInsideInstallDirectory()
        => Assert.True(AutoStart.IsInstalledCopy(Path.Combine(AutoStart.InstallDirectory, "Snapkit.exe")));

    [Fact]
    public void IsInstalledCopy_FalseForDevelopmentBuild()
        => Assert.False(AutoStart.IsInstalledCopy(@"C:\OSPanel\home\snapkit\src\Snapkit.App\bin\Debug\Snapkit.exe"));

    [Fact]
    public void IsInstalledCopy_FalseForSiblingFolderWithSamePrefix()
        => Assert.False(AutoStart.IsInstalledCopy(AutoStart.InstallDirectory + "Old\\Snapkit.exe"));
}
