using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Snapkit.Core.Theming;

namespace Snapkit.App.Shell;

/// <summary>
/// Тема на лету: кисти темы лежат в ресурсах приложения, окна берут их через DynamicResource,
/// поэтому замена кисти сразу перекрашивает все открытые окна.
/// </summary>
public static class ThemeManager
{
    public const string PanelKey = "PanelBrush", PanelRaisedKey = "PanelRaisedBrush", PanelBorderKey = "PanelBorderBrush",
        HoverKey = "HoverBrush", TextKey = "TextBrush", MutedTextKey = "MutedTextBrush", ChipKey = "ChipBrush",
        WarningTextKey = "WarningTextBrush";

    /// <summary>Заменить кисти темы в ресурсах приложения — окна с DynamicResource перекрашиваются сразу.</summary>
    public static void Apply(ThemePalette palette)
    {
        var resources = Application.Current.Resources;
        resources[PanelKey] = Brush(palette.Panel);
        resources[PanelRaisedKey] = Brush(palette.PanelRaised);
        resources[PanelBorderKey] = Brush(palette.PanelBorder);
        resources[HoverKey] = Brush(palette.Hover);
        resources[TextKey] = Brush(palette.Text);
        resources[MutedTextKey] = Brush(palette.MutedText);
        resources[ChipKey] = Brush(palette.Chip);
        resources[WarningTextKey] = Brush(palette.WarningText);
    }

    /// <summary>Тема приложений Windows светлая (AppsUseLightTheme == 1); нет значения — тёмная.</summary>
    public static bool WindowsAppsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 1;
        }
        catch (Exception ex)
        {
            Log.Error("чтение темы Windows", ex);
            return false;
        }
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
