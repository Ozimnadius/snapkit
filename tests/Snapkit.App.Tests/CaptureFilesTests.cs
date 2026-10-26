using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.App.Files;
using Snapkit.App.Imaging;

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
}
