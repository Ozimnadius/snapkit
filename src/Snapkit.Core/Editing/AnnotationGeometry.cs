using System.Collections.Immutable;
using Snapkit.Core.Model;

namespace Snapkit.Core.Editing;

/// <summary>Геометрия разметки: где кружок заметки, куда попал клик, границы фигуры.</summary>
public static class AnnotationGeometry
{
    public const double PinDiameter = 26;
    private const double PinRadius = PinDiameter / 2;

    // Приближённые размеры текста без измерения шрифта: средняя ширина символа и высота строки.
    private const double CharWidthFactor = 0.6;
    private const double LineHeightFactor = 1.3;

    // На сколько отрезков делится изогнутая стрелка для попадания и границ
    private const int CurveSegments = 24;

    public static double TextFontSize(double thickness) => 12 + thickness * 2;

    /// <summary>Ширина маркера: широкий полупрозрачный выделитель.</summary>
    public static double MarkerWidth(double thickness) => thickness * 4;

    /// <summary>Половина ширины наконечника широкой стрелки.</summary>
    public static double WideArrowHeadHalfWidth(double thickness) => thickness * 1.5 + 6;

    /// <summary>Сколько точек нужно фигуре: изогнутой стрелке — три, тексту и кружку — одна, остальным — две.</summary>
    public static int MinPoints(AnnotationKind kind) => kind switch
    {
        AnnotationKind.CurvedArrow => 3,
        AnnotationKind.Text or AnnotationKind.Pin => 1,
        _ => 2,
    };

