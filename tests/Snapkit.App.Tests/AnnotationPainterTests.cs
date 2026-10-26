using System.Collections.Immutable;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.App.Rendering;
using Snapkit.Core;
using Snapkit.Core.Model;

namespace Snapkit.App.Tests;

public class AnnotationPainterTests
{
    private const byte Background = 200;

    private static Annotation Shape(AnnotationKind kind, params PointD[] points)
        => new(Guid.NewGuid(), kind, [.. points], "#E53935", 4, null, null);

    // Серый фон 200×100 и фигуры поверх — так же, как их рисует итоговая картинка
    private static BitmapSource Render(BlurSource? source, params Annotation[] annotations)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(Background, Background, Background)), null, new Rect(0, 0, 200, 100));
            AnnotationPainter.DrawAll(dc, annotations, 'A', source);
        }
        var target = new RenderTargetBitmap(200, 100, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        return target;
    }

    private static (byte B, byte G, byte R) PixelAt(BitmapSource image, int x, int y)
    {
        var px = new byte[4];
        image.CopyPixels(new Int32Rect(x, y, 1, 1), px, 4, 0);
        return (px[0], px[1], px[2]);
    }

    private static bool IsBackground((byte B, byte G, byte R) p) => p == (Background, Background, Background);

    [Fact]
    public void Ellipse_SolidFill_FillsCenter() => StaRunner.Run(() =>
    {
        var oval = Shape(AnnotationKind.Ellipse, new(40, 20), new(160, 80)) with { Fill = FillMode.Solid };
        // Без своего цвета заливки — цвет обводки #E53935
        Assert.Equal((0x35, 0x39, 0xE5), PixelAt(Render(null, oval), 100, 50));
    });

    [Fact]
    public void SemiTransparentFill_MixesWithBackground() => StaRunner.Run(() =>
    {
        var box = Shape(AnnotationKind.Rectangle, new(40, 20), new(160, 80)) with { Fill = FillMode.SemiTransparent, FillColor = "#1E88E5" };
        var (_, g, r) = PixelAt(Render(null, box), 100, 50);
        Assert.InRange(r, 0x1E + 20, Background - 20);   // красный канал между заливкой (0x1E) и фоном
        Assert.InRange(g, 0x88 + 20, Background - 5);
    });

    [Fact]
    public void Marker_IsTranslucent() => StaRunner.Run(() =>
    {
        var (_, g, _) = PixelAt(Render(null, Shape(AnnotationKind.Marker, new(20, 50), new(180, 50))), 100, 50);
        // Сплошной красный дал бы G = 0x39, фон — 200: маркер между ними
        Assert.InRange(g, 0x39 + 30, Background - 20);
    });

    [Fact]
    public void DashedRectangle_HasGapsOnEdge() => StaRunner.Run(() =>
    {
        var box = Shape(AnnotationKind.Rectangle, new(20, 20), new(180, 80)) with { LineStyle = LineStyle.Dashed };
        var image = Render(null, box);
        var edge = Enumerable.Range(30, 140).Select(x => PixelAt(image, x, 20)).ToList();
        Assert.Contains(edge, IsBackground);
        Assert.Contains(edge, p => p == (0x35, 0x39, 0xE5));
    });

    [Fact]
    public void SolidRectangle_HasNoGapsOnEdge() => StaRunner.Run(() =>
    {
        var image = Render(null, Shape(AnnotationKind.Rectangle, new(20, 20), new(180, 80)));
        Assert.DoesNotContain(Enumerable.Range(30, 140).Select(x => PixelAt(image, x, 20)), IsBackground);
    });

    [Fact]
    public void CurvedArrow_DrawsThroughBend() => StaRunner.Run(() =>
    {
        var curved = Shape(AnnotationKind.CurvedArrow, new(20, 80), new(100, 20), new(180, 80));
        var (b, g, r) = PixelAt(Render(null, curved), 100, 20);
        Assert.True(r > g + 60 && r > b + 60, $"в точке изгиба ({r},{g},{b})");
    });

    [Fact]
    public void WideArrow_IsWiderNearHead() => StaRunner.Run(() =>
    {
        var image = Render(null, Shape(AnnotationKind.WideArrow, new(20, 50), new(180, 50)));
        // У начала клин тонкий, у наконечника — широкий
        Assert.True(IsBackground(PixelAt(image, 25, 58)));
        Assert.False(IsBackground(PixelAt(image, 160, 58)));
    });

    [Fact]
    public void PinOffset_MovesPinCircle() => StaRunner.Run(() =>
    {
        var box = Shape(AnnotationKind.Rectangle, new(50, 50), new(150, 90)) with { Note = "заметка", PinOffset = new PointD(40, 0) };
        var image = Render(null, box);
        // Кружок в (90,50): надпись в центре белая, поэтому смотрим выше центра
        Assert.Equal((0xF5, 0x7B, 0x2F), PixelAt(image, 90, 40));
        Assert.True(IsBackground(PixelAt(image, 50, 40)));
    });

    [Fact]
    public void ObscuredImages_SameArea_ReturnsCachedInstance() => StaRunner.Run(() =>
    {
        var image = BitmapSource.Create(40, 40, 96, 96, PixelFormats.Bgra32, null, new byte[40 * 40 * 4], 40 * 4);
        image.Freeze();
        var source = new BlurSource(image, new PointD(0, 0));
        var first = ObscuredImages.Get(source, new RectI(5, 5, 20, 20));
        Assert.NotNull(first);
        Assert.Same(first, ObscuredImages.Get(source, new RectI(5, 5, 20, 20)));
    });

    [Fact]
    public void ObscuredImages_LargeArea_KeepsSmallBitmap() => StaRunner.Run(() =>
    {
        // Пока тянут рамку размытия, каждая новая область попадает в кэш: копия во весь размер
        // области (здесь почти 4 МБ) на каждый кадр — это рывки и сотни мегабайт памяти
        var image = BitmapSource.Create(1200, 800, 96, 96, PixelFormats.Bgra32, null, new byte[1200 * 800 * 4], 1200 * 4);
        image.Freeze();
        var obscured = ObscuredImages.Get(new BlurSource(image, new PointD(0, 0)), new RectI(0, 0, 1200, 800));
        Assert.NotNull(obscured);
        Assert.True(obscured.PixelWidth * obscured.PixelHeight <= 1200 * 800 / 100,
            $"размытая копия {obscured.PixelWidth}×{obscured.PixelHeight}");
    });
}
