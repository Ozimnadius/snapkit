namespace Snapkit.Core.Editing;

public enum SelectionHandle { None, Move, North, South, East, West, NorthEast, NorthWest, SouthEast, SouthWest }

/// <summary>
/// Рамка выделяемой области на одном мониторе: новое выделение, перемещение, растягивание за маркеры.
/// Координаты — физические пиксели внутри монитора; рамка не выходит за его границы.
/// </summary>
public sealed class RegionSelection(RectI bounds)
{
    /// <summary>Выделение меньше этого размера не считается.</summary>
    public const int MinSize = 4;

    private SelectionHandle _handle;
    private PointD _anchor;
    private RectI _startRegion;

    public RectI? Region { get; private set; }
    public bool IsDragging { get; private set; }

    /// <summary>Начать перетаскивание: None — новое выделение, иначе — правка текущей рамки.</summary>
    public void Begin(PointD point, SelectionHandle handle = SelectionHandle.None)
    {
        IsDragging = true;
        _anchor = Clamp(point);
        _handle = Region is null ? SelectionHandle.None : handle;
        if (_handle == SelectionHandle.None) Region = null;
        else _startRegion = Region!.Value;
    }

    public void Drag(PointD point)
    {
        if (!IsDragging) return;
        var p = Clamp(point);
        Region = _handle switch
        {
            SelectionHandle.None => FromEdges(_anchor.X, _anchor.Y, p.X, p.Y),
            SelectionHandle.Move => Moved(point),
            _ => Resized(p),
        };
    }

    /// <summary>
    /// Закончить перетаскивание. Слишком маленькое новое выделение сбрасывается,
    /// а слишком сжатая существующая рамка возвращается к прежнему размеру.
    /// </summary>
    public void End()
    {
        IsDragging = false;
        if (Region is { } r && (r.Width < MinSize || r.Height < MinSize))
            Region = _handle == SelectionHandle.None ? null : _startRegion;
    }

    /// <summary>Выделить весь монитор (снимок всего экрана).</summary>
    public void SelectAll() => Region = bounds;

    /// <summary>Какой маркер рамки под точкой: углы, затем стороны, затем внутренность (перемещение).</summary>
    public SelectionHandle HitHandle(PointD p, double radius)
    {
        if (Region is not { } r) return SelectionHandle.None;
        bool Near(double a, double b) => Math.Abs(a - b) <= radius;
        bool Within(double v, double from, double to) => v >= from - radius && v <= to + radius;

        var (left, top, right, bottom) = ((double)r.X, (double)r.Y, (double)r.Right, (double)r.Bottom);
        if (Near(p.X, left) && Near(p.Y, top)) return SelectionHandle.NorthWest;
        if (Near(p.X, right) && Near(p.Y, top)) return SelectionHandle.NorthEast;
        if (Near(p.X, left) && Near(p.Y, bottom)) return SelectionHandle.SouthWest;
        if (Near(p.X, right) && Near(p.Y, bottom)) return SelectionHandle.SouthEast;
        if (Near(p.Y, top) && Within(p.X, left, right)) return SelectionHandle.North;
        if (Near(p.Y, bottom) && Within(p.X, left, right)) return SelectionHandle.South;
        if (Near(p.X, left) && Within(p.Y, top, bottom)) return SelectionHandle.West;
        if (Near(p.X, right) && Within(p.Y, top, bottom)) return SelectionHandle.East;
        if (p.X > left && p.X < right && p.Y > top && p.Y < bottom) return SelectionHandle.Move;
        return SelectionHandle.None;
    }

    private RectI Moved(PointD rawPoint)
    {
        var r = _startRegion;
        var x = (int)Math.Round(r.X + rawPoint.X - _anchor.X);
        var y = (int)Math.Round(r.Y + rawPoint.Y - _anchor.Y);
        x = Math.Clamp(x, bounds.X, bounds.Right - r.Width);
        y = Math.Clamp(y, bounds.Y, bounds.Bottom - r.Height);
        return r with { X = x, Y = y };
    }

    private RectI Resized(PointD p)
    {
        double left = _startRegion.X, top = _startRegion.Y, right = _startRegion.Right, bottom = _startRegion.Bottom;
        switch (_handle)
        {
            case SelectionHandle.North: top = p.Y; break;
            case SelectionHandle.South: bottom = p.Y; break;
            case SelectionHandle.West: left = p.X; break;
            case SelectionHandle.East: right = p.X; break;
            case SelectionHandle.NorthWest: left = p.X; top = p.Y; break;
            case SelectionHandle.NorthEast: right = p.X; top = p.Y; break;
            case SelectionHandle.SouthWest: left = p.X; bottom = p.Y; break;
            case SelectionHandle.SouthEast: right = p.X; bottom = p.Y; break;
        }
        return FromEdges(left, top, right, bottom);
    }

    private static RectI FromEdges(double x1, double y1, double x2, double y2)
    {
        var left = (int)Math.Round(Math.Min(x1, x2));
        var top = (int)Math.Round(Math.Min(y1, y2));
        var right = (int)Math.Round(Math.Max(x1, x2));
        var bottom = (int)Math.Round(Math.Max(y1, y2));
        return new RectI(left, top, right - left, bottom - top);
    }

    private PointD Clamp(PointD p)
        => new(Math.Clamp(p.X, bounds.X, bounds.Right), Math.Clamp(p.Y, bounds.Y, bounds.Bottom));
}
