using Snapkit.Core.Session;

namespace Snapkit.Core.Tests;

public class CursorPlacementTests
{
    [Fact]
    public void TopLeft_SubtractsHotspotAndFrameCorner()
        => Assert.Equal((96, 98), CursorPlacement.TopLeft((500, 300), (4, 2), new RectI(400, 200, 800, 600)));

    [Fact]
    public void TopLeft_MonitorLeftOfPrimary()
        // Второй монитор слева от основного: у кадра отрицательный X
        => Assert.Equal((920, 500), CursorPlacement.TopLeft((-1000, 500), (0, 0), new RectI(-1920, 0, 1920, 1080)));
}
