using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.App.Rendering;
using Snapkit.Core;
using Snapkit.Core.Editing;
using Snapkit.Core.Model;

namespace Snapkit.App.Editor;

/// <summary>
/// Холст разметки размером со снимок. Его координаты — пиксели снимка
/// (снаружи он уменьшается на масштаб монитора), поэтому рисование здесь совпадает с итоговой картинкой.
/// </summary>
internal sealed class EditorCanvas : FrameworkElement
{
    private static readonly Pen SelectionPen = CreateSelectionPen();

    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    public BitmapSource? Background { get; set; }
    public IReadOnlyList<Annotation> Items { get; set; } = [];

    /// <summary>Фигура, которая сейчас рисуется (ещё не в истории).</summary>
    public Annotation? Preview { get; set; }

    public Guid? SelectedId { get; set; }

    /// <summary>Кружок, к которому сейчас пишут первую заметку: рисуется со следующим номером.</summary>
    public Guid? PendingPinId { get; set; }

    /// <summary>Левый верхний угол области в координатах монитора: фигуры хранятся в них.</summary>
    public PointD Offset { get; set; }
    public char Letter { get; set; } = 'A';

    protected override Size MeasureOverride(Size availableSize) => new(PixelWidth, PixelHeight);

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(0, 0, PixelWidth, PixelHeight);
        // Прозрачная заливка нужна, чтобы холст получал клики по всей площади
        dc.DrawRectangle(Brushes.Transparent, null, bounds);
        dc.PushClip(new RectangleGeometry(bounds));
        if (Background is not null) dc.DrawImage(Background, bounds);
        dc.PushTransform(new TranslateTransform(-Offset.X, -Offset.Y));

        var items = Preview is null ? Items : Items.Append(Preview).ToList();
        AnnotationPainter.DrawAll(dc, items, Letter);
        if (PendingPinId is { } pendingId && Items.FirstOrDefault(a => a.Id == pendingId) is { HasNote: false } pending)
        {
            var next = Items.Count(a => a.HasNote) + 1;
            AnnotationPainter.DrawPin(dc, AnnotationGeometry.PinCenter(pending), $"{Letter}{next}");
        }

        if (SelectedId is { } id && Items.FirstOrDefault(a => a.Id == id) is { } selected)
        {
            var (x, y, w, h) = AnnotationGeometry.Bounds(selected);
            var pad = selected.Thickness / 2 + 4;
            dc.DrawRectangle(null, SelectionPen, new Rect(x - pad, y - pad, w + 2 * pad, h + 2 * pad));
        }
        dc.Pop();
        dc.Pop();
    }

    private static Pen CreateSelectionPen()
    {
        var pen = new Pen(Brushes.White, 1.5) { DashStyle = new DashStyle([4, 3], 0) };
        pen.Freeze();
        return pen;
    }
}
