using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.App.Editor;
using Snapkit.Core;
using Snapkit.Core.Editing;

namespace Snapkit.App.Tests;

/// <summary>Редактор целиком: кнопки панели и клавиши, как их нажимает пользователь.</summary>
public class EditorSurfaceTests
{
    private static EditorSurface Surface()
    {
        var frame = BitmapSource.Create(800, 600, 96, 96, PixelFormats.Bgra32, null, new byte[800 * 600 * 4], 800 * 4);
        frame.Freeze();
        return new EditorSurface(new EditorContext(new Size(800, 600), 1, new RectI(100, 100, 300, 200), new RectI(0, 0, 800, 560),
            frame, new PointD(0, 0), false, [], "", 'A', true));
    }

    private static T? Find<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T found) return found;
            if (Find<T>(child) is { } deeper) return deeper;
        }
        return null;
    }

    private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));

    private static void Press(EditorSurface surface, Key key)
    {
        // Окно-источник нажатия без WS_VISIBLE: видимое забрало бы фокус у тестов вставки, идущих параллельно
        using var source = new HwndSource(new HwndSourceParameters("Snapkit.Tests") { WindowStyle = 0 });
        surface.HandleKey(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    [Fact]
    public void Enter_InColorPicker_ChoosesColorAndKeepsEditing() => UiApp.Run(() =>
    {
        var surface = Surface();
        var done = false;
        surface.Done += (_, _) => done = true;
        var toolbar = Find<EditorToolbar>(surface)!;
        Click(toolbar.ColorButton);
        var picker = Find<ColorPickerPopup>(surface)!;

        // Подвинули ползунок оттенка и нажали Enter: фокус на ползунке, а не в поле кода
        picker.HueSlider.Value = 120;
        var expected = HexColor.FromHsv(120, 1, picker.ValueSlider.Value);
        Press(surface, Key.Enter);

        Assert.False(done, "Enter в выборе цвета завершил весь снимок");
        Assert.Null(Find<ColorPickerPopup>(surface));
        Assert.Equal((Color)ColorConverter.ConvertFromString(expected), ((SolidColorBrush)toolbar.ColorSwatch.Fill).Color);
    });

    [Fact]
    public void ToolChange_TurnsPipetteOff() => UiApp.Run(() =>
    {
        var surface = Surface();
        var cancelled = false;
        surface.Cancelled += (_, _) => cancelled = true;
        var toolbar = Find<EditorToolbar>(surface)!;
        Click(toolbar.ColorButton);
        var picker = Find<ColorPickerPopup>(surface)!;
        Click(LogicalTreeHelper.GetChildren(picker).OfType<DependencyObject>()
            .SelectMany(Descendants).OfType<Button>().Single(b => Equals(b.ToolTip, "Взять цвет со снимка")));

        // Передумали брать цвет и выбрали линию: пипетка выключена, Esc снова отменяет снимок
        Click(toolbar.LineTool);
        Press(surface, Key.Escape);

        Assert.True(cancelled, "пипетка осталась включённой после смены инструмента");
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        => LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().SelectMany(Descendants).Prepend(root);
}
