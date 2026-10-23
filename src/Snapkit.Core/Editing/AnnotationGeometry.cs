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

    public static double TextFontSize(double thickness) => 12 + thickness * 2;

    /// <summary>Центр кружка заметки: угол рамки, начало стрелки, точка привязки текста или пина.</summary>
    public static PointD PinCenter(Annotation a) => a.Kind switch
    {
        AnnotationKind.Rectangle => new PointD(Math.Min(a.Points[0].X, a.Points[1].X), Math.Min(a.Points[0].Y, a.Points[1].Y)),
        _ => a.Points[0],
    };

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
                return (a.Points[0].X - PinRadius, a.Points[0].Y - PinRadius, PinDiameter, PinDiameter);
            default:
                var minX = a.Points.Min(p => p.X); var minY = a.Points.Min(p => p.Y);
                return (minX, minY, a.Points.Max(p => p.X) - minX, a.Points.Max(p => p.Y) - minY);
        }
    }

    /// <summary>
    /// Попал ли клик в фигуру. У рамки считается только контур — внутри можно рисовать поверх.
    /// </summary>
    public static bool HitTest(Annotation a, PointD p, double tolerance)
    {
        switch (a.Kind)
        {
            case AnnotationKind.Rectangle:
            {
                var (x, y, w, h) = Bounds(a);
                var outside = DistanceToRect(p, x, y, w, h);
                var inside = Math.Min(Math.Min(p.X - x, x + w - p.X), Math.Min(p.Y - y, y + h - p.Y));
                return outside > 0 ? outside <= tolerance : inside <= tolerance;
            }
            case AnnotationKind.Arrow:
                return DistanceToSegment(p, a.Points[0], a.Points[1]) <= tolerance + a.Thickness / 2;
            case AnnotationKind.Pin:
                return Distance(p, a.Points[0]) <= PinRadius + tolerance;
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

    public static Annotation Translate(Annotation a, double dx, double dy)
        => a with { Points = [.. a.Points.Select(pt => new PointD(pt.X + dx, pt.Y + dy))] };

    private static double Distance(PointD a, PointD b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

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
}
