using System.Windows;
using System.Windows.Media.Imaging;

namespace Snapkit.App.Shell;

/// <summary>Значок программы из ресурсов (Assets/snapkit.ico) нужного размера.</summary>
internal static class AppIcon
{
    private static readonly Uri IconUri = new("pack://application:,,,/Assets/snapkit.ico");

    /// <summary>Кадр значка для WPF: наименьший, который не меньше нужного размера в пикселях.</summary>
    public static BitmapSource Frame(int pixelSize)
    {
        using var stream = Open();
        var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frames = decoder.Frames.OrderBy(f => f.PixelWidth).ToList();
        return frames.FirstOrDefault(f => f.PixelWidth >= pixelSize) ?? frames[^1];
    }

    /// <summary>Значок для трея (System.Drawing) нужного размера.</summary>
    public static System.Drawing.Icon TrayIcon(System.Drawing.Size size)
    {
        using var stream = Open();
        return new System.Drawing.Icon(stream, size);
    }

    private static System.IO.Stream Open()
        => Application.GetResourceStream(IconUri)?.Stream
           ?? throw new InvalidOperationException("Не найден значок программы в ресурсах.");
}
