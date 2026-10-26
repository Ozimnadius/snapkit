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
    private static readonly Brush CropShade = Palette.Brush("#000000", 0.5);

    /// <summary>Рамка обрезки, которую сейчас тянут (в координатах монитора).</summary>
    public Rect? CropPreview { get; set; }

    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }

    /// <summary>Пиксели под снимком (для размытия), их место в координатах монитора и показывать ли их как фон.</summary>
    public BitmapSource? Source { get; set; }
    public PointD SourceOrigin { get; set; }
    public bool ShowSource { get; set; }

    public IReadOnlyList<Annotation> Items { get; set; } = [];

    /// <summary>Фигура, которая сейчас рисуется (ещё не в истории).</summary>
    public Annotation? Preview { get; set; }

    public Guid? SelectedId { get; set; }

    /// <summary>Сторона маркера размера в пикселях холста (9 DIP с учётом масштаба монитора).</summary>
    public double HandleSize { get; set; } = 9;

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
        dc.PushTransform(new TranslateTransform(-Offset.X, -Offset.Y));
        // Снимок рисуется 1:1 на своём месте, а не вписывается в холст: если он не поместился
        // на экран или обрезан, край просто не виден, а разметка остаётся там же, где на снимке
        if (ShowSource && Source is not null)
            dc.DrawImage(Source, new Rect(SourceOrigin.X, SourceOrigin.Y, Source.PixelWidth, Source.PixelHeight));

        var items = Preview is null ? Items : Items.Append(Preview).ToList();
        var source = Source is null ? null : new BlurSource(Source, SourceOrigin);
        foreach (var item in items) AnnotationPainter.Draw(dc, item, source);

        if (SelectedId is { } id && Items.FirstOrDefault(a => a.Id == id) is { } selected)
        {
            var (x, y, w, h) = AnnotationGeometry.Bounds(selected);
            var pad = selected.Thickness / 2 + 4;
            dc.DrawRectangle(null, SelectionPen, new Rect(x - pad, y - pad, w + 2 * pad, h + 2 * pad));
            // Маркеры изменения размера: белые квадраты с обводкой цвета акцента
            var handlePen = new Pen(Palette.Brush(Palette.Accent), HandleSize / 6);
            handlePen.Freeze();
            foreach (var handle in AnnotationHandles.Of(selected))
            {
                dc.DrawRectangle(Brushes.White, handlePen,
                    new Rect(handle.X - HandleSize / 2, handle.Y - HandleSize / 2, HandleSize, HandleSize));
            }
        }

        // Кружки поверх маркеров: щелчок по кружку хватает кружок, а не маркер под ним
        AnnotationPainter.DrawPins(dc, items, Letter);
        if (PendingPinId is { } pendingId && Items.FirstOrDefault(a => a.Id == pendingId) is { HasNote: false } pending)
        {
            var next = Items.Count(a => a.HasNote) + 1;
            AnnotationPainter.DrawPin(dc, AnnotationGeometry.PinCenter(pending), $"{Letter}{next}");
        }

        // Будущая обрезка: вне рамки затемнено, рамка пунктиром
        if (CropPreview is { } crop)
        {
            var outer = new RectangleGeometry(new Rect(Offset.X, Offset.Y, PixelWidth, PixelHeight));
            dc.DrawGeometry(CropShade, null, new CombinedGeometry(GeometryCombineMode.Exclude, outer, new RectangleGeometry(crop)));
            dc.DrawRectangle(null, SelectionPen, crop);
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
