using Snapkit.Core.Settings;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.App.Files;
using Snapkit.App.Imaging;
using Snapkit.App.Rendering;
using Snapkit.Core;
using Snapkit.Core.Model;

namespace Snapkit.App.Tests;

public sealed class CaptureFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "snapkit-files-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static BitmapSource Image(int w, int h)
    {
        var image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, new byte[w * h * 4], w * 4);
        image.Freeze();
        return image;
    }

    [Fact]
    public void DragFileName_WithAndWithoutLetter()
    {
        Assert.Equal("Снимок B.png", CaptureFiles.DragFileName('B'));
        Assert.Equal("Снимок.png", CaptureFiles.DragFileName(null));
    }

    [Fact]
    public void WriteDragFile_TwiceSameLetter_BothFilesExist() => StaRunner.Run(() =>
    {
        var first = CaptureFiles.WriteDragFile(_dir, Image(30, 20), 'A');
        var second = CaptureFiles.WriteDragFile(_dir, Image(30, 20), 'A');

        Assert.NotEqual(first, second);
        Assert.Equal("Снимок A.png", Path.GetFileName(first));
        Assert.All([first, second], path =>
        {
            var image = ImageCodecs.DecodePng(File.ReadAllBytes(path));
            Assert.Equal((30, 20), (image.PixelWidth, image.PixelHeight));
        });
    });

    [Fact]
    public void CleanDragRoot_RemovesEverything() => StaRunner.Run(() =>
    {
        CaptureFiles.WriteDragFile(_dir, Image(4, 4), null);
        CaptureFiles.CleanDragRoot(_dir);
        Assert.False(Directory.Exists(_dir) && Directory.EnumerateFileSystemEntries(_dir).Any());
        CaptureFiles.CleanDragRoot(Path.Combine(_dir, "нет такой папки"));
    });

    [Fact]
    public void SavePng_WritesPng() => StaRunner.Run(() =>
    {
        var path = Path.Combine(_dir, "снимок.png");
        Directory.CreateDirectory(_dir);
        CaptureFiles.SavePng(path, Image(12, 8));
        var image = ImageCodecs.DecodePng(File.ReadAllBytes(path));
        Assert.Equal((12, 8), (image.PixelWidth, image.PixelHeight));
    });

    [Fact]
    public void SavePackage_WritesRenderedImagesAndPrompt() => StaRunner.Run(() =>
    {
        // Полосы по 2 px — как мелкий текст; размытие поверх них должно уйти в файл
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
        var blur = new Annotation(Guid.NewGuid(), AnnotationKind.Blur, [new(20, 10), new(100, 50)], "#E53935", 4, null, "пароль");
        var capture = new Capture(Guid.NewGuid(), w, h, new RectI(0, 0, w, h), [blur], "", DateTimeOffset.UnixEpoch);
        var item = new LetteredCapture('A', capture);
        var time = new DateTime(2026, 10, 26, 15, 40, 12);

        var folder = CaptureFiles.SavePackage(_dir, time, [(item, CaptureRenderer.Render(source, capture, 'A'))]);
        var again = CaptureFiles.SavePackage(_dir, time, [(item, CaptureRenderer.Render(source, capture, 'A'))]);

        Assert.Equal("2026-10-26_15-40-12", Path.GetFileName(folder));
        Assert.Equal("2026-10-26_15-40-12 (2)", Path.GetFileName(again));
        Assert.Equal(PackageExport.BuildPromptMarkdown([item]), File.ReadAllText(Path.Combine(folder, "prompt.md")));
        var saved = ImageCodecs.DecodePng(File.ReadAllBytes(Path.Combine(folder, "A.png")));
        var row = new byte[w * 4];
        var values = new List<byte>();
        // Кружок заметки A1 стоит в левом верхнем углу рамки (до y = 23) — замер ниже него
        for (var y = 26; y < 46; y++)
        {
            saved.CopyPixels(new System.Windows.Int32Rect(0, y, w, 1), row, w * 4, 0);
            for (var x = 24; x < 96; x++) values.Add(row[x * 4 + 2]);
        }
        Assert.True(values.Max() - values.Min() < 60, $"разброс под размытием {values.Max() - values.Min()}");
    });

    // Картинка с узором: JPEG хуже сжимает её при высоком качестве, чем при низком
    private static BitmapSource Pattern(int w, int h)
    {
        var pixels = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var i = (y * w + x) * 4;
            (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = ((byte)(x * 7 + y), (byte)(x ^ y), (byte)(y * 5), 255);
        }
        var image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
        image.Freeze();
        return image;
    }

    [Fact]
    public void Save_Jpeg_WritesJpeg() => StaRunner.Run(() =>
    {
        Directory.CreateDirectory(_dir);
        var low = Path.Combine(_dir, "low.jpg");
        var high = Path.Combine(_dir, "high.jpg");
        CaptureFiles.Save(low, Pattern(200, 120), SaveFormat.Jpeg, 10);
        CaptureFiles.Save(high, Pattern(200, 120), SaveFormat.Jpeg, 95);

        var bytes = File.ReadAllBytes(low);
        Assert.Equal((0xFF, 0xD8), (bytes[0], bytes[1]));
        Assert.True(new FileInfo(low).Length < new FileInfo(high).Length);
    });

    [Fact]
    public void SavePackage_Jpeg_WritesJpgAndLinks() => StaRunner.Run(() =>
    {
        var capture = new Capture(Guid.NewGuid(), 40, 30, new RectI(0, 0, 40, 30), [], "", DateTimeOffset.UnixEpoch);
        var item = new LetteredCapture('A', capture);

        var folder = CaptureFiles.SavePackage(_dir, new DateTime(2026, 10, 27, 9, 0, 0), [(item, Pattern(40, 30))], SaveFormat.Jpeg, 90);

        Assert.True(File.Exists(Path.Combine(folder, "A.jpg")));
        Assert.False(File.Exists(Path.Combine(folder, "A.png")));
        Assert.Contains("![Снимок A](A.jpg)", File.ReadAllText(Path.Combine(folder, "prompt.md")));
    });
}
