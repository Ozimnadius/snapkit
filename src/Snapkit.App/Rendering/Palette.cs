using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using Snapkit.Core.Theming;

namespace Snapkit.App.Rendering;

/// <summary>Цвета программы: цвет программы (кружки, рамка выделения, «Готово») и палитра разметки.</summary>
public static class Palette
{
    /// <summary>Первый цвет программы — рамка выделения, маркеры, обводки.</summary>
    public static string Accent => Volatile.Read(ref _accent).First;

    /// <summary>Заливка цветом программы — кнопки, кружки заметок; у градиента — под 135°. Заморожена.</summary>
    public static Brush AccentFill => Volatile.Read(ref _accentFill);

    /// <summary>Заливка для варианта цвета программы — без общего состояния.</summary>
    public static Brush CreateAccentFill(AccentOption option)
    {
        if (option.Second is null) return Brush(option.First);
        var brush = new LinearGradientBrush(Color(option.First), Color(option.Second), new Point(0, 0), new Point(1, 1));
        brush.Freeze();
        return brush;
    }

    /// <summary>Сменить цвет программы («#RRGGBB» или «#RRGGBB|#RRGGBB»; непонятное — синий по умолчанию).</summary>
    public static void SetAccent(string value)
    {
        var option = AccentOptions.Parse(value);
        Volatile.Write(ref _accentFill, CreateAccentFill(option));
        Volatile.Write(ref _accent, option);
    }
    public const string PanelBackground = "#1E2128";
    public const string PanelText = "#E8EAED";

    /// <summary>Цвета инструментов разметки; первый — по умолчанию.</summary>
    public static readonly string[] Markup = ["#E53935", "#FB8C00", "#FDD835", "#43A047", "#1E88E5", "#8E24AA"];

    public static readonly double[] Thicknesses = [2, 4, 6, 8];
    public const double DefaultThickness = 4;

    private static readonly ConcurrentDictionary<string, SolidColorBrush> Brushes = new(StringComparer.OrdinalIgnoreCase);

    // Цвет программы читается и с потоков отрисовки: меняется целиком, а не по частям.
    // Объявлен после Brushes: статические поля создаются по порядку, а заливке нужен словарь кистей
    private static AccentOption _accent = AccentOptions.Parse(AccentOptions.Default);
    private static Brush _accentFill = CreateAccentFill(_accent);

    public static Color Color(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    /// <summary>Замороженная кисть по цвету #RRGGBB — её можно использовать с любого потока.</summary>
    public static SolidColorBrush Brush(string hex) => Brushes.GetOrAdd(hex, h =>
    {
        var brush = new SolidColorBrush(Color(h));
        brush.Freeze();
        return brush;
    });

    /// <summary>Полупрозрачная кисть: opacity от 0 до 1.</summary>
    public static SolidColorBrush Brush(string hex, double opacity)
    {
        var alpha = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
        return Brushes.GetOrAdd($"{hex}/{alpha}", _ =>
        {
            var color = Color(hex);
            var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, color.R, color.G, color.B));
            brush.Freeze();
            return brush;
        });
    }
}
