using Snapkit.Core.Imaging;

namespace Snapkit.Core.Tests;

public class ImageFiltersTests
{
    private static byte[] Image(int width, int height, Func<int, int, (byte B, byte G, byte R)> color)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var (b, g, r) = color(x, y);
            var i = (y * width + x) * 4;
            (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (b, g, r, 255);
        }
        return pixels;
    }

    // Наибольший разброс значений по каждому из трёх цветовых каналов
    private static int Spread(byte[] pixels)
    {
        var spread = 0;
        for (var channel = 0; channel < 3; channel++)
        {
            var values = Enumerable.Range(0, pixels.Length / 4).Select(i => pixels[i * 4 + channel]).ToList();
            spread = Math.Max(spread, values.Max() - values.Min());
        }
        return spread;
    }

    [Fact]
    public void Obscure_UniformArea_KeepsColor()
    {
        var (pixels, area) = ImageFilters.ObscureArea(Image(30, 30, (_, _) => (10, 20, 30)), 30, 30, new RectI(0, 0, 30, 30));
        Assert.Equal(new RectI(0, 0, 30, 30), area);
        Assert.All(Enumerable.Range(0, pixels.Length / 4), i =>
            Assert.Equal((10, 20, 30, 255), (pixels[i * 4], pixels[i * 4 + 1], pixels[i * 4 + 2], pixels[i * 4 + 3])));
    }

    [Fact]
    public void Obscure_Checkerboard_BecomesNearlyFlat()
    {
        var board = Image(48, 48, (x, y) => (x + y) % 2 == 0 ? ((byte)0, (byte)0, (byte)0) : ((byte)255, (byte)255, (byte)255));
        var (pixels, _) = ImageFilters.ObscureArea(board, 48, 48, new RectI(0, 0, 48, 48));
        Assert.True(Spread(pixels) < 40, $"разброс {Spread(pixels)}");
    }

    [Fact]
    public void Obscure_Stripes_LoseContrast()
    {
        // Вертикальные полосы по 2 px — как штрихи букв мелкого текста
        var stripes = Image(48, 48, (x, _) => x / 2 % 2 == 0 ? ((byte)0, (byte)0, (byte)0) : ((byte)255, (byte)255, (byte)255));
        var (pixels, _) = ImageFilters.ObscureArea(stripes, 48, 48, new RectI(0, 0, 48, 48));
        Assert.True(Spread(pixels) < 60, $"разброс {Spread(pixels)}");
    }

    [Fact]
    public void Obscure_AreaPartlyOutside_IsClamped()
    {
        var (pixels, area) = ImageFilters.ObscureArea(Image(20, 20, (_, _) => (1, 2, 3)), 20, 20, new RectI(-10, -10, 30, 30));
        Assert.Equal(new RectI(0, 0, 20, 20), area);
        Assert.Equal(20 * 20 * 4, pixels.Length);
    }

    [Fact]
    public void Obscure_NoIntersection_ReturnsEmpty()
    {
        var (pixels, area) = ImageFilters.ObscureArea(Image(20, 20, (_, _) => (1, 2, 3)), 20, 20, new RectI(30, 30, 10, 10));
        Assert.Empty(pixels);
        Assert.Equal(0, area.Width * area.Height);
    }

    [Fact]
    public void Obscure_DoesNotChangeInput()
    {
        var input = Image(24, 24, (x, y) => ((byte)(x * 10), (byte)(y * 10), 0));
        var copy = input.ToArray();
        ImageFilters.ObscureArea(input, 24, 24, new RectI(2, 2, 20, 20));
        Assert.Equal(copy, input);
    }

    [Fact]
    public void Obscure_SubArea_ReturnsOnlyThatArea()
    {
        var (pixels, area) = ImageFilters.ObscureArea(Image(40, 30, (_, _) => (5, 5, 5)), 40, 30, new RectI(10, 5, 17, 9));
        Assert.Equal(new RectI(10, 5, 17, 9), area);
        Assert.Equal(17 * 9 * 4, pixels.Length);
    }
}
