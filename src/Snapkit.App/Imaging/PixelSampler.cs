using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.Core;

namespace Snapkit.App.Imaging;

/// <summary>Цвет пикселя снимка для пипетки.</summary>
public static class PixelSampler
{
    /// <summary>
    /// Цвет «#RRGGBB» пикселя под точкой point; origin — где пиксель (0,0) картинки в тех же координатах.
    /// За краем картинки — null.
    /// </summary>
    public static string? HexAt(BitmapSource image, PointD origin, PointD point)
    {
        var x = (int)Math.Floor(point.X - origin.X);
        var y = (int)Math.Floor(point.Y - origin.Y);
        if (x < 0 || y < 0 || x >= image.PixelWidth || y >= image.PixelHeight) return null;

        var bgra = image.Format == PixelFormats.Bgra32 ? image : new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var px = new byte[4];
        bgra.CopyPixels(new Int32Rect(x, y, 1, 1), px, 4, 0);
        return $"#{px[2]:X2}{px[1]:X2}{px[0]:X2}";
    }
}
