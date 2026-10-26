using System.Collections.Immutable;

namespace Snapkit.Core.Editing;

/// <summary>Прореживание штриха карандаша и маркера: мышь даёт точку на каждый пиксель, столько не нужно.</summary>
public static class StrokeSimplifier
{
    /// <summary>Первая и последняя точки остаются всегда; точка ближе minStep к последней сохранённой пропускается.</summary>
    public static ImmutableArray<PointD> Simplify(IReadOnlyList<PointD> points, double minStep)
    {
        if (points.Count == 0) return [];
        var result = ImmutableArray.CreateBuilder<PointD>();
        result.Add(points[0]);
        for (var i = 1; i < points.Count - 1; i++)
        {
            var (dx, dy) = (points[i].X - result[^1].X, points[i].Y - result[^1].Y);
            if (Math.Sqrt(dx * dx + dy * dy) >= minStep) result.Add(points[i]);
        }
        if (points.Count > 1) result.Add(points[^1]);
        return result.ToImmutable();
    }
}
