using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Snapkit.App.Rendering;
using Snapkit.Core.Editing;

namespace Snapkit.App.Editor;

/// <summary>Чей цвет выбирают: обводки фигуры или её заливки.</summary>
public enum ColorTarget { Stroke, Fill }

/// <summary>
/// Выбор цвета: палитра, свои цвета, оттенок и яркость, код цвета и пипетка.
/// Цвет из кода, ползунков или пипетки считается своим и попадает в список своих цветов.
/// </summary>
public partial class ColorPickerPopup : UserControl
{
    private bool _updatingSliders;

    public ColorPickerPopup(ColorTarget target, string current, IReadOnlyList<string> customColors)
    {
        InitializeComponent();
        Target = target;
        Title.Text = target == ColorTarget.Stroke ? "Цвет обводки" : "Цвет заливки";
        foreach (var hex in Palette.Markup) PaletteRow.Children.Add(Swatch(hex));
        foreach (var hex in customColors) CustomRow.Children.Add(Swatch(hex));
        CustomTitle.Visibility = customColors.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowColor(current);
        // Клики по окну не должны доходить до холста и рамки снимка
        MouseLeftButtonDown += (_, e) => e.Handled = true;
    }

    public ColorTarget Target { get; }

    /// <summary>Выбран цвет; IsCustom — набран кодом, ползунками или пипеткой.</summary>
    public event EventHandler<(string Hex, bool IsCustom)>? ColorChosen;
    public event EventHandler? PipetteRequested;
    public event EventHandler? Closed;

    private Button Swatch(string hex)
    {
        var button = new Button { Style = (Style)FindResource("Swatch"), Background = Palette.Brush(hex), ToolTip = hex };
        button.Click += (_, _) => ColorChosen?.Invoke(this, (hex, false));
        return button;
    }

    private void ShowColor(string hex)
    {
        _updatingSliders = true;
        var (hue, _, value) = HexColor.ToHsv(hex);
        HueSlider.Value = hue;
        ValueSlider.Value = value;
        _updatingSliders = false;
        HexBox.Text = hex;
        Preview.Background = Palette.Brush(hex);
    }

    // Ползунки задают оттенок и яркость при полной насыщенности; ноль яркости — чёрный
    private void Hsv_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingSliders || HexBox is null) return;
        var hex = HexColor.FromHsv(HueSlider.Value, 1, ValueSlider.Value);
        HexBox.Text = hex;
        Preview.Background = Palette.Brush(hex);
    }

    private void HexBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ChooseTyped();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Closed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void Choose_Click(object sender, RoutedEventArgs e) => ChooseTyped();

    private void ChooseTyped()
    {
        if (HexColor.TryNormalize(HexBox.Text, out var hex)) ColorChosen?.Invoke(this, (hex, true));
        else HexBox.Text = "";
    }

    private void Pipette_Click(object sender, RoutedEventArgs e) => PipetteRequested?.Invoke(this, EventArgs.Empty);
    private void Close_Click(object sender, RoutedEventArgs e) => Closed?.Invoke(this, EventArgs.Empty);
}
