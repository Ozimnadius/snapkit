namespace Snapkit.Core.Imaging;

/// <summary>Обработка пикселей снимка без WPF — чтобы её можно было проверить тестами.</summary>
public static class ImageFilters
{
    /// <summary>Наименьший блок размытия в пикселях снимка: мельче — текст начинает читаться.</summary>
    public const int ObscureBlock = 12;

    /// <summary>
    /// Блок размытия для области: не меньше ObscureBlock и не меньше четверти её меньшей стороны,
    /// чтобы крупный текст в тесной рамке тоже не читался. Считается по заданной области, а не по её
    /// части внутри изображения, — редактор и итоговая картинка размывают одинаково.
    /// </summary>
    public static int ObscureBlockFor(RectI area) => Math.Max(ObscureBlock, (Math.Min(area.Width, area.Height) + 3) / 4);

    /// <summary>
    /// Скрыть содержимое области: цвет усредняется по блокам (ObscureBlockFor), и каждый блок ещё
    /// смешивается с соседними. Результат — маленькая картинка, пиксель на блок; её растягивают
    /// на область с плавным переходом между центрами блоков — получается сильное размытие без мозаики,
    /// а считать и хранить приходится в сотни раз меньше пикселей. pixels — BGRA32 всего изображения,
    /// stride = width * 4; входной массив не меняется. Пустое пересечение с изображением — пустая сетка.
    /// </summary>
    public static ObscuredBlocks ObscureBlocks(byte[] pixels, int width, int height, RectI area)
    {
        var x0 = Math.Clamp(area.X, 0, width);
        var y0 = Math.Clamp(area.Y, 0, height);
        var clamped = new RectI(x0, y0, Math.Max(0, Math.Clamp(area.Right, 0, width) - x0), Math.Max(0, Math.Clamp(area.Bottom, 0, height) - y0));
        var size = ObscureBlockFor(area);
        if (clamped.Width == 0 || clamped.Height == 0) return new ObscuredBlocks([], 0, 0, size, clamped);

        var (w, h) = (clamped.Width, clamped.Height);
        var (blocksX, blocksY) = ((w + size - 1) / size, (h + size - 1) / size);

        // Средний цвет каждого блока
        var sums = new double[blocksX * blocksY * 3];
        var counts = new int[blocksX * blocksY];
        for (var y = 0; y < h; y++)
        {
            var row = (y0 + y) * width + x0;
            var blockRow = y / size * blocksX;
            for (var x = 0; x < w; x++)
            {
                var block = blockRow + x / size;
                var i = (row + x) * 4;
                sums[block * 3] += pixels[i];
                sums[block * 3 + 1] += pixels[i + 1];
                sums[block * 3 + 2] += pixels[i + 2];
                counts[block]++;
            }
        }
        for (var block = 0; block < counts.Length; block++)
        for (var c = 0; c < 3; c++)
            sums[block * 3 + c] /= counts[block];

        // Каждый блок — среднее с соседями (3×3 в пределах области): штрих буквы размазывается
        // на три блока, и от него остаётся лишь слабое пятно
        var result = new byte[blocksX * blocksY * 4];
        for (var by = 0; by < blocksY; by++)
        for (var bx = 0; bx < blocksX; bx++)
        {
            var n = 0;
            var (b, g, r) = (0.0, 0.0, 0.0);
            for (var ny = Math.Max(0, by - 1); ny <= Math.Min(blocksY - 1, by + 1); ny++)
            for (var nx = Math.Max(0, bx - 1); nx <= Math.Min(blocksX - 1, bx + 1); nx++)
            {
                var j = (ny * blocksX + nx) * 3;
                (b, g, r) = (b + sums[j], g + sums[j + 1], r + sums[j + 2]);
                n++;
            }
            var o = (by * blocksX + bx) * 4;
            (result[o], result[o + 1], result[o + 2], result[o + 3]) =
                ((byte)Math.Round(b / n), (byte)Math.Round(g / n), (byte)Math.Round(r / n), 255);
        }
        return new ObscuredBlocks(result, blocksX, blocksY, size, clamped);
    }
}

/// <summary>
/// Размытая область сеткой блоков: Pixels — BGRA32 размером Columns×Rows (альфа 255), BlockSize —
/// сторона блока в пикселях изображения, Area — область, пересечённая с изображением; блок (0,0)
/// начинается в её левом верхнем углу, крайние блоки могут выходить за неё.
/// </summary>
public sealed record ObscuredBlocks(byte[] Pixels, int Columns, int Rows, int BlockSize, RectI Area);
