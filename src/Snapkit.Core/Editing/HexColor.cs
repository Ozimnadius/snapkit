using System.Globalization;

namespace Snapkit.Core.Editing;

/// <summary>Цвета разметки в виде «#RRGGBB»: ввод кода, оттенок и яркость, список своих цветов.</summary>
public static class HexColor
{
    /// <summary>«e53935», «#E53935», «#abc» → «#E53935», «#AABBCC». Остальное — false.</summary>
    public static bool TryNormalize(string input, out string hex)
    {
        hex = "";
        var s = input.Trim().TrimStart('#');
        if (s.Length == 3) s = string.Concat(s.Select(c => new string(c, 2)));
        if (s.Length != 6 || !s.All(Uri.IsHexDigit)) return false;
        hex = "#" + s.ToUpperInvariant();
        return true;
    }

    /// <summary>Цвет по оттенку (0–360), насыщенности и яркости (0–1).</summary>
    public static string FromHsv(double hue, double saturation, double value)
    {
        var h = (hue % 360 + 360) % 360 / 60;
        var s = Math.Clamp(saturation, 0, 1);
        var v = Math.Clamp(value, 0, 1);
        var c = v * s;
        var x = c * (1 - Math.Abs(h % 2 - 1));
        var (r, g, b) = (int)h switch
        {
            0 => (c, x, 0d),
            1 => (x, c, 0d),
            2 => (0d, c, x),
            3 => (0d, x, c),
            4 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        var m = v - c;
        return $"#{Byte(r + m):X2}{Byte(g + m):X2}{Byte(b + m):X2}";

        static int Byte(double channel) => (int)Math.Round(Math.Clamp(channel, 0, 1) * 255);
    }

    public static (double Hue, double Saturation, double Value) ToHsv(string hex)
    {
        if (!TryNormalize(hex, out var normal)) return (0, 0, 0);
        var r = int.Parse(normal.AsSpan(1, 2), NumberStyles.HexNumber) / 255.0;
        var g = int.Parse(normal.AsSpan(3, 2), NumberStyles.HexNumber) / 255.0;
        var b = int.Parse(normal.AsSpan(5, 2), NumberStyles.HexNumber) / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var delta = max - Math.Min(r, Math.Min(g, b));
        double hue = 0;
        if (delta > 0)
        {
            if (max == r) hue = 60 * ((g - b) / delta % 6);
            else if (max == g) hue = 60 * ((b - r) / delta + 2);
            else hue = 60 * ((r - g) / delta + 4);
        }
        return ((hue + 360) % 360, max == 0 ? 0 : delta / max, max);
    }

    /// <summary>Свой цвет — в начало списка, без повторов, не больше max цветов.</summary>
    public static string AddRecent(string list, string hex, int max = 8)
    {
        if (!TryNormalize(hex, out var normal)) return string.Join(",", Parse(list));
        return string.Join(",", Parse(list).Where(c => c != normal).Prepend(normal).Take(max));
    }

    /// <summary>Список «#112233,#445566» → цвета; непонятные части пропускаются.</summary>
    public static IReadOnlyList<string> Parse(string list)
        => list.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => TryNormalize(part, out var hex) ? hex : null)
            .OfType<string>()
            .Distinct()
            .ToList();
}
