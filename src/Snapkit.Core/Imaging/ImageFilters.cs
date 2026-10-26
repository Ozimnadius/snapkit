namespace Snapkit.Core.Imaging;

/// <summary>Обработка пикселей снимка без WPF — чтобы её можно было проверить тестами.</summary>
public static class ImageFilters
{
    /// <summary>Размер блока размытия в пикселях снимка: мельче — текст начинает читаться.</summary>
    public const int ObscureBlock = 12;

    /// <summary>
    /// Скрыть содержимое области: цвет усредняется по блокам ObscureBlock, а между центрами блоков
    /// плавно переходит — получается сильное размытие без мозаики. pixels — BGRA32 всего изображения,
    /// stride = width * 4; входной массив не меняется. Возвращает пиксели области, пересечённой
    /// с изображением (альфа 255), и саму эту область; пустое пересечение — пустой массив.
    /// </summary>
    public static (byte[] Pixels, RectI Area) ObscureArea(byte[] pixels, int width, int height, RectI area)
    {
        var x0 = Math.Clamp(area.X, 0, width);
        var y0 = Math.Clamp(area.Y, 0, height);
        var clamped = new RectI(x0, y0, Math.Max(0, Math.Clamp(area.Right, 0, width) - x0), Math.Max(0, Math.Clamp(area.Bottom, 0, height) - y0));
        if (clamped.Width == 0 || clamped.Height == 0) return ([], clamped);

        var (w, h) = (clamped.Width, clamped.Height);
        var (blocksX, blocksY) = ((w + ObscureBlock - 1) / ObscureBlock, (h + ObscureBlock - 1) / ObscureBlock);

        // Средний цвет каждого блока
        var sums = new double[blocksX * blocksY * 3];
        var counts = new int[blocksX * blocksY];
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var block = y / ObscureBlock * blocksX + x / ObscureBlock;
            var i = ((y0 + y) * width + x0 + x) * 4;
            sums[block * 3] += pixels[i];
            sums[block * 3 + 1] += pixels[i + 1];
            sums[block * 3 + 2] += pixels[i + 2];
            counts[block]++;
        }
        for (var block = 0; block < counts.Length; block++)
        for (var c = 0; c < 3; c++)
            sums[block * 3 + c] /= counts[block];

        // Плавный переход между центрами блоков
        var result = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        {
            var (by0, by1, ty) = Neighbours(y, blocksY);
            for (var x = 0; x < w; x++)
            {
                var (bx0, bx1, tx) = Neighbours(x, blocksX);
                var o = (y * w + x) * 4;
                for (var c = 0; c < 3; c++)
                {
                    var top = Lerp(sums[(by0 * blocksX + bx0) * 3 + c], sums[(by0 * blocksX + bx1) * 3 + c], tx);
                    var bottom = Lerp(sums[(by1 * blocksX + bx0) * 3 + c], sums[(by1 * blocksX + bx1) * 3 + c], tx);
                    result[o + c] = (byte)Math.Round(Lerp(top, bottom, ty));
                }
                result[o + 3] = 255;
            }
        }
        return (result, clamped);
    }

    // Два соседних блока вокруг пикселя и доля второго из них
    private static (int First, int Second, double T) Neighbours(int position, int blocks)
    {
        var f = (position + 0.5) / ObscureBlock - 0.5;
        var first = Math.Clamp((int)Math.Floor(f), 0, blocks - 1);
        var second = Math.Min(first + 1, blocks - 1);
        return (first, second, Math.Clamp(f - first, 0, 1));
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
