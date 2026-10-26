using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Snapkit.App.Rendering;
using Snapkit.Core.Model;

namespace Snapkit.App.Editor;

/// <summary>Панель инструментов редактора. Только сообщает о нажатиях; состояние хранит EditorSurface.</summary>
public partial class EditorToolbar : UserControl
{
    private static readonly Dictionary<EditorTool, string> Names = new()
    {
        [EditorTool.Rectangle] = "Прямоугольник",
        [EditorTool.RoundedRectangle] = "Скруглённый прямоугольник",
        [EditorTool.Ellipse] = "Овал",
        [EditorTool.Arrow] = "Стрелка",
        [EditorTool.CurvedArrow] = "Изогнутая стрелка",
        [EditorTool.WideArrow] = "Широкая стрелка",
        [EditorTool.Pencil] = "Карандаш",
        [EditorTool.Marker] = "Маркер",
    };

    private static readonly Dictionary<EditorTool, string> Keys = new()
    {
        [EditorTool.Rectangle] = "R", [EditorTool.RoundedRectangle] = "R", [EditorTool.Ellipse] = "R",
        [EditorTool.Arrow] = "A", [EditorTool.CurvedArrow] = "A", [EditorTool.WideArrow] = "A",
        [EditorTool.Pencil] = "P", [EditorTool.Marker] = "M",
    };

    private IReadOnlyDictionary<ToolGroup, EditorTool> _lastVariant = new Dictionary<ToolGroup, EditorTool>();
    private FillMode _fill;
    private LineStyle _lineStyle;

    public EditorToolbar()
    {
        InitializeComponent();
        SelectTool.Content = Icon(EditorTool.Select);
        LineTool.Content = Icon(EditorTool.Line);
        TextTool.Content = Icon(EditorTool.Text);
        EraserTool.Content = Icon(EditorTool.Eraser);
        BlurTool.Content = Icon(EditorTool.Blur);
        CropTool.Content = Icon(EditorTool.Crop);
        PinTool.Content = Icon(EditorTool.Pin);
        ShowGroups();
    }

    public event EventHandler<EditorTool>? ToolSelected;
    public event EventHandler<ColorTarget>? ColorPickerRequested;
    public event EventHandler<FillMode>? FillSelected;
    public event EventHandler? FillColorReset;
    public event EventHandler<double>? ThicknessSelected;
    public event EventHandler<LineStyle>? LineStyleSelected;
    public event EventHandler? UndoClicked;
    public event EventHandler? RedoClicked;
    public event EventHandler? CopyClicked;
    public event EventHandler? DoneClicked;
    public event EventHandler? HelpClicked;

    /// <summary>Где на панели кнопка цвета — над ней открывается выбор цвета.</summary>
    public double ColorButtonLeft => ColorButton.IsLoaded ? ColorButton.TranslatePoint(new Point(0, 0), this).X : 0;

    /// <summary>
    /// Показать состояние редактора. fillApplicable — заливку можно применить (рамки и овал);
    /// иначе кнопка заливки неактивна, но остаётся на месте: ширина панели не меняется.
    /// </summary>
    public void Show(EditorTool tool, IReadOnlyDictionary<ToolGroup, EditorTool> lastVariant, string color, double thickness,
        FillMode fill, string? fillColor, LineStyle lineStyle, bool fillApplicable, bool canUndo, bool canRedo)
    {
        _lastVariant = lastVariant;
        _fill = fill;
        _lineStyle = lineStyle;
        FillButton.IsEnabled = fillApplicable;
        FillNone.Visibility = fill == FillMode.None ? Visibility.Visible : Visibility.Collapsed;
        FillSwatch.Fill = fill switch
        {
            FillMode.Solid => Palette.Brush(fillColor ?? color),
            FillMode.SemiTransparent => Palette.Brush(fillColor ?? color, 0.3),
            FillMode.Blur => Palette.Brush("#9AA0A6", 0.7),
            _ => Brushes.Transparent,
        };
        ShowGroups();
        var group = EditorKeys.GroupOf(tool);
        SelectTool.IsChecked = tool == EditorTool.Select;
        ShapeTool.IsChecked = group == ToolGroup.Shape;
        ArrowTool.IsChecked = group == ToolGroup.Arrow;
        PencilTool.IsChecked = group == ToolGroup.Pencil;
        LineTool.IsChecked = tool == EditorTool.Line;
        TextTool.IsChecked = tool == EditorTool.Text;
        EraserTool.IsChecked = tool == EditorTool.Eraser;
        BlurTool.IsChecked = tool == EditorTool.Blur;
        CropTool.IsChecked = tool == EditorTool.Crop;
        PinTool.IsChecked = tool == EditorTool.Pin;
        ColorSwatch.Fill = Palette.Brush(color);
        ThicknessText.Text = $"{thickness:0} px";
        UndoButton.IsEnabled = canUndo;
        RedoButton.IsEnabled = canRedo;
    }

