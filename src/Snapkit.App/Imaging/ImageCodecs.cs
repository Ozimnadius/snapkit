using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Snapkit.App.Imaging;

/// <summary>Кодирование снимков: PNG для файлов и буфера обмена, DIB — для программ, которые не понимают PNG.</summary>
public static class ImageCodecs
{
    private const int BitmapInfoHeaderSize = 40;

    public static byte[] EncodePng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>Картинка всегда приводится к Bgra32 и 96 DPI, чтобы 1 пиксель = 1 единица WPF.</summary>
    public static BitmapSource DecodePng(byte[] png)
    {
        using var stream = new MemoryStream(png);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return Normalize(decoder.Frames[0]);
    }

    /// <summary>
    /// Картинка из файла (PNG, JPEG и другие форматы Windows) → Bgra32, 96 DPI; прозрачность PNG сохраняется.
    /// Не картинка — NotSupportedException.
    /// </summary>
    public static BitmapSource DecodeImage(byte[] data)
    {
        try
        {
            using var stream = new MemoryStream(data);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return Normalize(decoder.Frames[0]);
        }
        catch (FileFormatException ex)
        {
            throw new NotSupportedException("Файл не читается как картинка", ex);
        }
    }

    /// <summary>BITMAPINFOHEADER + пиксели 32 bpp BI_RGB, строки снизу вверх (формат CF_DIB).</summary>
    public static byte[] EncodeDib(BitmapSource image)
    {
        var pixels = Bgra32Pixels(image, out var width, out var height);
        var stride = width * 4;
        var dib = new byte[BitmapInfoHeaderSize + pixels.Length];
        var header = dib.AsSpan(0, BitmapInfoHeaderSize);
        BitConverter.TryWriteBytes(header[0..], BitmapInfoHeaderSize); // biSize
        BitConverter.TryWriteBytes(header[4..], width);                // biWidth
        BitConverter.TryWriteBytes(header[8..], height);               // biHeight > 0 — снизу вверх
        BitConverter.TryWriteBytes(header[12..], (short)1);            // biPlanes
        BitConverter.TryWriteBytes(header[14..], (short)32);           // biBitCount
        BitConverter.TryWriteBytes(header[16..], 0);                   // biCompression = BI_RGB
        BitConverter.TryWriteBytes(header[20..], pixels.Length);       // biSizeImage
        // Остальные поля (разрешение, палитра) — нули.

        for (var row = 0; row < height; row++)
        {
            var source = pixels.AsSpan((height - 1 - row) * stride, stride);
            source.CopyTo(dib.AsSpan(BitmapInfoHeaderSize + row * stride, stride));
        }
        return dib;
    }

    /// <summary>Копия картинки в Bgra32, 96 DPI, замороженная.</summary>
    public static BitmapSource Normalize(BitmapSource image)
    {
        var pixels = Bgra32Pixels(image, out var width, out var height);
        var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        result.Freeze();
        return result;
    }

    /// <summary>
    /// Копия с полностью непрозрачными пикселями. Картинки из буфера обмена (CF_DIB) часто
    /// приходят с нулевой альфой, и без этого снимок выглядел бы пустым.
    /// </summary>
    public static BitmapSource Opaque(BitmapSource image)
    {
        var pixels = Bgra32Pixels(image, out var width, out var height);
        for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
        var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        result.Freeze();
        return result;
    }

    private static byte[] Bgra32Pixels(BitmapSource image, out int width, out int height)
    {
        BitmapSource bgra = image.Format == PixelFormats.Bgra32
            ? image
            : new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        width = bgra.PixelWidth;
        height = bgra.PixelHeight;
        var pixels = new byte[width * height * 4];
        bgra.CopyPixels(pixels, width * 4, 0);
        return pixels;
    }
}
