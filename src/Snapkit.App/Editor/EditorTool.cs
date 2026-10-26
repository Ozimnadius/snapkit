using System.Collections.Immutable;
using System.Windows;
using System.Windows.Media.Imaging;
using Snapkit.Core;
using Snapkit.Core.Model;

namespace Snapkit.App.Editor;

public enum EditorTool
{
    Select, Rectangle, RoundedRectangle, Ellipse, Arrow, CurvedArrow, WideArrow, Line,
    Pencil, Marker, Text, Eraser, Blur, Crop, Pin,
}

/// <summary>Инструменты, собранные под одной кнопкой панели с меню «▾».</summary>
public enum ToolGroup { None, Shape, Arrow, Pencil }

/// <summary>Результат «Готово»: разметка, общий комментарий и было ли что-то изменено.</summary>
public sealed record EditorResult(ImmutableList<Annotation> Annotations, string SnapshotNote, bool IsChanged);

/// <summary>
/// Всё, что редактору нужно знать о месте, где он открыт.
/// MonitorSize — размер монитора в единицах WPF; Scale — физические пиксели на единицу;
/// Region — область снимка в физических пикселях внутри монитора;
/// WorkArea — рабочая область монитора (без панели задач) в тех же координатах;
/// Source — пиксели под снимком для размытия и пипетки: у нового снимка кадр монитора,
/// у открытого из ленты — его исходник; SourceOrigin — где пиксель (0,0) Source в координатах монитора;
/// ShowSource — рисовать Source на холсте (у нового снимка кадр уже показывает окно);
/// RegionEditable — можно ли менять границы снимка маркерами.
/// </summary>
public sealed record EditorContext(
    Size MonitorSize,
    double Scale,
    RectI Region,
    RectI WorkArea,
    BitmapSource Source,
    PointD SourceOrigin,
    bool ShowSource,
    ImmutableList<Annotation> Annotations,
    string SnapshotNote,
    char Letter,
    bool RegionEditable);
