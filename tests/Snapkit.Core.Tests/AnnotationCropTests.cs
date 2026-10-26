using System.Collections.Immutable;
using Snapkit.Core.Editing;
using Snapkit.Core.Model;
using static Snapkit.Core.Tests.TestData;

namespace Snapkit.Core.Tests;

public class AnnotationCropTests
{
    private const double R = AnnotationGeometry.PinDiameter / 2;

    [Fact]
    public void Crop_ShiftsShapes()
    {
        var box = Shape(AnnotationKind.Rectangle, points: [new(50, 60), new(90, 100)]);
        var cropped = Assert.Single(AnnotationGeometry.Crop([box], new RectI(20, 30, 200, 200)));
        Assert.Equal([new(30, 30), new(70, 70)], cropped.Points.ToArray());
    }

    [Fact]
    public void Crop_PinOutside_MovesInside()
    {
        var box = Shape(AnnotationKind.Rectangle, note: "рамка", points: [new(5, 5), new(60, 60)]);
        var center = AnnotationGeometry.PinCenter(Assert.Single(AnnotationGeometry.Crop([box], new RectI(20, 20, 100, 100))));
        Assert.InRange(center.X, R, 100 - R);
        Assert.InRange(center.Y, R, 100 - R);
    }

    [Fact]
    public void Crop_PinInside_IsUntouched()
    {
        var box = Shape(AnnotationKind.Rectangle, note: "рамка", points: [new(40, 40), new(80, 80)]);
        Assert.Null(Assert.Single(AnnotationGeometry.Crop([box], new RectI(20, 20, 100, 100))).PinOffset);
    }

    [Fact]
    public void Crop_KeepsShapesPartlyOutside()
    {
        ImmutableList<Annotation> items =
        [
            Shape(AnnotationKind.Rectangle, points: [new(0, 0), new(30, 30)]),
            Shape(AnnotationKind.Line, points: [new(-50, -50), new(500, 500)]),
            Shape(AnnotationKind.Arrow, points: [new(300, 300), new(400, 400)]),
        ];
        Assert.Equal(3, AnnotationGeometry.Crop(items, new RectI(20, 20, 100, 100)).Count);
    }

    [Fact]
    public void KeepPinsInside_ShapeWithoutNote_IsUntouched()
    {
        var box = Shape(AnnotationKind.Rectangle, points: [new(-30, -30), new(60, 60)]);
        Assert.Same(box, Assert.Single(AnnotationGeometry.KeepPinsInside([box], 100, 100)));
    }

    [Fact]
    public void KeepPinsInside_StandalonePin_MovesInside()
    {
        var pin = Shape(AnnotationKind.Pin, note: "точка", points: [new(150, -5)]);
        var center = AnnotationGeometry.PinCenter(Assert.Single(AnnotationGeometry.KeepPinsInside([pin], 100, 80)));
        Assert.Equal(new PointD(100 - R, R), center);
    }

    [Fact]
    public void KeepPinsInside_TinyImage_PutsPinInCenter()
    {
        var pin = Shape(AnnotationKind.Pin, note: "точка", points: [new(-40, 3)]);
        var center = AnnotationGeometry.PinCenter(Assert.Single(AnnotationGeometry.KeepPinsInside([pin], 10, 6)));
        Assert.Equal(new PointD(5, 3), center);
    }
}
