using System.Collections.Immutable;

namespace Snapkit.Core.Model;

// Значения хранятся в файле ленты строками: новые — только в конец
public enum AnnotationKind { Rectangle, Arrow, Text, Pin, RoundedRectangle, Ellipse, CurvedArrow, WideArrow, Line, Pencil, Marker, Blur }

/// <summary>Заливка рамок и овала: нет, сплошная, полупрозрачная или размытием снимка.</summary>
public enum FillMode { None, Solid, SemiTransparent, Blur }

public enum LineStyle { Solid, Dashed, Dotted }

/// <summary>
/// Одна фигура разметки. Координаты — в пикселях снимка.
/// Points: Rectangle, RoundedRectangle, Ellipse, Blur — [угол, противоположный угол];
/// Arrow, WideArrow, Line — [начало, конец]; CurvedArrow — [начало, точка изгиба на кривой, конец];
/// Pencil, Marker — ломаная; Text и Pin — [точка привязки].
/// Note — комментарий; пустой или null — у фигуры нет кружка.
/// Поля этапа 2 идут последними и со значениями по умолчанию: ленты этапа 1 читаются без них.
/// </summary>
public sealed record Annotation(
    Guid Id,
    AnnotationKind Kind,
    ImmutableArray<PointD> Points,
    string Color,
    double Thickness,
    string? Text,
    string? Note,
    FillMode Fill = FillMode.None,
    // null — цвет обводки
    string? FillColor = null,
    LineStyle LineStyle = LineStyle.Solid,
    // Сдвиг кружка заметки от его места по умолчанию
    PointD? PinOffset = null)
{
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
}
