using Snapkit.Core.Editing;
using Snapkit.Core.Model;
using static Snapkit.Core.Tests.TestData;

namespace Snapkit.Core.Tests;

public class AnnotationHandlesTests
{
    private static readonly Annotation Box = Shape(AnnotationKind.Rectangle, points: [new(10, 20), new(110, 70)]);

    [Fact]
    public void Of_Rectangle_IsCornersAndMidpointsClockwise()
        => Assert.Equal(
            [new(10, 20), new(60, 20), new(110, 20), new(110, 45), new(110, 70), new(60, 70), new(10, 70), new(10, 45)],
            AnnotationHandles.Of(Box));

    [Fact]
    public void Move_SouthEast_ResizesRectangle()
        => Assert.Equal((10d, 20d, 140d, 80d), AnnotationGeometry.Bounds(AnnotationHandles.Move(Box, 4, new PointD(150, 100))));

    [Fact]
    public void Move_North_ChangesOnlyTop()
        => Assert.Equal((10d, 5d, 100d, 65d), AnnotationGeometry.Bounds(AnnotationHandles.Move(Box, 1, new PointD(999, 5))));

    [Fact]
    public void Move_NorthWestPastOpposite_KeepsPositiveSize()
    {
        var moved = AnnotationHandles.Move(Box, 0, new PointD(200, 100));
        Assert.Equal((110d, 70d, 90d, 30d), AnnotationGeometry.Bounds(moved));
        // Углы нормализованы: первый — левый верхний, поэтому кружок остаётся в левом верхнем углу
        Assert.Equal(new PointD(110, 70), moved.Points[0]);
    }

    [Fact]
    public void Move_ArrowEnd_MovesOnlyEnd()
    {
        var arrow = Shape(AnnotationKind.Arrow, points: [new(0, 0), new(50, 50)]);
        Assert.Equal([new(0, 0), new(80, 10)], AnnotationHandles.Move(arrow, 1, new PointD(80, 10)).Points.ToArray());
    }

    [Fact]
    public void Move_CurvedArrowBend_MovesOnlyBend()
    {
        var curved = Shape(AnnotationKind.CurvedArrow, points: [new(0, 0), new(50, 40), new(100, 0)]);
        Assert.Equal([new(0, 0), new(50, -30), new(100, 0)], AnnotationHandles.Move(curved, 1, new PointD(50, -30)).Points.ToArray());
    }

    [Theory]
    [InlineData(AnnotationKind.Pencil)]
    [InlineData(AnnotationKind.Marker)]
    [InlineData(AnnotationKind.Text)]
    [InlineData(AnnotationKind.Pin)]
    public void Of_StrokesTextPin_AreEmpty(AnnotationKind kind)
        => Assert.Empty(AnnotationHandles.Of(Shape(kind, text: "т", points: [new(0, 0), new(10, 10)])));

    [Fact]
    public void Hit_FindsHandleWithinRadius()
    {
        Assert.Equal(4, AnnotationHandles.Hit(Box, new PointD(113, 72), 6));
        Assert.Null(AnnotationHandles.Hit(Box, new PointD(60, 45), 6));
    }

    [Fact]
    public void WithPinAt_PinCenterEqualsTarget()
        => Assert.Equal(new PointD(40, 33), AnnotationGeometry.PinCenter(AnnotationHandles.WithPinAt(Box, new PointD(40, 33))));

    [Fact]
    public void WithPinAt_DoesNotMoveShape()
        => Assert.Equal(Box.Points.ToArray(), AnnotationHandles.WithPinAt(Box, new PointD(40, 33)).Points.ToArray());

    [Fact]
    public void WithPinAt_TwiceKeepsTarget()
    {
        var once = AnnotationHandles.WithPinAt(Box, new PointD(40, 33));
        Assert.Equal(new PointD(70, 60), AnnotationGeometry.PinCenter(AnnotationHandles.WithPinAt(once, new PointD(70, 60))));
    }

    [Fact]
    public void Translate_KeepsPinOffset()
    {
        var shifted = AnnotationGeometry.Translate(Box with { PinOffset = new PointD(5, 5) }, 100, 0);
        Assert.Equal(new PointD(115, 25), AnnotationGeometry.PinCenter(shifted));
    }

    [Fact]
    public void HitPin_WithoutNote_IsFalse()
        => Assert.False(AnnotationHandles.HitPin(Box, new PointD(10, 20), 4));

    [Fact]
    public void HitPin_WithNote_OnCircle_IsTrue()
        => Assert.True(AnnotationHandles.HitPin(Box with { Note = "рамка" }, new PointD(15, 25), 4));

    [Fact]
    public void PickForSelect_PinOnSelectedCorner_TakesPin()
    {
        // Кружок рамки стоит в её левом верхнем углу — там же маркер размера 0
        var noted = Box with { Note = "заметка" };
        var target = AnnotationHandles.PickForSelect([noted], noted.Id, new PointD(10, 20), 8, 4);
        Assert.Equal(new SelectTarget(SelectTargetKind.Pin, noted.Id), target);
    }

    [Fact]
    public void PickForSelect_CornerWithoutNote_TakesHandle()
        => Assert.Equal(new SelectTarget(SelectTargetKind.Handle, Box.Id, 0),
            AnnotationHandles.PickForSelect([Box], Box.Id, new PointD(10, 20), 8, 4));

    [Fact]
    public void PickForSelect_OppositeCornerOfNotedBox_TakesHandle()
    {
        var noted = Box with { Note = "заметка" };
        Assert.Equal(new SelectTarget(SelectTargetKind.Handle, noted.Id, 4),
            AnnotationHandles.PickForSelect([noted], noted.Id, new PointD(110, 70), 8, 4));
    }

    [Fact]
    public void PickForSelect_OnOutline_TakesShape()
        => Assert.Equal(new SelectTarget(SelectTargetKind.Shape, Box.Id),
            AnnotationHandles.PickForSelect([Box], null, new PointD(60, 70), 8, 4));

    [Fact]
    public void PickForSelect_Empty_ReturnsNull()
        => Assert.Null(AnnotationHandles.PickForSelect([Box], null, new PointD(300, 300), 8, 4));
}
