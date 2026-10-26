using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.App.Strip;
using Snapkit.Core;
using Snapkit.Core.Model;

namespace Snapkit.App.Tests;

/// <summary>Вид ленты: что видно на карточках стопки.</summary>
public class StripWindowTests
{
    private static Capture Cap() => new(
        Guid.NewGuid(), 10, 10, new RectI(0, 0, 10, 10), [], "", false, DateTimeOffset.UnixEpoch);

    private static BitmapSource Red()
    {
        var pixels = new byte[180 * 120 * 4];
        for (var i = 0; i < pixels.Length; i += 4) (pixels[i + 2], pixels[i + 3]) = (255, 255);
        var image = BitmapSource.Create(180, 120, 96, 96, PixelFormats.Bgra32, null, pixels, 180 * 4);
        image.Freeze();
        return image;
    }

    [Fact]
    public void Stripe_ShowsSnapshot() => UiApp.Run(() =>
    {
        // Как у Snapik: по полоске карточки в стопке видно сам снимок, а не тёмную плашку
        var model = new StripViewModel();
        model.Refresh([Cap(), Cap()], (_, _) => Red());
        var window = new StripWindow(model);
        var root = (FrameworkElement)window.Content;
        root.Measure(new Size(window.Width, double.PositiveInfinity));
        root.Arrange(new Rect(root.DesiredSize));
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);

        var first = (FrameworkElement)window.CardsList.ItemContainerGenerator.ContainerFromIndex(0);
        var corner = first.TranslatePoint(new Point(0, 0), root);
        var x = (int)(corner.X + first.ActualWidth * 0.75);
        var y = (int)(corner.Y + StripViewModel.StripeHeight / 2);
        var px = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), px, 4, 0);
        Assert.True(px[2] > 200 && px[1] < 60 && px[0] < 60, $"на полосе цвет R{px[2]} G{px[1]} B{px[0]}, а не снимок");
    });

    [Fact]
    public void Stack_IsCompact()
        => Assert.Equal((96.0, 26.0), (StripViewModel.CardHeight, StripViewModel.StripeHeight));
}
