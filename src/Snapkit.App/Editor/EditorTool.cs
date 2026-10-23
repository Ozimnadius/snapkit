using System.Collections.Immutable;
using System.Windows;
using System.Windows.Media.Imaging;
using Snapkit.Core;
using Snapkit.Core.Model;

namespace Snapkit.App.Editor;

public enum EditorTool { Select, Rectangle, Arrow, Text, Pin }

/// <summary>Результат «Готово»: разметка, общий комментарий и было ли что-то изменено.</summary>
public sealed record EditorResult(ImmutableList<Annotation> Annotations, string SnapshotNote, bool IsChanged);

/// <summary>
/// Всё, что редактору нужно знать о месте, где он открыт.
/// MonitorSize — размер монитора в единицах WPF; Scale — физические пиксели на единицу;
/// Region — область снимка в физических пикселях внутри монитора;
/// WorkArea — рабочая область монитора (без панели задач) в тех же координатах;
/// Background — картинка снимка, если под редактором не застывший кадр (повторное открытие);
/// RegionEditable — можно ли менять границы снимка маркерами.
/// </summary>
public sealed record EditorContext(
    Size MonitorSize,
    double Scale,
    RectI Region,
    RectI WorkArea,
    BitmapSource? Background,
    ImmutableList<Annotation> Annotations,
    string SnapshotNote,
    char Letter,
    bool RegionEditable);
