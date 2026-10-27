using System.Windows.Media;
using Snapkit.App.Rendering;
using Snapkit.Core.Theming;

namespace Snapkit.App.Tests;

public class PaletteTests
{
    [Fact]
    public void CreateAccentFill_Gradient_IsFrozenTwoStops()
    {
        var fill = Assert.IsType<LinearGradientBrush>(Palette.CreateAccentFill(AccentOptions.Parse("#6366F1|#3B82F6")));
        Assert.True(fill.IsFrozen);
        Assert.Equal(new[] { Color.FromRgb(0x63, 0x66, 0xF1), Color.FromRgb(0x3B, 0x82, 0xF6) }, fill.GradientStops.Select(s => s.Color));
    }

    [Fact]
    public void CreateAccentFill_Solid_IsSolid()
    {
        var fill = Assert.IsType<SolidColorBrush>(Palette.CreateAccentFill(AccentOptions.Parse("#16A34A")));
        Assert.True(fill.IsFrozen);
        Assert.Equal(Color.FromRgb(0x16, 0xA3, 0x4A), fill.Color);
    }
}
