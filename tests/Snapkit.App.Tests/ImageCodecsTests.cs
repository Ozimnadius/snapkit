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

    [Fact]
    public void Opaque_SetsAlphaTo255KeepingColor() => StaRunner.Run(() =>
    {
        var transparentRed = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 0 }, 4);
        var result = ImageCodecs.Opaque(transparentRed);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixels(result));
        Assert.True(result.IsFrozen);
    });

    internal static byte[] Pixels(BitmapSource image)
    {
        var buffer = new byte[image.PixelWidth * image.PixelHeight * 4];
        image.CopyPixels(buffer, image.PixelWidth * 4, 0);
        return buffer;
    }

    [Fact]
    public void DecodeImage_Jpeg_IsBgra32At96Dpi() => StaRunner.Run(() =>
    {
        var source = BitmapSource.Create(30, 20, 144, 144, PixelFormats.Bgr24, null, new byte[30 * 20 * 3], 30 * 3);
        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new System.IO.MemoryStream();
        encoder.Save(stream);

        var image = ImageCodecs.DecodeImage(stream.ToArray());

        Assert.Equal((30, 20, PixelFormats.Bgra32, 96.0), (image.PixelWidth, image.PixelHeight, image.Format, image.DpiX));
        Assert.True(image.IsFrozen);
    });

    [Fact]
    public void DecodeImage_Png_KeepsSize() => StaRunner.Run(() =>
    {
        var image = ImageCodecs.DecodeImage(ImageCodecs.EncodePng(RedOverBlue()));
        Assert.Equal((3, 2), (image.PixelWidth, image.PixelHeight));
    });

    [Fact]
    public void DecodeImage_Garbage_Throws() => StaRunner.Run(() =>
        Assert.Throws<NotSupportedException>(() => ImageCodecs.DecodeImage([1, 2, 3, 4, 5, 6, 7, 8])));
}
