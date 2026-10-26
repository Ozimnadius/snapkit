using System.Windows.Input;

namespace Snapkit.App.Editor;

/// <summary>Группы инструментов панели и клавиши редактора — одна буква на инструмент.</summary>
public static class EditorKeys
{
    private static readonly EditorTool[] Shapes = [EditorTool.Rectangle, EditorTool.RoundedRectangle, EditorTool.Ellipse];
    private static readonly EditorTool[] Arrows = [EditorTool.Arrow, EditorTool.CurvedArrow, EditorTool.WideArrow];
    private static readonly EditorTool[] Pencils = [EditorTool.Pencil, EditorTool.Marker];

    public static ToolGroup GroupOf(EditorTool tool) => tool switch
    {
        EditorTool.Rectangle or EditorTool.RoundedRectangle or EditorTool.Ellipse => ToolGroup.Shape,
        EditorTool.Arrow or EditorTool.CurvedArrow or EditorTool.WideArrow => ToolGroup.Arrow,
        EditorTool.Pencil or EditorTool.Marker => ToolGroup.Pencil,
        _ => ToolGroup.None,
    };

    /// <summary>Инструменты группы в порядке меню; первый — вариант по умолчанию.</summary>
    public static IReadOnlyList<EditorTool> Variants(ToolGroup group) => group switch
    {
        ToolGroup.Shape => Shapes,
        ToolGroup.Arrow => Arrows,
        ToolGroup.Pencil => Pencils,
        _ => [],
    };

    /// <summary>Инструмент по клавише; у групп — последний выбранный вариант. Не клавиша инструмента — null.</summary>
    public static EditorTool? ToolFor(Key key, IReadOnlyDictionary<ToolGroup, EditorTool> lastVariant) => key switch
    {
        Key.V => EditorTool.Select,
        Key.R => lastVariant.GetValueOrDefault(ToolGroup.Shape, EditorTool.Rectangle),
        Key.A => lastVariant.GetValueOrDefault(ToolGroup.Arrow, EditorTool.Arrow),
        Key.L => EditorTool.Line,
        Key.P => EditorTool.Pencil,
        Key.M => EditorTool.Marker,
        Key.T => EditorTool.Text,
        Key.E => EditorTool.Eraser,
        Key.B => EditorTool.Blur,
        Key.X => EditorTool.Crop,
        Key.C => EditorTool.Pin,
        _ => null,
    };

    /// <summary>Подсказка «?»: все клавиши редактора.</summary>
    public static IReadOnlyList<(string Keys, string Action)> Shortcuts { get; } =
    [
        ("V", "Выбор: двигать фигуру, менять размер за маркеры, двигать кружок"),
        ("R", "Фигура: прямоугольник, скруглённый прямоугольник, овал"),
        ("A", "Стрелка: прямая, изогнутая, широкая"),
        ("L", "Линия"),
        ("P", "Карандаш"),
        ("M", "Маркер"),
        ("T", "Текст"),
        ("E", "Ластик — щелчок по фигуре удаляет её"),
        ("B", "Размыть — скрыть пароль, телефон, имя"),
        ("X", "Обрезать снимок"),
        ("C", "Комментарий — кружок с заметкой"),
        ("Delete", "Удалить выбранную фигуру"),
        ("Ctrl+Z", "Отменить"),
        ("Ctrl+Y", "Повторить"),
        ("Ctrl+Shift+C", "Копировать снимок"),
        ("Enter", "Готово"),
        ("Esc", "Отменить снимок"),
        ("F1", "Эта подсказка"),
    ];
}
