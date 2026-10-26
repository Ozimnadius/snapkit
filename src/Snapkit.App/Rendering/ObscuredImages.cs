using System.Buffers;
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
/// Размытые области снимка: маленькая картинка, пиксель на блок размытия (ImageFilters.ObscureBlocks).
/// Её растягивают на область (Draw), поэтому при перетаскивании рамки на кадр уходит один проход по
/// пикселям области без больших выделений памяти, а кэш почти ничего не весит. Одна и та же область
/// считается один раз; кэш привязан к картинке слабо и исчезает вместе с ней.
/// </summary>
internal static class ObscuredImages
{
    // Пока тянут рамку размытия, областей много — держим только последние
    private const int MaxPerImage = 32;

    private static readonly ConditionalWeakTable<BitmapSource, Cache> Caches = new();

    /// <summary>
    /// Размытая область картинки (areaInImage — в её пикселях): картинка Columns×Rows, которую растягивают
    /// на блоки BlockSize от левого верхнего угла видимой части области; null, если область за краем.
    /// </summary>
    public static BitmapSource? Get(BlurSource source, RectI areaInImage) => GetBlocks(source, areaInImage)?.Image;

    /// <summary>Нарисовать размытую область rect (в координатах фигур) с плавным переходом между блоками.</summary>
    public static void Draw(DrawingContext dc, BlurSource source, Rect rect)
    {
        var area = new RectI(
            (int)Math.Floor(rect.X - source.Origin.X), (int)Math.Floor(rect.Y - source.Origin.Y),
            (int)Math.Ceiling(rect.Width) + 1, (int)Math.Ceiling(rect.Height) + 1);
        if (GetBlocks(source, area) is not { } blocks) return;

        var visible = new Rect(blocks.Area.X + source.Origin.X, blocks.Area.Y + source.Origin.Y, blocks.Area.Width, blocks.Area.Height);
        visible.Intersect(rect);
        if (visible.IsEmpty) return;

        // Под растянутой сеткой — сплошной средний цвет: если край растянутой картинки
        // сгладится в прозрачность, сквозь него будет виден он, а не исходник
        var group = new DrawingGroup();
        RenderOptions.SetBitmapScalingMode(group, BitmapScalingMode.Linear);
        using (var inner = group.Open())
        {
            inner.DrawRectangle(blocks.Average, null, visible);
            inner.DrawImage(blocks.Image, new Rect(blocks.Area.X + source.Origin.X, blocks.Area.Y + source.Origin.Y,
                blocks.Image.PixelWidth * blocks.BlockSize, blocks.Image.PixelHeight * blocks.BlockSize));
        }
        group.ClipGeometry = new RectangleGeometry(visible);
        group.Freeze();
        dc.DrawDrawing(group);
    }

    private static Blocks? GetBlocks(BlurSource source, RectI areaInImage)
    {
        var image = source.Image;
        var x0 = Math.Clamp(areaInImage.X, 0, image.PixelWidth);
        var y0 = Math.Clamp(areaInImage.Y, 0, image.PixelHeight);
        var visible = new RectI(x0, y0,
            Math.Max(0, Math.Clamp(areaInImage.Right, 0, image.PixelWidth) - x0),
            Math.Max(0, Math.Clamp(areaInImage.Bottom, 0, image.PixelHeight) - y0));
        if (visible.Width == 0 || visible.Height == 0) return null;

        // Размер блока зависит от всей области, а не от видимой части, — ключ кэша тоже вся область
        var cache = Caches.GetValue(image, _ => new Cache());
        lock (cache)
        {
            if (cache.Items.TryGetValue(areaInImage, out var cached)) return cached;
        }

        var blocks = Obscure(image, areaInImage, visible);
        lock (cache)
        {
            if (cache.Items.TryAdd(areaInImage, blocks)) cache.Order.Enqueue(areaInImage);
            while (cache.Order.Count > MaxPerImage) cache.Items.Remove(cache.Order.Dequeue());
        }
        return blocks;
    }

    private static Blocks Obscure(BitmapSource image, RectI area, RectI visible)
    {
        // Читаем только видимую часть области, а не весь кадр монитора, во временный буфер из пула
        var bgra = image.Format == PixelFormats.Bgra32 ? image : new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var length = visible.Width * visible.Height * 4;
        var pixels = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            bgra.CopyPixels(new Int32Rect(visible.X, visible.Y, visible.Width, visible.Height), pixels, visible.Width * 4, 0);
            var local = new RectI(area.X - visible.X, area.Y - visible.Y, area.Width, area.Height);
            var result = ImageFilters.ObscureBlocks(pixels, visible.Width, visible.Height, local);
            var bitmap = BitmapSource.Create(result.Columns, result.Rows, 96, 96, PixelFormats.Bgra32, null, result.Pixels, result.Columns * 4);
            bitmap.Freeze();
            return new Blocks(bitmap, result.BlockSize, visible, AverageBrush(result.Pixels));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pixels);
        }
    }

    private static SolidColorBrush AverageBrush(byte[] bgra)
    {
        var (b, g, r) = (0L, 0L, 0L);
        var n = bgra.Length / 4;
        for (var i = 0; i < bgra.Length; i += 4) (b, g, r) = (b + bgra[i], g + bgra[i + 1], r + bgra[i + 2]);
        var brush = new SolidColorBrush(Color.FromRgb((byte)(r / n), (byte)(g / n), (byte)(b / n)));
        brush.Freeze();
        return brush;
    }

    /// <summary>Сетка блоков, сторона блока и видимая часть области (в пикселях картинки).</summary>
    private sealed record Blocks(BitmapSource Image, int BlockSize, RectI Area, SolidColorBrush Average);

    private sealed class Cache
    {
        public readonly Dictionary<RectI, Blocks> Items = [];
        public readonly Queue<RectI> Order = new();
    }
}
