using System.Globalization;

namespace Snapkit.Core.Theming;

/// <summary>Контраст цветов по WCAG: 1 — не различить, 21 — чёрное на белом.</summary>
public static class ColorContrast
{
    /// <summary>Цвета «#RRGGBB» или «#AARRGGBB» (альфа не учитывается).</summary>
    public static double Ratio(string foreground, string background)
    {
        var a = Luminance(foreground);
        var b = Luminance(background);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Luminance(string hex)
    {
        var rgb = hex.TrimStart('#');
        if (rgb.Length == 8) rgb = rgb[2..];
        double Channel(int i)
        {
            var c = int.Parse(rgb.Substring(i * 2, 2), NumberStyles.HexNumber) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(0) + 0.7152 * Channel(1) + 0.0722 * Channel(2);
    }
}
