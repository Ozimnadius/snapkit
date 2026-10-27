using System.Collections.Immutable;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.App.Rendering;
using Snapkit.Core;
using Snapkit.Core.Model;

namespace Snapkit.App.Tests;

public class CaptureRendererTests
{
    private static BitmapSource Gray(int w, int h)
    {
        var pixels = Enumerable.Repeat<byte>(200, w * h * 4).ToArray();
        var image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
        image.Freeze();
        return image;
    }

    private static Capture CaptureWith(int w, int h, params Annotation[] annotations) => new(
        Guid.NewGuid(), w, h, new RectI(0, 0, w, h), [.. annotations], "", DateTimeOffset.UnixEpoch);

    private static Annotation Box(string? note, PointD a, PointD b) => new(
        Guid.NewGuid(), AnnotationKind.Rectangle, [a, b], "#E53935", 4, null, note);

    private static (byte B, byte G, byte R) PixelAt(BitmapSource image, int x, int y)
    {
        var px = new byte[4];
        image.CopyPixels(new System.Windows.Int32Rect(x, y, 1, 1), px, 4, 0);
        return (px[0], px[1], px[2]);
    }

    [Fact]
    public void Render_OutputHasCapturePixelSize() => StaRunner.Run(() =>
    {
        var result = CaptureRenderer.Render(Gray(200, 100), CaptureWith(200, 100, Box("x", new(10, 10), new(60, 40))), 'A');
        Assert.Equal((200, 100), (result.PixelWidth, result.PixelHeight));
        Assert.Equal((96d, 96d), (result.DpiX, result.DpiY));
        Assert.Equal(PixelFormats.Bgra32, result.Format);
    });

    [Fact]
    public void Render_DrawsPinWithAccentColor() => StaRunner.Run(() =>
    {
        var result = CaptureRenderer.Render(Gray(200, 100), CaptureWith(200, 100, Box("заметка", new(50, 50), new(150, 90))), 'A');
        // Кружок с центром в углу рамки (50,50); надпись «A1» в центре белая, поэтому смотрим чуть выше.
        Assert.Equal((0xF5, 0x7B, 0x2F), PixelAt(result, 50, 40));
    });

    [Fact]
    public void Render_DrawsRectangleOutlineInItsColor() => StaRunner.Run(() =>
    {
        var result = CaptureRenderer.Render(Gray(200, 100), CaptureWith(200, 100, Box(null, new(50, 50), new(150, 90))), 'A');
        Assert.Equal((0x35, 0x39, 0xE5), PixelAt(result, 100, 50));  // верхняя сторона рамки, цвет #E53935
        Assert.Equal((200, 200, 200), PixelAt(result, 100, 70));      // внутри рамки — исходный снимок
    });

    [Fact]
    public void Render_WithoutNote_DrawsNoPin() => StaRunner.Run(() =>
    {
        var result = CaptureRenderer.Render(Gray(200, 100), CaptureWith(200, 100, Box(null, new(50, 50), new(150, 90))), 'A');
        Assert.Equal((200, 200, 200), PixelAt(result, 50, 40));
    });

    [Fact]
    public void Blur_InFinalImage_HidesStripes() => StaRunner.Run(() =>
    {
        // Вертикальные полосы по 2 px — как штрихи мелкого текста
        const int w = 120, h = 60;
        var pixels = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var v = (byte)(x / 2 % 2 == 0 ? 0 : 255);
            var i = (y * w + x) * 4;
            (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (v, v, v, 255);
        }
        var source = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
        source.Freeze();
        var blur = new Annotation(Guid.NewGuid(), AnnotationKind.Blur, [new(20, 10), new(100, 50)], "#E53935", 4, null, null);

        var result = CaptureRenderer.Render(source, CaptureWith(w, h, blur), 'A');

        var inside = (from y in Enumerable.Range(14, 32) from x in Enumerable.Range(24, 72) select PixelAt(result, x, y).R).ToList();
        Assert.True(inside.Max() - inside.Min() < 60, $"разброс под размытием {inside.Max() - inside.Min()}");
        // Вне размытия полосы на месте
        Assert.Equal((0, 0, 0), PixelAt(result, 4, 30));
        Assert.Equal((255, 255, 255), PixelAt(result, 6, 30));
    });

    [Fact]
    public void Blur_InFinalImage_HidesStripesUpToEdges() => StaRunner.Run(() =>
    {
        const int w = 120, h = 60;
        var pixels = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var v = (byte)(x / 2 % 2 == 0 ? 0 : 255);
            var i = (y * w + x) * 4;
            (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (v, v, v, 255);
        }
        var source = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
        source.Freeze();
        var blur = new Annotation(Guid.NewGuid(), AnnotationKind.Blur, [new(21, 11), new(98, 47)], "#E53935", 4, null, null);

        var result = CaptureRenderer.Render(source, CaptureWith(w, h, blur), 'A');

        // Каждый пиксель внутри рамки, включая крайние ряды, — без следа полос
        var inside = (from y in Enumerable.Range(11, 36) from x in Enumerable.Range(21, 77) select PixelAt(result, x, y).R).ToList();
        Assert.True(inside.Max() - inside.Min() < 60, $"разброс под размытием {inside.Max() - inside.Min()}");
    });

    [Fact]
    public void RenderThumbnail_FitsMaxWidth() => StaRunner.Run(() =>
    {
        var thumb = CaptureRenderer.RenderThumbnail(Gray(400, 200), CaptureWith(400, 200), 'A', 200);
        Assert.Equal((200, 100), (thumb.PixelWidth, thumb.PixelHeight));
    });

    [Fact]
    public void RenderThumbnail_DoesNotUpscale() => StaRunner.Run(() =>
    {
        var thumb = CaptureRenderer.RenderThumbnail(Gray(100, 50), CaptureWith(100, 50), null, 200);
        Assert.Equal((100, 50), (thumb.PixelWidth, thumb.PixelHeight));
    });
}
