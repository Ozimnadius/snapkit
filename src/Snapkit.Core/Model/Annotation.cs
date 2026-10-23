using System.Collections.Immutable;

namespace Snapkit.Core.Model;

public enum AnnotationKind { Rectangle, Arrow, Text, Pin }

/// <summary>
/// Одна фигура разметки. Координаты — в пикселях снимка.
/// Points: Rectangle — [угол, противоположный угол]; Arrow — [начало, конец]; Text и Pin — [точка привязки].
/// Note — комментарий; пустой или null — у фигуры нет кружка.
/// </summary>
public sealed record Annotation(
    Guid Id,
    AnnotationKind Kind,
    ImmutableArray<PointD> Points,
    string Color,
    double Thickness,
    string? Text,
    string? Note)
{
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
}