    // Кнопка группы показывает значок и подсказку последнего выбранного варианта
    private void ShowGroups()
    {
        foreach (var (button, group) in new[] { (ShapeTool, ToolGroup.Shape), (ArrowTool, ToolGroup.Arrow), (PencilTool, ToolGroup.Pencil) })
        {
            var tool = VariantOf(group);
            if (Equals(button.DataContext, tool)) continue;
            button.DataContext = tool;
            button.Content = Icon(tool);
            button.ToolTip = $"{Names[tool]} ({Keys[tool]})";
        }
    }

    private EditorTool VariantOf(ToolGroup group)
        => _lastVariant.TryGetValue(group, out var tool) ? tool : EditorKeys.Variants(group)[0];

    private void Tool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<EditorTool>(tag, out var tool))
            ToolSelected?.Invoke(this, tool);
    }

    private void Group_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<ToolGroup>(tag, out var group))
            ToolSelected?.Invoke(this, VariantOf(group));
    }

    private void GroupMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag } target || !Enum.TryParse<ToolGroup>(tag, out var group)) return;
        var current = VariantOf(group);
        var menu = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom };
        foreach (var tool in EditorKeys.Variants(group))
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = new ContentControl
            {
                Content = Icon(tool), Width = 22, Margin = new Thickness(0, 0, 10, 0), Foreground = Brushes.White,
                FontFamily = (FontFamily)FindResource("IconFont"), FontSize = 14,
            };
            header.Children.Add(icon);
            header.Children.Add(new TextBlock { Text = $"{Names[tool]} ({Keys[tool]})", VerticalAlignment = VerticalAlignment.Center });
            var item = new MenuItem { Header = header, IsChecked = tool == current };
            item.Click += (_, _) => ToolSelected?.Invoke(this, tool);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e) => ColorPickerRequested?.Invoke(this, ColorTarget.Stroke);

    private void FillButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = FillButton, Placement = PlacementMode.Bottom };
        foreach (var (mode, name) in new[]
                 {
                     (FillMode.None, "Без заливки"), (FillMode.Solid, "Сплошная"),
                     (FillMode.SemiTransparent, "Полупрозрачная"), (FillMode.Blur, "Размытием"),
                 })
        {
            var item = new MenuItem { Header = name, IsChecked = mode == _fill };
            item.Click += (_, _) => FillSelected?.Invoke(this, mode);
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var pick = new MenuItem { Header = "Цвет заливки…" };
        pick.Click += (_, _) => ColorPickerRequested?.Invoke(this, ColorTarget.Fill);
        menu.Items.Add(pick);
        var reset = new MenuItem { Header = "Как цвет обводки" };
        reset.Click += (_, _) => FillColorReset?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(reset);
        menu.IsOpen = true;
    }

    private void ThicknessButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = ThicknessButton, Placement = PlacementMode.Bottom };
        foreach (var thickness in Palette.Thicknesses)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new Rectangle
            {
                Width = 28, Height = thickness, Fill = Brushes.White, RadiusX = thickness / 2, RadiusY = thickness / 2,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0),
            });
            header.Children.Add(new TextBlock { Text = $"{thickness:0} px", VerticalAlignment = VerticalAlignment.Center });
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => ThicknessSelected?.Invoke(this, thickness);
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        foreach (var (style, name, dash) in new[]
                 {
                     (LineStyle.Solid, "Сплошная", (DoubleCollection?)null),
                     (LineStyle.Dashed, "Пунктир", new DoubleCollection([3, 2])),
                     (LineStyle.Dotted, "Точки", new DoubleCollection([0, 2])),
                 })
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new Line
            {
                X1 = 0, X2 = 28, Stroke = Brushes.White, StrokeThickness = 3, StrokeDashArray = dash,
                StrokeDashCap = PenLineCap.Round, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0),
            });
            header.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
            var item = new MenuItem { Header = header, IsChecked = style == _lineStyle };
            item.Click += (_, _) => LineStyleSelected?.Invoke(this, style);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => UndoClicked?.Invoke(this, EventArgs.Empty);
    private void Redo_Click(object sender, RoutedEventArgs e) => RedoClicked?.Invoke(this, EventArgs.Empty);
    private void Copy_Click(object sender, RoutedEventArgs e) => CopyClicked?.Invoke(this, EventArgs.Empty);
    private void Done_Click(object sender, RoutedEventArgs e) => DoneClicked?.Invoke(this, EventArgs.Empty);
    private void Help_Click(object sender, RoutedEventArgs e) => HelpClicked?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Значок инструмента. Знаки шрифта Segoe Fluent Icons или контур, который берёт цвет текста кнопки,
    /// чтобы у выбранного инструмента он становился белым.
    /// </summary>
    internal static object Icon(EditorTool tool) => tool switch
    {
        EditorTool.Select => FilledPath("M0,0 L0,15 L4,11.2 L6.8,17 L9.4,15.8 L6.7,10.2 L12,10.2 Z"),
        EditorTool.Rectangle => "",
        EditorTool.RoundedRectangle => StrokedPath("M5,2 H11 A3,3 0 0 1 14,5 V11 A3,3 0 0 1 11,14 H5 A3,3 0 0 1 2,11 V5 A3,3 0 0 1 5,2 Z"),
        EditorTool.Ellipse => StrokedPath("M1,8 A7,5.5 0 1 1 15,8 A7,5.5 0 1 1 1,8 Z"),
        EditorTool.Arrow => StrokedPath("M1,15 L14,2 M6,2 L14,2 L14,10"),
        EditorTool.CurvedArrow => StrokedPath("M1,15 Q2,3 14,3 M9.5,-0.5 L14,3 L9.5,6.5"),
        EditorTool.WideArrow => FilledPath("M0.5,14 L8.5,6 L6.5,4 L15,1 L12,9.5 L10,7.5 L2,15.5 Z"),
        EditorTool.Line => StrokedPath("M2,14 L14,2"),
        EditorTool.Pencil => "",
        EditorTool.Marker => "",
        EditorTool.Text => "",
        EditorTool.Eraser => "",
        EditorTool.Blur => Mosaic(),
        EditorTool.Crop => "",
        EditorTool.Pin => "",
        _ => "",
    };

    private static Path StrokedPath(string data)
    {
        var path = new Path
        {
            Data = Geometry.Parse(data), StrokeThickness = 1.8, StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
        };
        path.SetBinding(Shape.StrokeProperty, ForegroundOfButton());
        return path;
    }

    private static Path FilledPath(string data)
    {
        var path = new Path { Data = Geometry.Parse(data), StrokeThickness = 0 };
        path.SetBinding(Shape.FillProperty, ForegroundOfButton());
        return path;
    }

    // Мозаика из квадратиков разной яркости — значок размытия
    private static Grid Mosaic()
    {
        var grid = new Grid { Width = 15, Height = 15 };
        double[] opacity = [1, 0.45, 0.8, 0.55, 1, 0.4, 0.85, 0.5, 0.7];
        for (var i = 0; i < 9; i++)
        {
            var cell = new Rectangle { Width = 4, Height = 4, Opacity = opacity[i], HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(i % 3 * 5.5, i / 3 * 5.5, 0, 0), RadiusX = 0.8, RadiusY = 0.8 };
            cell.SetBinding(Shape.FillProperty, ForegroundOfButton());
            grid.Children.Add(cell);
        }
        return grid;
    }

    private static Binding ForegroundOfButton()
        => new("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1) };
}
