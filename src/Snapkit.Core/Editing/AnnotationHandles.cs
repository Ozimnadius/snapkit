using Snapkit.Core.Model;

namespace Snapkit.Core.Editing;

/// <summary>
/// Маркеры изменения размера выделенной фигуры и перемещение кружка заметки.
/// У рамок, овала и размытия 8 маркеров по часовой с левого верхнего угла: СЗ, С, СВ, В, ЮВ, Ю, ЮЗ, З.
/// У линии и прямых стрелок — начало и конец, у изогнутой стрелки — начало, изгиб и конец.
/// Текст, кружок, карандаш и маркер только перемещаются.
/// </summary>
public static class AnnotationHandles
{
    public static IReadOnlyList<PointD> Of(Annotation a)
    {
        if (AnnotationGeometry.IsBoxShape(a.Kind))
        {
            var (l, t, r, b) = Edges(a);
            var (cx, cy) = ((l + r) / 2, (t + b) / 2);
            return [new(l, t), new(cx, t), new(r, t), new(r, cy), new(r, b), new(cx, b), new(l, b), new(l, cy)];
        }
        return a.Kind is AnnotationKind.Line or AnnotationKind.Arrow or AnnotationKind.WideArrow or AnnotationKind.CurvedArrow
            ? a.Points.ToArray()
            : [];
    }

    /// <summary>Номер маркера под точкой или null.</summary>
    public static int? Hit(Annotation a, PointD p, double radius)
    {
        var handles = Of(a);
        for (var i = 0; i < handles.Count; i++)
        {
            if (Math.Abs(handles[i].X - p.X) <= radius && Math.Abs(handles[i].Y - p.Y) <= radius) return i;
        }
        return null;
    }

    /// <summary>Перетащить маркер. У рамок углы потом нормализуются: фигура не выворачивается наизнанку.</summary>
    public static Annotation Move(Annotation a, int handle, PointD to)
    {
        if (!AnnotationGeometry.IsBoxShape(a.Kind))
        {
            if (handle < 0 || handle >= a.Points.Length) return a;
            return a with { Points = a.Points.SetItem(handle, to) };
        }

        var (l, t, r, b) = Edges(a);
        switch (handle)
        {
            case 0: l = to.X; t = to.Y; break;
            case 1: t = to.Y; break;
            case 2: r = to.X; t = to.Y; break;
            case 3: r = to.X; break;
            case 4: r = to.X; b = to.Y; break;
            case 5: b = to.Y; break;
            case 6: l = to.X; b = to.Y; break;
            case 7: l = to.X; break;
            default: return a;
        }
        return a with { Points = [new PointD(Math.Min(l, r), Math.Min(t, b)), new PointD(Math.Max(l, r), Math.Max(t, b))] };
    }

    /// <summary>Попал ли щелчок в кружок заметки (у фигуры без заметки кружка нет).</summary>
    public static bool HitPin(Annotation a, PointD p, double tolerance)
    {
        if (!a.HasNote) return false;
        var c = AnnotationGeometry.PinCenter(a);
        var (dx, dy) = (p.X - c.X, p.Y - c.Y);
        return Math.Sqrt(dx * dx + dy * dy) <= AnnotationGeometry.PinDiameter / 2 + tolerance;
    }

    /// <summary>
    /// Что хватает щелчок инструмента «Выбор»: кружок заметки любой фигуры (отдельный кружок двигается
    /// как фигура), затем маркер размера выделенной фигуры, затем сама фигура; null — пусто. Кружок
    /// первым: он рисуется поверх маркеров и у рамок стоит на маркере левого верхнего угла — тот угол
    /// всё равно можно тянуть маркерами верхней и левой сторон.
    /// </summary>
    public static SelectTarget? PickForSelect(IReadOnlyList<Annotation> items, Guid? selectedId, PointD p, double handleRadius, double tolerance)
    {
        for (var i = items.Count - 1; i >= 0; i--)
        {
            if (items[i].Kind != AnnotationKind.Pin && HitPin(items[i], p, tolerance))
                return new SelectTarget(SelectTargetKind.Pin, items[i].Id);
        }
        if (selectedId is { } id && items.FirstOrDefault(a => a.Id == id) is { } selected && Hit(selected, p, handleRadius) is { } handle)
            return new SelectTarget(SelectTargetKind.Handle, selected.Id, handle);
        return AnnotationGeometry.TopmostHit(items, p, tolerance) is { } hit ? new SelectTarget(SelectTargetKind.Shape, hit) : null;
    }

    /// <summary>Поставить кружок заметки в точку center, не двигая саму фигуру.</summary>
    public static Annotation WithPinAt(Annotation a, PointD center)
    {
        var place = AnnotationGeometry.PinCenter(a with { PinOffset = null });
        return a with { PinOffset = new PointD(center.X - place.X, center.Y - place.Y) };
    }

    private static (double L, double T, double R, double B) Edges(Annotation a)
    {
        var (p, q) = (a.Points[0], a.Points[1]);
        return (Math.Min(p.X, q.X), Math.Min(p.Y, q.Y), Math.Max(p.X, q.X), Math.Max(p.Y, q.Y));
    }
}

public enum SelectTargetKind { Handle, Pin, Shape }

/// <summary>Цель щелчка «Выбора»: маркер Handle фигуры Id, её кружок заметки или она сама.</summary>
public readonly record struct SelectTarget(SelectTargetKind Kind, Guid Id, int Handle = 0);
