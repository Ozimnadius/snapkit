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

    private const double MarkerOpacity = 0.35;
    private const double SemiTransparentOpacity = 0.3;
    // Размытие без исходника (не должно случаться) — просто серый прямоугольник
    private static readonly Brush NoSourceBlur = Palette.Brush("#808080", 0.6);

    /// <summary>
    /// Все фигуры, затем кружки заметок поверх. letter == null — у кружков только номер
    /// (миниатюра отправленного снимка). source — пиксели снимка для размытия.
    /// </summary>
    public static void DrawAll(DrawingContext dc, IEnumerable<Annotation> annotations, char? letter, BlurSource? source = null)
    {
        var list = annotations as IReadOnlyList<Annotation> ?? annotations.ToList();
        foreach (var annotation in list) Draw(dc, annotation, source);
        var number = 0;
        foreach (var annotation in list)
        {
            if (!annotation.HasNote) continue;
            number++;
            var label = letter is { } l ? NoteNumbers.Label(l, number) : number.ToString();
            DrawPin(dc, AnnotationGeometry.PinCenter(annotation), label);
        }
    }

    public static void Draw(DrawingContext dc, Annotation a, BlurSource? source = null)
    {
        var brush = Palette.Brush(a.Color);
        switch (a.Kind)
        {
            case AnnotationKind.Rectangle or AnnotationKind.RoundedRectangle or AnnotationKind.Ellipse:
                DrawBoxShape(dc, a, brush, source);
                break;
            case AnnotationKind.Blur:
                DrawBlur(dc, BoundsRect(a), source);
                break;
            case AnnotationKind.Arrow:
                DrawArrowHeadAndShaft(dc, brush, Pen(brush, a.Thickness, a.LineStyle), a.Thickness, [a.Points[0], a.Points[1]]);
                break;
            case AnnotationKind.CurvedArrow:
                DrawArrowHeadAndShaft(dc, brush, Pen(brush, a.Thickness, a.LineStyle), a.Thickness, AnnotationGeometry.CurvePoints(a, 24));
                break;
            case AnnotationKind.WideArrow:
                DrawWideArrow(dc, brush, a.Thickness, a.Points[0], a.Points[1]);
                break;
            case AnnotationKind.Line:
                dc.DrawLine(Pen(brush, a.Thickness, a.LineStyle), ToPoint(a.Points[0]), ToPoint(a.Points[1]));
                break;
            case AnnotationKind.Pencil:
                dc.DrawGeometry(null, Pen(brush, a.Thickness, a.LineStyle), Polyline(a.Points, closed: false));
                break;
            case AnnotationKind.Marker:
                dc.DrawGeometry(null, MarkerPen(Palette.Brush(a.Color, MarkerOpacity), AnnotationGeometry.MarkerWidth(a.Thickness)),
                    Polyline(a.Points, closed: false));
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

    // Рамка, скруглённая рамка или овал: сначала заливка, потом контур
    private static void DrawBoxShape(DrawingContext dc, Annotation a, Brush stroke, BlurSource? source)
    {
        var rect = BoundsRect(a);
        Geometry shape = a.Kind switch
        {
            AnnotationKind.Ellipse => new EllipseGeometry(rect),
            AnnotationKind.RoundedRectangle => new RectangleGeometry(rect, CornerRadius(a, rect), CornerRadius(a, rect)),
            _ => new RectangleGeometry(rect),
        };
        shape.Freeze();

        var fillHex = a.FillColor ?? a.Color;
        switch (a.Fill)
        {
            case FillMode.Solid:
                dc.DrawGeometry(Palette.Brush(fillHex), null, shape);
                break;
            case FillMode.SemiTransparent:
                dc.DrawGeometry(Palette.Brush(fillHex, SemiTransparentOpacity), null, shape);
                break;
            case FillMode.Blur:
                dc.PushClip(shape);
                DrawBlur(dc, rect, source);
                dc.Pop();
                break;
        }
        dc.DrawGeometry(null, Pen(stroke, a.Thickness, a.LineStyle), shape);
    }

    private static double CornerRadius(Annotation a, Rect rect) => Math.Min(12 + a.Thickness, Math.Min(rect.Width, rect.Height) / 2);

    // Размытая копия снимка под областью rect (в координатах фигур)
    private static void DrawBlur(DrawingContext dc, Rect rect, BlurSource? source)
    {
        if (source is null)
        {
            dc.DrawRectangle(NoSourceBlur, null, rect);
            return;
        }
        var area = new RectI(
            (int)Math.Floor(rect.X - source.Origin.X), (int)Math.Floor(rect.Y - source.Origin.Y),
            (int)Math.Ceiling(rect.Width) + 1, (int)Math.Ceiling(rect.Height) + 1);
        var x0 = Math.Max(0, area.X);
        var y0 = Math.Max(0, area.Y);
        if (ObscuredImages.Get(source, area) is not { } obscured) return;
        dc.PushClip(new RectangleGeometry(rect));
        dc.DrawImage(obscured, new Rect(x0 + source.Origin.X, y0 + source.Origin.Y, obscured.PixelWidth, obscured.PixelHeight));
        dc.Pop();
    }

    /// <summary>Стрелка по ломаной: линия до основания наконечника, затем залитый наконечник.</summary>
    private static void DrawArrowHeadAndShaft(DrawingContext dc, Brush brush, Pen pen, double thickness, IReadOnlyList<PointD> path)
    {
        var to = path[^1];
        var length = 0.0;
        for (var i = 1; i < path.Count; i++) length += Dist(path[i - 1], path[i]);
        if (length < 1) return;

        // Наконечник не длиннее самой стрелки
        var headLength = Math.Min(Math.Max(12, thickness * 3.5), length);
        // Последняя точка пути, отстоящая от конца хотя бы на длину наконечника, задаёт направление
        var anchor = path.Count - 2;
        while (anchor > 0 && Dist(path[anchor], to) < headLength) anchor--;
        var direction = Dist(path[anchor], to);
        if (direction < 1e-6) return;
        var ux = (to.X - path[anchor].X) / direction; var uy = (to.Y - path[anchor].Y) / direction;
        var headHalfWidth = headLength * 0.5;
        var baseX = to.X - ux * headLength; var baseY = to.Y - uy * headLength;

        var shaft = path.Take(anchor + 1).Append(new PointD(baseX, baseY)).ToList();
        dc.DrawGeometry(null, pen, Polyline(shaft, closed: false));

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

    /// <summary>Широкая стрелка: залитый клин от тонкого начала к широкому наконечнику.</summary>
    private static void DrawWideArrow(DrawingContext dc, Brush brush, double thickness, PointD from, PointD to)
    {
        var length = Dist(from, to);
        if (length < 1) return;
        var (ux, uy) = ((to.X - from.X) / length, (to.Y - from.Y) / length);
        var (nx, ny) = (-uy, ux);
        var headHalf = AnnotationGeometry.WideArrowHeadHalfWidth(thickness);
        var headLength = Math.Min(length * 0.6, headHalf * 1.6);
        var shaftHalf = headHalf * 0.45;
        var tailHalf = thickness / 2;
        var (bx, by) = (to.X - ux * headLength, to.Y - uy * headLength);

        PointD[] outline =
        [
            new(from.X + nx * tailHalf, from.Y + ny * tailHalf),
            new(bx + nx * shaftHalf, by + ny * shaftHalf),
            new(bx + nx * headHalf, by + ny * headHalf),
            to,
            new(bx - nx * headHalf, by - ny * headHalf),
            new(bx - nx * shaftHalf, by - ny * shaftHalf),
            new(from.X - nx * tailHalf, from.Y - ny * tailHalf),
        ];
        dc.DrawGeometry(brush, null, Polyline(outline, closed: true));
    }

    private static StreamGeometry Polyline(IReadOnlyList<PointD> points, bool closed)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(ToPoint(points[0]), isFilled: closed, isClosed: closed);
            for (var i = 1; i < points.Count; i++) g.LineTo(ToPoint(points[i]), isStroked: true, isSmoothJoin: true);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Rect BoundsRect(Annotation a)
    {
        var (x, y, w, h) = AnnotationGeometry.Bounds(a);
        return new Rect(x, y, w, h);
    }

    private static Point ToPoint(PointD p) => new(p.X, p.Y);

    private static double Dist(PointD a, PointD b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static Pen Pen(Brush brush, double thickness, LineStyle style = LineStyle.Solid)
    {
        var pen = new Pen(brush, thickness) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        switch (style)
        {
            // Длины штрихов в WPF задаются в толщинах линии
            case LineStyle.Dashed:
                pen.DashStyle = new DashStyle([3, 2], 0);
                pen.DashCap = PenLineCap.Flat;
                break;
            case LineStyle.Dotted:
                pen.DashStyle = new DashStyle([0, 2], 0);
                pen.DashCap = PenLineCap.Round;
                break;
        }
        pen.Freeze();
        return pen;
    }

    private static Pen MarkerPen(Brush brush, double width)
    {
        var pen = new Pen(brush, width) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        pen.Freeze();
        return pen;
    }

    private static FormattedText Text(string text, Typeface face, double size, Brush brush)
        => new(text, CultureInfo.GetCultureInfo("ru-RU"), FlowDirection.LeftToRight, face, size, brush, pixelsPerDip: 1.0);
}
