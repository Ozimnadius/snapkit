using Snapkit.Core.Editing;

namespace Snapkit.Core.Theming;

/// <summary>Цвет программы: Value — как в settings.json («#RRGGBB» или градиент «#RRGGBB|#RRGGBB»).</summary>
public sealed record AccentOption(string Value, string First, string? Second);

/// <summary>Цвета программы на вкладке «Вид»: кнопки, рамка выделения, кружки заметок.</summary>
public static class AccentOptions
{
    public const string Default = "#2F7BF5";

    /// <summary>12 цветов: 6 однотонных, затем 6 градиентов. Белый текст на каждом читается (контраст не меньше 3).</summary>
    public static IReadOnlyList<AccentOption> All { get; } =
    [
        .. new[]
        {
            Default, "#16A34A", "#A855F7", "#EA580C", "#EC4899", "#0D9488",
            "#6366F1|#3B82F6", "#EA580C|#DC2626", "#16A34A|#0D9488", "#D97706|#DB2777", "#EC4899|#8B5CF6", "#0891B2|#2563EB",
        }.Select(Parse),
    ];

    /// <summary>«#RRGGBB» или «#RRGGBB|#RRGGBB»; непонятное — Default.</summary>
    public static AccentOption Parse(string value)
    {
        var parts = value.Split('|');
        var colors = new List<string>();
        foreach (var part in parts)
        {
            if (!HexColor.TryNormalize(part.Trim(), out var hex)) return new AccentOption(Default, Default, null);
            colors.Add(hex);
        }
        return colors.Count switch
        {
            1 => new AccentOption(colors[0], colors[0], null),
            2 => new AccentOption($"{colors[0]}|{colors[1]}", colors[0], colors[1]),
            _ => new AccentOption(Default, Default, null),
        };
    }
}
