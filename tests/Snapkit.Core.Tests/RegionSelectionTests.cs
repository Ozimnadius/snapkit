using Snapkit.Core.Editing;

namespace Snapkit.Core.Tests;

public class RegionSelectionTests
{
    private static readonly RectI Monitor = new(0, 0, 1000, 800);

    private static RegionSelection Selected(RectI region)
    {
        var s = new RegionSelection(Monitor);
        s.Begin(new PointD(region.X, region.Y));
        s.Drag(new PointD(region.Right, region.Bottom));
        s.End();
        return s;
    }

    [Fact]
    public void Drag_CreatesNormalizedRegion()
    {
        var s = new RegionSelection(Monitor);
        s.Begin(new PointD(300, 200));
        s.Drag(new PointD(100, 50));
        Assert.Equal(new RectI(100, 50, 200, 150), s.Region);
        Assert.True(s.IsDragging);
        s.End();
        Assert.False(s.IsDragging);
        Assert.Equal(new RectI(100, 50, 200, 150), s.Region);
    }

    [Fact]
    public void Drag_IsClampedToMonitor()
    {
        var s = new RegionSelection(Monitor);
        s.Begin(new PointD(900, 700));
        s.Drag(new PointD(1500, 1200));
        Assert.Equal(new RectI(900, 700, 100, 100), s.Region);
    }

    [Fact]
    public void TinySelection_IsDiscardedOnEnd()
    {
        var s = new RegionSelection(Monitor);
        s.Begin(new PointD(10, 10));
        s.Drag(new PointD(12, 40));
        s.End();
        Assert.Null(s.Region);
    }

    [Fact]
    public void HitHandle_FindsCornersEdgesAndInside()
    {
        var s = Selected(new RectI(100, 100, 200, 100));
        Assert.Equal(SelectionHandle.NorthWest, s.HitHandle(new PointD(102, 99), 6));
        Assert.Equal(SelectionHandle.SouthEast, s.HitHandle(new PointD(300, 200), 6));
        Assert.Equal(SelectionHandle.East, s.HitHandle(new PointD(301, 150), 6));
        Assert.Equal(SelectionHandle.North, s.HitHandle(new PointD(200, 100), 6));
        Assert.Equal(SelectionHandle.Move, s.HitHandle(new PointD(200, 150), 6));
        Assert.Equal(SelectionHandle.None, s.HitHandle(new PointD(500, 500), 6));
    }

    [Fact]
    public void ResizeSouthEast_MovesOnlyThatCorner()
    {
        var s = Selected(new RectI(100, 100, 200, 100));
        s.Begin(new PointD(300, 200), SelectionHandle.SouthEast);
        s.Drag(new PointD(350, 260));
        s.End();
        Assert.Equal(new RectI(100, 100, 250, 160), s.Region);
    }

    [Fact]
    public void ResizeWest_PastEastEdge_FlipsAndStaysNormalized()
    {
        var s = Selected(new RectI(100, 100, 200, 100));
        s.Begin(new PointD(100, 150), SelectionHandle.West);
        s.Drag(new PointD(400, 150));
        Assert.Equal(new RectI(300, 100, 100, 100), s.Region);
    }

    [Fact]
    public void Move_ShiftsRegionAndStaysInsideMonitor()
    {
        var s = Selected(new RectI(100, 100, 200, 100));
        s.Begin(new PointD(150, 150), SelectionHandle.Move);
        s.Drag(new PointD(170, 130));
        Assert.Equal(new RectI(120, 80, 200, 100), s.Region);
        s.Drag(new PointD(2000, 2000));
        Assert.Equal(new RectI(800, 700, 200, 100), s.Region);
    }

    [Fact]
    public void BeginOutside_StartsNewSelection()
    {
        var s = Selected(new RectI(100, 100, 200, 100));
        s.Begin(new PointD(500, 500));
        s.Drag(new PointD(600, 560));
        Assert.Equal(new RectI(500, 500, 100, 60), s.Region);
    }

    [Fact]
    public void SelectAll_CoversMonitor()
    {
        var s = new RegionSelection(Monitor);
        s.SelectAll();
        Assert.Equal(Monitor with { X = 0, Y = 0 }, s.Region);
    }
}
