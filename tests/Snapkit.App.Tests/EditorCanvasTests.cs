using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.App.Editor;

namespace Snapkit.App.Tests;

public class EditorCanvasTests
{
    [Fact]
    public void Background_IsDrawnOneToOne_WhenCanvasIsSmallerThanImage() => StaRunner.Run(() =>
    {
        // Снимок 200×100: левая половина красная, правая синяя. Холст на экране — только 100×50
        // (снимок не поместился на монитор). Картинку нельзя сжимать: разметка тогда съедет.
        const int w = 200, h = 100;
        var pixels = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var i = (y * w + x) * 4;
            if (x < 100) pixels[i + 2] = 255; else pixels[i] = 255;
            pixels[i + 3] = 255;
        }
        var image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
        image.Freeze();

        var canvas = new EditorCanvas { PixelWidth = 100, PixelHeight = 50, Background = image };
        canvas.Measure(new Size(100, 50));
        canvas.Arrange(new Rect(0, 0, 100, 50));
        var target = new RenderTargetBitmap(100, 50, 96, 96, PixelFormats.Pbgra32);
        target.Render(canvas);

        var px = new byte[4];
        target.CopyPixels(new Int32Rect(60, 25, 1, 1), px, 4, 0);
        Assert.Equal((0, 0, 255), (px[0], px[1], px[2])); // точка (60,25) снимка — красная
    });
}
