using Snapkit.Core.Editing;
using Snapkit.Core.Model;
using static Snapkit.Core.Tests.TestData;

namespace Snapkit.Core.Tests;

public class AnnotationGeometryTests
{
    private static readonly Annotation Box = Shape(AnnotationKind.Rectangle, points: [new(10, 10), new(110, 60)]);

    [Theory]
    [InlineData(AnnotationKind.CurvedArrow, 3)]
    [InlineData(AnnotationKind.Text, 1)]
    [InlineData(AnnotationKind.Pin, 1)]
    [InlineData(AnnotationKind.Rectangle, 2)]
    [InlineData(AnnotationKind.Ellipse, 2)]
    [InlineData(AnnotationKind.Line, 2)]
    [InlineData(AnnotationKind.Pencil, 2)]
    [InlineData(AnnotationKind.Marker, 2)]
    [InlineData(AnnotationKind.Blur, 2)]
    [InlineData(AnnotationKind.WideArrow, 2)]
    public void MinPoints_PerKind(AnnotationKind kind, int expected)
        => Assert.Equal(expected, AnnotationGeometry.MinPoints(kind));

    [Fact]
    public void IsWellFormed_CurvedArrowWithTwoPoints_IsFalse()
        => Assert.False(AnnotationGeometry.IsWellFormed(Shape(AnnotationKind.CurvedArrow, points: [new(0, 0), new(10, 10)])));

    [Fact]
    public void IsWellFormed_NaNPoint_IsFalse()
        => Assert.False(AnnotationGeometry.IsWellFormed(Shape(AnnotationKind.Line, points: [new(0, 0), new(double.NaN, 10)])));

    [Fact]
    public void IsWellFormed_ZeroThickness_IsFalse()
        => Assert.False(AnnotationGeometry.IsWellFormed(Shape(AnnotationKind.Line) with { Thickness = 0 }));

    [Fact]
    public void IsWellFormed_PencilStroke_IsTrue()
        => Assert.True(AnnotationGeometry.IsWellFormed(Shape(AnnotationKind.Pencil, points: [new(0, 0), new(3, 4), new(8, 9)])));

    // --- Этап 2: новые фигуры ---

    private static readonly Annotation Oval = Shape(AnnotationKind.Ellipse, points: [new(0, 0), new(100, 60)]);
    private static readonly Annotation Curved = Shape(AnnotationKind.CurvedArrow, points: [new(0, 0), new(50, 40), new(100, 0)]);

    [Fact]
    public void Ellipse_HitOnOutline_NotInside_WhenNotFilled()
    {
        Assert.True(AnnotationGeometry.HitTest(Oval, new PointD(50, 0), 4));
        Assert.False(AnnotationGeometry.HitTest(Oval, new PointD(50, 30), 4));
    }

    [Fact]
    public void Ellipse_HitInside_WhenFilled()
        => Assert.True(AnnotationGeometry.HitTest(Oval with { Fill = FillMode.Solid }, new PointD(50, 30), 4));

    [Fact]
    public void Rectangle_SemiTransparentFill_HitInside()
        => Assert.True(AnnotationGeometry.HitTest(Box with { Fill = FillMode.SemiTransparent }, new PointD(60, 35), 4));

    [Fact]
    public void Blur_HitInside()
        => Assert.True(AnnotationGeometry.HitTest(Shape(AnnotationKind.Blur, points: [new(10, 10), new(110, 60)]), new PointD(60, 35), 4));

    [Fact]
    public void CurvedArrow_PassesThroughBendPoint()
    {
        var middle = AnnotationGeometry.CurvePoints(Curved, 24)[12];
        Assert.Equal(50, middle.X, 0.01);
        Assert.Equal(40, middle.Y, 0.01);
        Assert.Equal(25, AnnotationGeometry.CurvePoints(Curved, 24).Count);
    }

    [Fact]
    public void CurvedArrow_HitNearCurve_NotOnChord()
    {
        Assert.True(AnnotationGeometry.HitTest(Curved, new PointD(50, 40), 4));
        Assert.False(AnnotationGeometry.HitTest(Curved, new PointD(50, 0), 4));
    }

    [Fact]
    public void Bounds_CurvedArrow_CoversWholeCurve()
    {
        // Изгиб у начала: кривая заходит левее начальной точки (до x ≈ −5,6), хотя все три точки правее нуля
        var skewed = Shape(AnnotationKind.CurvedArrow, points: [new(0, 0), new(10, 40), new(100, 0)]);
        var (x, _, _, _) = AnnotationGeometry.Bounds(skewed);
        Assert.True(x <= -5, $"левая граница {x}");
    }

