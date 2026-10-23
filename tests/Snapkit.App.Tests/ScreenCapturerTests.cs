using System.Windows.Media;
using Snapkit.App.Capturing;
using Snapkit.Core;

namespace Snapkit.App.Tests;

/// <summary>Тесты работают с настоящим экраном — запускаются только на рабочем столе.</summary>
public class ScreenCapturerTests
{
    [Fact]
    public void GetMonitors_HasExactlyOnePrimaryWithSaneValues()
    {
        var monitors = ScreenCapturer.GetMonitors();
        Assert.NotEmpty(monitors);
        Assert.Single(monitors, m => m.IsPrimary);
        Assert.All(monitors, m =>
        {
            Assert.True(m.Bounds.Width > 0 && m.Bounds.Height > 0);
            Assert.True(m.DpiScale >= 1.0);
            Assert.NotEqual(0, m.Handle);
        });
    }

    [Fact]
    public void Capture_ReturnsRequestedSizeOpaqueBgra32At96Dpi() => StaRunner.Run(() =>
    {
        var primary = ScreenCapturer.GetMonitors().Single(m => m.IsPrimary);
        var region = new RectI(primary.Bounds.X + 10, primary.Bounds.Y + 10, 40, 30);
        var image = ScreenCapturer.Capture(region);
        Assert.Equal((40, 30), (image.PixelWidth, image.PixelHeight));
        Assert.Equal(PixelFormats.Bgra32, image.Format);
        Assert.Equal((96d, 96d), (image.DpiX, image.DpiY));
        Assert.True(image.IsFrozen);
        var pixels = ImageCodecsTests.Pixels(image);
        Assert.All(Enumerable.Range(0, pixels.Length / 4), i => Assert.Equal(255, pixels[i * 4 + 3]));
    });
}
