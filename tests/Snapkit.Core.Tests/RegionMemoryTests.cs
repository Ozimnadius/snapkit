using Snapkit.Core.Session;

namespace Snapkit.Core.Tests;

public class RegionMemoryTests
{
    private static readonly RectI[] Monitors = [new(0, 0, 1920, 1080), new(1920, 0, 2560, 1440)];

    [Fact]
    public void FitOnScreen_InsideSecondMonitor_IsSame()
        => Assert.Equal(new RectI(2000, 100, 800, 600), RegionMemory.FitOnScreen(new RectI(2000, 100, 800, 600), Monitors));

    [Fact]
    public void FitOnScreen_AcrossMonitors_IsNull()
        => Assert.Null(RegionMemory.FitOnScreen(new RectI(1800, 100, 300, 200), Monitors));

    [Fact]
    public void FitOnScreen_OutsideAllMonitors_IsNull()
        => Assert.Null(RegionMemory.FitOnScreen(new RectI(5000, 100, 300, 200), Monitors));

    [Fact]
    public void FitOnScreen_NoMonitors_IsNull()
        => Assert.Null(RegionMemory.FitOnScreen(new RectI(0, 0, 10, 10), []));
}
