using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.App.Imaging;

namespace Snapkit.App.Tests;

public class ImageCodecsTests
{
    // Картинка 3×2: верхняя строка красная, нижняя синяя (Bgra32).
    internal static BitmapSource RedOverBlue()
    {
        byte[] red = [0, 0, 255, 255], blue = [255, 0, 0, 255];
        var pixels = Enumerable.Repeat(red, 3).Concat(Enumerable.Repeat(blue, 3)).SelectMany(p => p).ToArray();
        var image = BitmapSource.Create(3, 2, 96, 96, PixelFormats.Bgra32, null, pixels, 3 * 4);
        image.Freeze();
        return image;
    }

    [Fact]
    public void EncodeDib_HeaderAndBottomUpRows() => StaRunner.Run(() =>
    {
        var dib = ImageCodecs.EncodeDib(RedOverBlue());
        Assert.Equal(40, BitConverter.ToInt32(dib, 0));   // размер заголовка
        Assert.Equal(3, BitConverter.ToInt32(dib, 4));    // ширина
        Assert.Equal(2, BitConverter.ToInt32(dib, 8));    // высота > 0 — строки снизу вверх
        Assert.Equal(1, BitConverter.ToInt16(dib, 12));   // плоскости
        Assert.Equal(32, BitConverter.ToInt16(dib, 14));  // бит на пиксель
        Assert.Equal(0, BitConverter.ToInt32(dib, 16));   // BI_RGB
        Assert.Equal(40 + 3 * 2 * 4, dib.Length);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, dib[40..44]);  // первой идёт нижняя (синяя) строка
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, dib[^4..]);    // последней — верхняя (красная)
    });

    [Fact]
    public void Png_RoundTrip_KeepsSizeAndPixels() => StaRunner.Run(() =>
    {
        var original = RedOverBlue();
        var decoded = ImageCodecs.DecodePng(ImageCodecs.EncodePng(original));
        Assert.Equal((3, 2), (decoded.PixelWidth, decoded.PixelHeight));
        Assert.Equal((96d, 96d), (decoded.DpiX, decoded.DpiY));
        Assert.Equal(PixelFormats.Bgra32, decoded.Format);
        Assert.True(decoded.IsFrozen);
        Assert.Equal(Pixels(original), Pixels(decoded));
    });

    internal static byte[] Pixels(BitmapSource image)
    {
        var buffer = new byte[image.PixelWidth * image.PixelHeight * 4];
        image.CopyPixels(buffer, image.PixelWidth * 4, 0);
        return buffer;
    }
}
