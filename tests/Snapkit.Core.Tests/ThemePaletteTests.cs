using Snapkit.Core.Settings;
using Snapkit.Core.Theming;

namespace Snapkit.Core.Tests;

public class ThemePaletteTests
{
    public static TheoryData<string> Themes => ["Dark", "Light"];

    private static ThemePalette Of(string name) => name == "Dark" ? ThemePalette.Dark : ThemePalette.Light;

    [Theory]
    [MemberData(nameof(Themes))]
    public void Palettes_TextIsReadable(string name)
    {
        // Светлый текст на светлом фоне или тёмный на тёмном — провал этапа
        var p = Of(name);
        Assert.True(ColorContrast.Ratio(p.Text, p.Panel) >= 4.5, "текст на панели");
        Assert.True(ColorContrast.Ratio(p.Text, p.PanelRaised) >= 4.5, "текст на приподнятой панели");
        Assert.True(ColorContrast.Ratio(p.MutedText, p.Panel) >= 3, "приглушённый текст");
        Assert.True(ColorContrast.Ratio(p.WarningText, p.Panel) >= 3, "предупреждение");
    }

    [Fact]
    public void Ratio_BlackOnWhite_Is21() => Assert.Equal(21, ColorContrast.Ratio("#000000", "#FFFFFF"), 1);

    [Fact]
    public void Resolve_ExplicitChoice_IgnoresWindows()
    {
        Assert.Same(ThemePalette.Dark, ThemePalette.Resolve(AppTheme.Dark, windowsAppsLight: true));
        Assert.Same(ThemePalette.Light, ThemePalette.Resolve(AppTheme.Light, windowsAppsLight: false));
    }

    [Fact]
    public void Resolve_System_FollowsWindows()
    {
        Assert.Same(ThemePalette.Light, ThemePalette.Resolve(AppTheme.System, windowsAppsLight: true));
        Assert.Same(ThemePalette.Dark, ThemePalette.Resolve(AppTheme.System, windowsAppsLight: false));
    }
}