    /// <summary>Фигуру можно рисовать: точек хватает, координаты и толщина — обычные числа.</summary>
    public static bool IsWellFormed(Annotation a)
        => Enum.IsDefined(a.Kind)
           && !a.Points.IsDefault
           && a.Points.Length >= MinPoints(a.Kind)
           && a.Points.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y))
           && double.IsFinite(a.Thickness) && a.Thickness > 0
           && (a.PinOffset is not { } offset || (double.IsFinite(offset.X) && double.IsFinite(offset.Y)));

    /// <summary>Фигура задаётся прямоугольником по двум углам.</summary>
    public static bool IsBoxShape(AnnotationKind kind)
        => kind is AnnotationKind.Rectangle or AnnotationKind.RoundedRectangle or AnnotationKind.Ellipse or AnnotationKind.Blur;

    /// <summary>
    /// Центр кружка заметки: левый верхний угол рамки, овала или размытия, начало остальных фигур,
    /// точка привязки текста и кружка — плюс сдвиг, если кружок передвинули.
    /// </summary>
    public static PointD PinCenter(Annotation a)
    {
        var place = IsBoxShape(a.Kind)
            ? new PointD(Math.Min(a.Points[0].X, a.Points[1].X), Math.Min(a.Points[0].Y, a.Points[1].Y))
            : a.Points[0];
        return a.PinOffset is { } offset ? new PointD(place.X + offset.X, place.Y + offset.Y) : place;
    }

    /// <summary>
    /// Точки изогнутой стрелки: квадратичная кривая через начало, точку изгиба (при t = 0.5) и конец.
    /// Возвращает segments + 1 точек.
    /// </summary>
    public static IReadOnlyList<PointD> CurvePoints(Annotation curvedArrow, int segments)
    {
        var (start, bend, end) = (curvedArrow.Points[0], curvedArrow.Points[1], curvedArrow.Points[2]);
        // Контрольная точка такая, чтобы кривая проходила через точку изгиба
        var control = new PointD(2 * bend.X - (start.X + end.X) / 2, 2 * bend.Y - (start.Y + end.Y) / 2);
        var points = new PointD[segments + 1];
        for (var i = 0; i <= segments; i++)
        {
            var t = (double)i / segments;
            var (a, b, c) = ((1 - t) * (1 - t), 2 * t * (1 - t), t * t);
            points[i] = new PointD(a * start.X + b * control.X + c * end.X, a * start.Y + b * control.Y + c * end.Y);
        }
        return points;
    }

    /// <summary>Изгиб новой стрелки: середина, сдвинутая влево от направления на 20 % длины.</summary>
    public static PointD DefaultBend(PointD start, PointD end)
    {
        var (dx, dy) = (end.X - start.X, end.Y - start.Y);
        // Поворот направления на 90° влево (ось Y смотрит вниз), длина 0,2 от отрезка
        return new PointD((start.X + end.X) / 2 + dy * 0.2, (start.Y + end.Y) / 2 - dx * 0.2);
    }

    /// <summary>
    /// Фигура не случайный щелчок: у рамок, овала и размытия обе стороны не меньше minSize,
    /// у линий и стрелок — длина, у карандаша и маркера — хотя бы две точки и длина штриха.
    /// </summary>
    public static bool IsBigEnough(Annotation a, double minSize)
    {
        switch (a.Kind)
        {
            case AnnotationKind.Text or AnnotationKind.Pin:
                return true;
            case AnnotationKind.Pencil or AnnotationKind.Marker:
                return a.Points.Length >= 2 && PolylineLength(a.Points) >= minSize;
            case AnnotationKind.CurvedArrow:
                return a.Points.Length >= 3 && Distance(a.Points[0], a.Points[2]) >= minSize;
            default:
                if (a.Points.Length < 2) return false;
                var (w, h) = (Math.Abs(a.Points[1].X - a.Points[0].X), Math.Abs(a.Points[1].Y - a.Points[0].Y));
                return IsBoxShape(a.Kind) ? w >= minSize && h >= minSize : Math.Sqrt(w * w + h * h) >= minSize;
        }
    }

    public static (double X, double Y, double W, double H) Bounds(Annotation a)
    {
        switch (a.Kind)
        {
            case AnnotationKind.Text:
                var lines = (a.Text ?? "").ReplaceLineEndings("\n").Split('\n');
                var size = TextFontSize(a.Thickness);
                var width = lines.Max(l => l.Length) * size * CharWidthFactor;
                return (a.Points[0].X, a.Points[0].Y, width, lines.Length * size * LineHeightFactor);
            case AnnotationKind.Pin:
                var center = PinCenter(a);
                return (center.X - PinRadius, center.Y - PinRadius, PinDiameter, PinDiameter);
            case AnnotationKind.CurvedArrow:
                return BoundsOf(CurvePoints(a, CurveSegments));
            default:
                return BoundsOf(a.Points);
        }
    }

    /// <summary>
    /// Попал ли клик в фигуру. У рамок и овала без заливки считается только контур — внутри можно
    /// рисовать поверх; с заливкой и у размытия — вся площадь.
    /// </summary>
    public static bool HitTest(Annotation a, PointD p, double tolerance)
    {
        switch (a.Kind)
        {
            case AnnotationKind.Rectangle or AnnotationKind.RoundedRectangle:
            {
                var (x, y, w, h) = Bounds(a);
                var outside = DistanceToRect(p, x, y, w, h);
                if (outside > 0) return outside <= tolerance;
                if (a.Fill != FillMode.None) return true;
                var inside = Math.Min(Math.Min(p.X - x, x + w - p.X), Math.Min(p.Y - y, y + h - p.Y));
                return inside <= tolerance;
            }
            case AnnotationKind.Ellipse:
                return a.Fill != FillMode.None && IsInsideEllipse(a, p) || DistanceToEllipse(a, p) <= tolerance + a.Thickness / 2;
            case AnnotationKind.Blur:
            {
                var (x, y, w, h) = Bounds(a);
                return DistanceToRect(p, x, y, w, h) <= tolerance;
            }
            case AnnotationKind.Arrow or AnnotationKind.Line:
                return DistanceToSegment(p, a.Points[0], a.Points[1]) <= tolerance + a.Thickness / 2;
            case AnnotationKind.WideArrow:
                return DistanceToSegment(p, a.Points[0], a.Points[1]) <= tolerance + WideArrowHeadHalfWidth(a.Thickness);
            case AnnotationKind.CurvedArrow:
                return DistanceToPolyline(p, CurvePoints(a, CurveSegments)) <= tolerance + a.Thickness / 2;
            case AnnotationKind.Pencil:
                return DistanceToPolyline(p, a.Points) <= tolerance + a.Thickness / 2;
            case AnnotationKind.Marker:
                return DistanceToPolyline(p, a.Points) <= tolerance + MarkerWidth(a.Thickness) / 2;
            case AnnotationKind.Pin:
                return Distance(p, PinCenter(a)) <= PinRadius + tolerance;
            case AnnotationKind.Text:
            {
                var (x, y, w, h) = Bounds(a);
                return DistanceToRect(p, x, y, w, h) <= tolerance;
            }
            default:
                return false;
        }
    }

    /// <summary>Верхняя фигура под точкой (последняя в списке). Кружок заметки тоже считается попаданием.</summary>
    public static Guid? TopmostHit(IReadOnlyList<Annotation> items, PointD p, double tolerance)
    {
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var a = items[i];
            if (HitTest(a, p, tolerance)) return a.Id;
            if (a.HasNote && Distance(p, PinCenter(a)) <= PinRadius + tolerance) return a.Id;
        }
        return null;
    }

    /// <summary>
    /// Границы снимка поменялись: координаты фигур отсчитываются от левого верхнего угла области,
    /// поэтому их сдвигаем обратно, чтобы на экране они остались на месте.
    /// </summary>
    public static ImmutableList<Annotation> ShiftForRegionChange(ImmutableList<Annotation> items, RectI oldRegion, RectI newRegion)
    {
        double dx = oldRegion.X - newRegion.X, dy = oldRegion.Y - newRegion.Y;
        return dx == 0 && dy == 0 ? items : items.Select(a => Translate(a, dx, dy)).ToImmutableList();
    }

    /// <summary>
    /// Кружки заметок, которые выходят за край снимка width × height, сдвигаются внутрь целиком.
    /// Сами фигуры не меняются; если снимок меньше кружка — кружок встаёт в середину.
    /// </summary>
    public static ImmutableList<Annotation> KeepPinsInside(ImmutableList<Annotation> items, int width, int height)
    {
        return items.Select(a =>
        {
            if (!a.HasNote) return a;
            var center = PinCenter(a);
            var inside = new PointD(Fit(center.X, width), Fit(center.Y, height));
            return inside == center ? a : AnnotationHandles.WithPinAt(a, inside);
        }).ToImmutableList();

        static double Fit(double v, int size) => size < PinDiameter ? size / 2.0 : Math.Clamp(v, PinRadius, size - PinRadius);
    }

    /// <summary>
    /// Разметка обрезанного снимка: фигуры сдвигаются в координаты области crop, кружки остаются внутри.
    /// Фигуры не удаляются — то, что за краем, обрежет сама картинка.
    /// </summary>
    public static ImmutableList<Annotation> Crop(ImmutableList<Annotation> items, RectI crop)
        => KeepPinsInside(items.Select(a => Translate(a, -crop.X, -crop.Y)).ToImmutableList(), crop.Width, crop.Height);

    /// <summary>Сдвинуть фигуру; кружок заметки едет вместе с ней (PinOffset не меняется).</summary>
    public static Annotation Translate(Annotation a, double dx, double dy)
        => a with { Points = [.. a.Points.Select(pt => new PointD(pt.X + dx, pt.Y + dy))] };

    private static (double X, double Y, double W, double H) BoundsOf(IReadOnlyList<PointD> points)
    {
        var minX = points.Min(p => p.X); var minY = points.Min(p => p.Y);
        return (minX, minY, points.Max(p => p.X) - minX, points.Max(p => p.Y) - minY);
    }

    private static double Distance(PointD a, PointD b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static double PolylineLength(IReadOnlyList<PointD> points)
    {
        var length = 0.0;
        for (var i = 1; i < points.Count; i++) length += Distance(points[i - 1], points[i]);
        return length;
    }

    // Расстояние от точки до прямоугольника снаружи; 0, если точка внутри.
    private static double DistanceToRect(PointD p, double x, double y, double w, double h)
    {
        var dx = Math.Max(Math.Max(x - p.X, 0), p.X - (x + w));
        var dy = Math.Max(Math.Max(y - p.Y, 0), p.Y - (y + h));
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double DistanceToSegment(PointD p, PointD a, PointD b)
    {
        var abx = b.X - a.X; var aby = b.Y - a.Y;
        var lengthSquared = abx * abx + aby * aby;
        if (lengthSquared == 0) return Distance(p, a);
        var t = Math.Clamp(((p.X - a.X) * abx + (p.Y - a.Y) * aby) / lengthSquared, 0, 1);
        return Distance(p, new PointD(a.X + t * abx, a.Y + t * aby));
    }

    private static double DistanceToPolyline(PointD p, IReadOnlyList<PointD> points)
    {
        if (points.Count == 1) return Distance(p, points[0]);
        var best = double.MaxValue;
        for (var i = 1; i < points.Count; i++) best = Math.Min(best, DistanceToSegment(p, points[i - 1], points[i]));
        return best;
    }

    // Нормированное расстояние от центра овала: 1 — на контуре
    private static (double Ratio, double FromCenter, double MinRadius) EllipseMeasure(Annotation a, PointD p)
    {
        var (x, y, w, h) = Bounds(a);
        var (rx, ry) = (w / 2, h / 2);
        var (cx, cy) = (x + rx, y + ry);
        var (dx, dy) = (p.X - cx, p.Y - cy);
        var ratio = Math.Sqrt(Sq(dx / Math.Max(rx, 1e-9)) + Sq(dy / Math.Max(ry, 1e-9)));
        return (ratio, Math.Sqrt(dx * dx + dy * dy), Math.Min(rx, ry));
    }

    private static bool IsInsideEllipse(Annotation a, PointD p) => EllipseMeasure(a, p).Ratio <= 1;

    // Расстояние до контура вдоль луча из центра — точно на осях, близко к правде в остальных местах
    private static double DistanceToEllipse(Annotation a, PointD p)
    {
        var (ratio, fromCenter, minRadius) = EllipseMeasure(a, p);
        return ratio < 1e-9 ? minRadius : fromCenter * Math.Abs(1 - 1 / ratio);
    }

    private static double Sq(double v) => v * v;
}
