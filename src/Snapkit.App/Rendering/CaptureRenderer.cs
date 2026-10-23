using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.App.Imaging;
using Snapkit.Core;
using Snapkit.Core.Editing;
using Snapkit.Core.Model;

namespace Snapkit.App.Rendering;

/// <summary>Итоговая картинка снимка: исходник, фигуры, затем кружки заметок с метками A1, A2…</summary>
public static class CaptureRenderer
{
    public static BitmapSource Render(BitmapSource source, Capture capture, char letter)
        => RenderScaled(source, capture, letter, 1.0);

    /// <summary>Уменьшенная копия для ленты; letter == null — кружки без меток (отправленный снимок).</summary>
    public static BitmapSource RenderThumbnail(BitmapSource source, Capture capture, char? letter, int maxWidth)
        => RenderScaled(source, capture, letter, Math.Min(1.0, (double)maxWidth / capture.PixelWidth));

    private static BitmapSource RenderScaled(BitmapSource source, Capture capture, char? letter, double scale)
    {
        var width = Math.Max(1, (int)Math.Round(capture.PixelWidth * scale));
        var height = Math.Max(1, (int)Math.Round(capture.PixelHeight * scale));

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.DrawImage(source, new Rect(0, 0, capture.PixelWidth, capture.PixelHeight));
            foreach (var annotation in capture.Annotations) AnnotationPainter.Draw(dc, annotation);
            foreach (var (annotation, number) in NoteNumbers.Number(capture))
            {
                var label = letter is { } l ? NoteNumbers.Label(l, number) : number.ToString();
                AnnotationPainter.DrawPin(dc, AnnotationGeometry.PinCenter(annotation), label);
            }
            dc.Pop();
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        return ImageCodecs.Normalize(target);
    }
}
