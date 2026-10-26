using Snapkit.Core.Model;

namespace Snapkit.Core.Editing;

/// <summary>
/// Заливка выделенной фигуры с панели инструментов: меняется только выбранное свойство,
/// второе у фигуры остаётся своим.
/// </summary>
public static class AnnotationStyle
{
    /// <summary>Заливка есть у рамок и овала.</summary>
    public static bool IsFillable(AnnotationKind kind)
        => kind is AnnotationKind.Rectangle or AnnotationKind.RoundedRectangle or AnnotationKind.Ellipse;

    /// <summary>Новый вид заливки; цвет заливки фигуры не меняется.</summary>
    public static Annotation WithFill(Annotation a, FillMode fill)
        => IsFillable(a.Kind) && a.Fill != fill ? a with { Fill = fill } : a;

    /// <summary>
    /// Новый цвет заливки (null — как у обводки); вид заливки не меняется, а без заливки
    /// включается сплошная — раз выбрали цвет заливки, она нужна.
    /// </summary>
    public static Annotation WithFillColor(Annotation a, string? fillColor)
    {
        if (!IsFillable(a.Kind)) return a;
        var fill = a.Fill == FillMode.None && fillColor is not null ? FillMode.Solid : a.Fill;
        return a.FillColor == fillColor && a.Fill == fill ? a : a with { Fill = fill, FillColor = fillColor };
    }
}