    [Fact]
    public void Pencil_HitNearPolyline_NotFarAway()
    {
        var stroke = Shape(AnnotationKind.Pencil, points: [new(0, 0), new(50, 0), new(50, 50)]);
        Assert.True(AnnotationGeometry.HitTest(stroke, new PointD(52, 25), 4));
        Assert.False(AnnotationGeometry.HitTest(stroke, new PointD(25, 25), 4));
    }

    [Fact]
    public void Marker_HitWithinMarkerWidth()
    {
        // Толщина 4 → маркер шириной 16: вместе с допуском попадание ближе 12 px к линии
        var marker = Shape(AnnotationKind.Marker, points: [new(0, 0), new(100, 0)]);
        Assert.True(AnnotationGeometry.HitTest(marker, new PointD(50, 7), 4));
        Assert.False(AnnotationGeometry.HitTest(marker, new PointD(50, 15), 4));
    }

    [Fact]
    public void PinCenter_Ellipse_IsTopLeftOfBounds()
        => Assert.Equal(new PointD(0, 0), AnnotationGeometry.PinCenter(Shape(AnnotationKind.Ellipse, points: [new(100, 60), new(0, 0)])));

    [Fact]
    public void PinCenter_AppliesPinOffset()
        => Assert.Equal(new PointD(15, 7), AnnotationGeometry.PinCenter(Box with { PinOffset = new PointD(5, -3) }));

    [Fact]
    public void DefaultBend_IsOffMidpointBy20PercentOfLength()
    {
        var bend = AnnotationGeometry.DefaultBend(new PointD(0, 0), new PointD(100, 0));
        Assert.Equal(50, bend.X, 0.001);
        Assert.Equal(20, Math.Abs(bend.Y), 0.001);
    }

    [Fact]
    public void IsBigEnough_PencilWithOnePoint_IsFalse()
        => Assert.False(AnnotationGeometry.IsBigEnough(Shape(AnnotationKind.Pencil, points: [new(5, 5)]), 4));

    [Fact]
    public void IsBigEnough_FlatEllipse_IsFalse()
        => Assert.False(AnnotationGeometry.IsBigEnough(Shape(AnnotationKind.Ellipse, points: [new(0, 0), new(50, 2)]), 4));

    [Fact]
    public void IsBigEnough_LongLine_IsTrue()
        => Assert.True(AnnotationGeometry.IsBigEnough(Shape(AnnotationKind.Line, points: [new(0, 0), new(3, 4)]), 4));

    [Fact]
    public void HitTest_PinWithOffset_UsesShiftedCircle()
    {
        // Кружок, сдвинутый внутрь снимка, выбирается там, где нарисован
        var pin = Shape(AnnotationKind.Pin, note: "точка", points: [new(0, 0)]) with { PinOffset = new PointD(40, 0) };
        Assert.True(AnnotationGeometry.HitTest(pin, new PointD(40, 0), 4));
        Assert.False(AnnotationGeometry.HitTest(pin, new PointD(0, 0), 4));
    }

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
    public void ShiftForRegionChange_KeepsShapesInPlaceOnScreen()
    {
        // Левая граница сдвинулась вправо на 30, верхняя вверх на 10 — фигуры смещаются обратно
        var moved = AnnotationGeometry.ShiftForRegionChange([Box], new RectI(100, 100, 300, 200), new RectI(130, 90, 270, 210));
        Assert.Equal(new[] { new PointD(-20, 20), new PointD(80, 70) }, moved[0].Points.ToArray());
    }

    [Fact]
    public void ShiftForRegionChange_SameOrigin_ReturnsSameList()
    {
        var list = System.Collections.Immutable.ImmutableList.Create(Box);
        Assert.Same(list, AnnotationGeometry.ShiftForRegionChange(list, new RectI(0, 0, 10, 10), new RectI(0, 0, 50, 50)));
    }

    [Fact]
    public void Translate_MovesAllPoints()
    {
        var moved = AnnotationGeometry.Translate(Box, 5, -3);
        Assert.Equal(new[] { new PointD(15, 7), new PointD(115, 57) }, moved.Points.ToArray());
        Assert.Equal(Box.Id, moved.Id);
    }
}
