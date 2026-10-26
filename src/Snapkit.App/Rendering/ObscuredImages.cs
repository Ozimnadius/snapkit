using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.Core;
using Snapkit.Core.Imaging;

namespace Snapkit.App.Rendering;

/// <summary>Откуда брать пиксели для размытия: Origin — где пиксель (0,0) картинки в координатах фигур.</summary>
public sealed record BlurSource(BitmapSource Image, PointD Origin);

/// <summary>
/// Размытые копии областей снимка. Считаются один раз на область: при перерисовке редактора
/// на каждое движение мыши размытие не пересчитывается. Кэш привязан к картинке слабо и исчезает вместе с ней.
/// </summary>
internal static class ObscuredImages
{
    // Пока тянут рамку размытия, областей много — держим только последние
    private const int MaxPerImage = 32;

    private static readonly ConditionalWeakTable<BitmapSource, Cache> Caches = new();

    /// <summary>Размытая область картинки (в её пикселях) или null, если область за краем.</summary>
    public static BitmapSource? Get(BlurSource source, RectI areaInImage)
    {
        var image = source.Image;
        var x0 = Math.Clamp(areaInImage.X, 0, image.PixelWidth);
        var y0 = Math.Clamp(areaInImage.Y, 0, image.PixelHeight);
        var area = new RectI(x0, y0,
            Math.Max(0, Math.Clamp(areaInImage.Right, 0, image.PixelWidth) - x0),
            Math.Max(0, Math.Clamp(areaInImage.Bottom, 0, image.PixelHeight) - y0));
        if (area.Width == 0 || area.Height == 0) return null;

        var cache = Caches.GetValue(image, _ => new Cache());
        lock (cache)
        {
            if (cache.Items.TryGetValue(area, out var cached)) return cached;
        }

        var obscured = Obscure(image, area);
        lock (cache)
        {
            cache.Items[area] = obscured;
            cache.Order.Enqueue(area);
            while (cache.Order.Count > MaxPerImage) cache.Items.Remove(cache.Order.Dequeue());
        }
        return obscured;
    }

    private static BitmapSource Obscure(BitmapSource image, RectI area)
    {
        // Читаем только нужную область, а не весь кадр монитора
        var bgra = image.Format == PixelFormats.Bgra32 ? image : new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[area.Width * area.Height * 4];
        bgra.CopyPixels(new Int32Rect(area.X, area.Y, area.Width, area.Height), pixels, area.Width * 4, 0);
        var (result, _) = ImageFilters.ObscureArea(pixels, area.Width, area.Height, new RectI(0, 0, area.Width, area.Height));
        var bitmap = BitmapSource.Create(area.Width, area.Height, 96, 96, PixelFormats.Bgra32, null, result, area.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private sealed class Cache
    {
        public readonly Dictionary<RectI, BitmapSource> Items = [];
        public readonly Queue<RectI> Order = new();
    }
}
