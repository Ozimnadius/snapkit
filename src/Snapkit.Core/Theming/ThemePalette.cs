using Snapkit.Core.Settings;

namespace Snapkit.Core.Theming;

/// <summary>
/// Цвета темы программы: панели, рамки, текст. Цвет программы (кнопки, рамка выделения, кружки заметок)
/// задаётся отдельно — он одинаковый в обеих темах.
/// </summary>
public sealed record ThemePalette(string Panel, string PanelRaised, string PanelBorder, string Hover,
    string Text, string MutedText, string Chip, string WarningText)
{
    /// <summary>Тёмная — как до этапа 4, по образцу Snapik.</summary>
    public static ThemePalette Dark { get; } =
        new("#1E2128", "#272B33", "#3A3F4A", "#323743", "#E8EAED", "#9AA0A6", "#33FFFFFF", "#FBBF5A");

    /// <summary>Светлая — как на наброске light-theme.html: белые панели, серые рамки.</summary>
    public static ThemePalette Light { get; } =
        new("#FFFFFF", "#F3F5F8", "#D5DAE1", "#E8EBF0", "#1F2328", "#6B7280", "#E8EBF0", "#B45309");

    /// <summary>Выбор в настройках; «Как в Windows» — по теме приложений Windows.</summary>
    public static ThemePalette Resolve(AppTheme choice, bool windowsAppsLight) => choice switch
    {
        AppTheme.Light => Light,
        AppTheme.System => windowsAppsLight ? Light : Dark,
        _ => Dark,
    };
}
