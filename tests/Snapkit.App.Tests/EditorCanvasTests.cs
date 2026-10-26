using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.App.Editor;
using Snapkit.Core.Model;

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

        var canvas = new EditorCanvas { PixelWidth = 100, PixelHeight = 50, Source = image, ShowSource = true };
        canvas.Measure(new Size(100, 50));
        canvas.Arrange(new Rect(0, 0, 100, 50));
        var target = new RenderTargetBitmap(100, 50, 96, 96, PixelFormats.Pbgra32);
        target.Render(canvas);

        var px = new byte[4];
        target.CopyPixels(new Int32Rect(60, 25, 1, 1), px, 4, 0);
        Assert.Equal((0, 0, 255), (px[0], px[1], px[2])); // точка (60,25) снимка — красная
    });

    [Fact]
    public void Pin_IsDrawnOverSelectionHandles() => StaRunner.Run(() =>
    {
        // Кружок рамки стоит на её левом верхнем маркере; щелчок по нему хватает кружок,
        // значит и виден должен быть кружок, а не белый квадрат маркера
        var box = new Annotation(Guid.NewGuid(), AnnotationKind.Rectangle, [new(50, 50), new(150, 100)], "#E53935", 4, null, "заметка");
        var canvas = new EditorCanvas { PixelWidth = 200, PixelHeight = 120, Items = [box], SelectedId = box.Id, HandleSize = 24 };
        canvas.Measure(new Size(200, 120));
        canvas.Arrange(new Rect(0, 0, 200, 120));
        var target = new RenderTargetBitmap(200, 120, 96, 96, PixelFormats.Pbgra32);
        target.Render(canvas);

        // Точка внутри кружка (центр 50,50) выше надписи «A1» и внутри квадрата маркера
        var px = new byte[4];
        target.CopyPixels(new Int32Rect(50, 43, 1, 1), px, 4, 0);
        Assert.Equal((0xF5, 0x7B, 0x2F), (px[0], px[1], px[2]));
    });
}
