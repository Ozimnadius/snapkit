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
        Guid.NewGuid(), w, h, new RectI(0, 0, w, h), [.. annotations], "", false, DateTimeOffset.UnixEpoch);

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
