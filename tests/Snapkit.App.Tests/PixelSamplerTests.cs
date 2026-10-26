using Snapkit.App.Imaging;
using Snapkit.Core;

namespace Snapkit.App.Tests;

public class PixelSamplerTests
{
    // Картинка 3×2: верхний ряд красный, нижний синий
    private static readonly System.Windows.Media.Imaging.BitmapSource Image = ImageCodecsTests.RedOverBlue();

    [Fact]
    public void HexAt_ReturnsPixelColor() => StaRunner.Run(() =>
    {
        Assert.Equal("#FF0000", PixelSampler.HexAt(Image, new PointD(0, 0), new PointD(1, 0)));
        Assert.Equal("#0000FF", PixelSampler.HexAt(Image, new PointD(0, 0), new PointD(2.7, 1.2)));
    });

    [Fact]
    public void HexAt_RespectsOrigin() => StaRunner.Run(()
        => Assert.Equal("#0000FF", PixelSampler.HexAt(Image, new PointD(100, 50), new PointD(101, 51))));

    [Fact]
    public void HexAt_OutsideImage_IsNull() => StaRunner.Run(() =>
    {
        Assert.Null(PixelSampler.HexAt(Image, new PointD(0, 0), new PointD(3, 0)));
        Assert.Null(PixelSampler.HexAt(Image, new PointD(100, 50), new PointD(99, 50)));
    });
}
