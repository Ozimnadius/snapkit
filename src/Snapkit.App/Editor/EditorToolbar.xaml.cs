using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Snapkit.App.Rendering;

namespace Snapkit.App.Editor;

/// <summary>Панель инструментов редактора. Только сообщает о нажатиях; состояние хранит EditorSurface.</summary>
public partial class EditorToolbar : UserControl
{
    private static readonly string[] ColorNames = ["Красный", "Оранжевый", "Жёлтый", "Зелёный", "Синий", "Фиолетовый"];

    public EditorToolbar()
    {
        InitializeComponent();
    }

    public event EventHandler<EditorTool>? ToolSelected;
    public event EventHandler<string>? ColorSelected;
    public event EventHandler<double>? ThicknessSelected;
    public event EventHandler? UndoClicked;
    public event EventHandler? RedoClicked;
    public event EventHandler? CopyClicked;
    public event EventHandler? DoneClicked;

    public void Show(EditorTool tool, string color, double thickness, bool canUndo, bool canRedo)
    {
        SelectTool.IsChecked = tool == EditorTool.Select;
        RectangleTool.IsChecked = tool == EditorTool.Rectangle;
        ArrowTool.IsChecked = tool == EditorTool.Arrow;
        TextTool.IsChecked = tool == EditorTool.Text;
        PinTool.IsChecked = tool == EditorTool.Pin;
        ColorSwatch.Fill = Palette.Brush(color);
        ThicknessText.Text = $"{thickness:0} px";
        UndoButton.IsEnabled = canUndo;
        RedoButton.IsEnabled = canRedo;
    }

    private void Tool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<EditorTool>(tag, out var tool))
            ToolSelected?.Invoke(this, tool);
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = ColorButton, Placement = PlacementMode.Bottom };
        for (var i = 0; i < Palette.Markup.Length; i++)
        {
            var hex = Palette.Markup[i];
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new Ellipse { Width = 14, Height = 14, Fill = Palette.Brush(hex), Margin = new Thickness(0, 0, 10, 0) });
            header.Children.Add(new TextBlock { Text = ColorNames[i], VerticalAlignment = VerticalAlignment.Center });
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => ColorSelected?.Invoke(this, hex);
            menu.Items.Add(item);
        }
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
        menu.IsOpen = true;
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => UndoClicked?.Invoke(this, EventArgs.Empty);
    private void Redo_Click(object sender, RoutedEventArgs e) => RedoClicked?.Invoke(this, EventArgs.Empty);
    private void Copy_Click(object sender, RoutedEventArgs e) => CopyClicked?.Invoke(this, EventArgs.Empty);
    private void Done_Click(object sender, RoutedEventArgs e) => DoneClicked?.Invoke(this, EventArgs.Empty);
}
