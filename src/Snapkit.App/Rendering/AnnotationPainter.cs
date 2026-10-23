using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Snapkit.Core;
using Snapkit.Core.Editing;
using Snapkit.Core.Model;

namespace Snapkit.App.Rendering;

/// <summary>
/// Рисование разметки. Одна процедура для редактора и для итоговой картинки,
/// поэтому то, что видно в редакторе, ровно то, что уйдёт в чат. Координаты — пиксели снимка.
/// </summary>
public static class AnnotationPainter
{
    private static readonly Typeface TextFace = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Typeface PinFace = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private const double PinFontSize = 12;

    public static void Draw(DrawingContext dc, Annotation a)
    {
        var brush = Palette.Brush(a.Color);
        switch (a.Kind)
        {
            case AnnotationKind.Rectangle:
            {
                var (x, y, w, h) = AnnotationGeometry.Bounds(a);
                dc.DrawRectangle(null, Pen(brush, a.Thickness), new Rect(x, y, w, h));
                break;
            }
            case AnnotationKind.Arrow:
                DrawArrow(dc, brush, a.Thickness, a.Points[0], a.Points[1]);
                break;
            case AnnotationKind.Text when !string.IsNullOrEmpty(a.Text):
                dc.DrawText(Text(a.Text, TextFace, AnnotationGeometry.TextFontSize(a.Thickness), brush),
                    new Point(a.Points[0].X, a.Points[0].Y));
                break;
            // Pin — только кружок, его рисует DrawPin.
        }
    }

    /// <summary>Кружок заметки цвета акцента с белой надписью вида «A1».</summary>
    public static void DrawPin(DrawingContext dc, PointD center, string label)
    {
        var c = new Point(center.X, center.Y);
        var r = AnnotationGeometry.PinDiameter / 2;
        dc.DrawEllipse(Palette.Brush(Palette.Accent), Pen(Brushes.White, 1.5), c, r, r);
        if (label.Length == 0) return;
        var text = Text(label, PinFace, PinFontSize, Brushes.White);
        dc.DrawText(text, new Point(c.X - text.Width / 2, c.Y - text.Height / 2));
    }

    private static void DrawArrow(DrawingContext dc, Brush brush, double thickness, PointD from, PointD to)
    {
        var dx = to.X - from.X; var dy = to.Y - from.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1) return;
        var ux = dx / length; var uy = dy / length;

        // Наконечник не длиннее самой стрелки
        var headLength = Math.Min(Math.Max(12, thickness * 3.5), length);
        var headHalfWidth = headLength * 0.5;
        var baseX = to.X - ux * headLength; var baseY = to.Y - uy * headLength;

        dc.DrawLine(Pen(brush, thickness), new Point(from.X, from.Y), new Point(baseX, baseY));

        var head = new StreamGeometry();
        using (var g = head.Open())
        {
            g.BeginFigure(new Point(to.X, to.Y), isFilled: true, isClosed: true);
            g.LineTo(new Point(baseX - uy * headHalfWidth, baseY + ux * headHalfWidth), isStroked: false, isSmoothJoin: false);
            g.LineTo(new Point(baseX + uy * headHalfWidth, baseY - ux * headHalfWidth), isStroked: false, isSmoothJoin: false);
        }
        head.Freeze();
        dc.DrawGeometry(brush, null, head);
    }

    private static Pen Pen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        return pen;
    }

    private static FormattedText Text(string text, Typeface face, double size, Brush brush)
        => new(text, CultureInfo.GetCultureInfo("ru-RU"), FlowDirection.LeftToRight, face, size, brush, pixelsPerDip: 1.0);
}
