using System.Collections.Concurrent;
using System.Windows.Media;

namespace Snapkit.App.Rendering;

/// <summary>Цвета программы: акцент (кружки, рамка выделения, «Готово») и палитра разметки.</summary>
public static class Palette
{
    public const string Accent = "#2F7BF5";
    public const string PanelBackground = "#1E2128";
    public const string PanelText = "#E8EAED";

    /// <summary>Цвета инструментов разметки; первый — по умолчанию.</summary>
    public static readonly string[] Markup = ["#E53935", "#FB8C00", "#FDD835", "#43A047", "#1E88E5", "#8E24AA"];

    public static readonly double[] Thicknesses = [2, 4, 6, 8];
    public const double DefaultThickness = 4;

    private static readonly ConcurrentDictionary<string, SolidColorBrush> Brushes = new(StringComparer.OrdinalIgnoreCase);

    public static Color Color(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    /// <summary>Замороженная кисть по цвету #RRGGBB — её можно использовать с любого потока.</summary>
    public static SolidColorBrush Brush(string hex) => Brushes.GetOrAdd(hex, h =>
    {
        var brush = new SolidColorBrush(Color(h));
        brush.Freeze();
        return brush;
    });
}
