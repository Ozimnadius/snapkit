using Snapkit.Core.Editing;
using Snapkit.Core.Model;
using static Snapkit.Core.Tests.TestData;

namespace Snapkit.Core.Tests;

public class AnnotationGeometryTests
{
    private static readonly Annotation Box = Shape(AnnotationKind.Rectangle, points: [new(10, 10), new(110, 60)]);

    [Theory]
    [InlineData(10, 30, true)]   // на левой стороне
    [InlineData(60, 35, false)]  // внутри — не попадание, чтобы можно было рисовать поверх
    [InlineData(113, 30, true)]  // рядом с правой стороной, в пределах допуска
    [InlineData(120, 30, false)] // дальше допуска
    public void HitTest_Rectangle_OnBorderNotInside(double x, double y, bool hit)
        => Assert.Equal(hit, AnnotationGeometry.HitTest(Box, new PointD(x, y), 4));

    [Fact]
    public void HitTest_Arrow_NearSegment()
    {
        var arrow = Shape(AnnotationKind.Arrow, points: [new(0, 0), new(100, 0)]);
        Assert.True(AnnotationGeometry.HitTest(arrow, new PointD(50, 3), 4));
        Assert.False(AnnotationGeometry.HitTest(arrow, new PointD(50, 10), 4));
        Assert.False(AnnotationGeometry.HitTest(arrow, new PointD(110, 0), 4)); // за концом отрезка
    }

    [Fact]
    public void HitTest_Pin_WithinRadius()
    {
        var pin = Shape(AnnotationKind.Pin, note: "x", points: [new(50, 50)]);
        Assert.True(AnnotationGeometry.HitTest(pin, new PointD(60, 50), 2));
        Assert.False(AnnotationGeometry.HitTest(pin, new PointD(70, 50), 2));
    }

    [Fact]
    public void HitTest_Text_InsideTextBox()
    {
        var text = Shape(AnnotationKind.Text, text: "Привет", points: [new(10, 10)]); // размер шрифта 20
        Assert.True(AnnotationGeometry.HitTest(text, new PointD(30, 20), 0));
        Assert.False(AnnotationGeometry.HitTest(text, new PointD(200, 20), 0));
    }

    [Fact]
    public void TextFontSize_GrowsWithThickness()
        => Assert.Equal(20, AnnotationGeometry.TextFontSize(4));

    [Fact]
    public void PinCenter_RectangleIsNormalizedTopLeft()
    {
        var box = Shape(AnnotationKind.Rectangle, points: [new(110, 60), new(10, 10)]);
        Assert.Equal(new PointD(10, 10), AnnotationGeometry.PinCenter(box));
    }

    [Fact]
    public void PinCenter_ArrowIsStart()
        => Assert.Equal(new PointD(5, 6), AnnotationGeometry.PinCenter(Shape(AnnotationKind.Arrow, points: [new(5, 6), new(50, 60)])));

    [Fact]
    public void Bounds_RectangleIsNormalized()
        => Assert.Equal((10d, 10d, 100d, 50d),
            AnnotationGeometry.Bounds(Shape(AnnotationKind.Rectangle, points: [new(110, 60), new(10, 10)])));

    [Fact]
    public void TopmostHit_PrefersLastAnnotation()
    {
        var below = Shape(AnnotationKind.Rectangle, points: [new(10, 10), new(110, 60)]);
        var above = Shape(AnnotationKind.Rectangle, points: [new(10, 10), new(80, 80)]);
        Assert.Equal(above.Id, AnnotationGeometry.TopmostHit([below, above], new PointD(10, 30), 4));
    }

    [Fact]
    public void TopmostHit_HitsPinCircle()
    {
        var noted = Shape(AnnotationKind.Rectangle, note: "заметка", points: [new(40, 40), new(140, 90)]);
        // Точка вне рамки и не на её контуре, но внутри кружка заметки (центр 40,40, радиус 13)
        Assert.Equal(noted.Id, AnnotationGeometry.TopmostHit([noted], new PointD(32, 32), 2));
    }

    [Fact]
    public void TopmostHit_NoNote_NoPinCircle()
    {
        var plain = Shape(AnnotationKind.Rectangle, points: [new(40, 40), new(140, 90)]);
        Assert.Null(AnnotationGeometry.TopmostHit([plain], new PointD(32, 32), 2));
    }

    [Fact]
    public void Translate_MovesAllPoints()
    {
        var moved = AnnotationGeometry.Translate(Box, 5, -3);
        Assert.Equal(new[] { new PointD(15, 7), new PointD(115, 57) }, moved.Points.ToArray());
        Assert.Equal(Box.Id, moved.Id);
    }
}
