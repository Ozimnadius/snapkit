using Snapkit.Core.Editing;
using Snapkit.Core.Model;
using static Snapkit.Core.Tests.TestData;

namespace Snapkit.Core.Tests;

public class AnnotationStyleTests
{
    private static readonly Annotation BlueFilled = Shape(AnnotationKind.Rectangle) with { Fill = FillMode.Solid, FillColor = "#1E88E5" };

    [Fact]
    public void WithFill_KeepsShapeFillColor()
    {
        // У фигуры синяя заливка, а в редакторе цвет заливки не выбран: смена вида не должна сбросить синий
        var changed = AnnotationStyle.WithFill(BlueFilled, FillMode.SemiTransparent);
        Assert.Equal((FillMode.SemiTransparent, "#1E88E5"), (changed.Fill, changed.FillColor));
    }

    [Fact]
    public void WithFillColor_KeepsShapeFillMode()
    {
        var changed = AnnotationStyle.WithFillColor(BlueFilled with { Fill = FillMode.SemiTransparent }, "#43A047");
        Assert.Equal((FillMode.SemiTransparent, "#43A047"), (changed.Fill, changed.FillColor));
    }

    [Fact]
    public void WithFillColor_WithoutFill_TurnsOnSolid()
    {
        var changed = AnnotationStyle.WithFillColor(Shape(AnnotationKind.Ellipse), "#43A047");
        Assert.Equal((FillMode.Solid, "#43A047"), (changed.Fill, changed.FillColor));
    }

    [Fact]
    public void WithFillColor_Reset_KeepsFillMode()
    {
        var changed = AnnotationStyle.WithFillColor(BlueFilled, null);
        Assert.Equal((FillMode.Solid, (string?)null), (changed.Fill, changed.FillColor));
    }

    [Fact]
    public void WithFill_Arrow_IsUnchanged()
    {
        var arrow = Shape(AnnotationKind.Arrow);
        Assert.Same(arrow, AnnotationStyle.WithFill(arrow, FillMode.Solid));
        Assert.Same(arrow, AnnotationStyle.WithFillColor(arrow, "#43A047"));
    }
}
