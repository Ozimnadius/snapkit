using Snapkit.App.Editor;

namespace Snapkit.App.Tests;

public class EditorLayoutTests
{
    // Монитор 1920×1080 с панелью задач снизу: рабочая область по высоте 0…1032; панель инструментов 48
    private const double WorkTop = 0, WorkBottom = 1032, Bar = 48;

    [Fact]
    public void Toolbar_BelowRegion_WhenThereIsRoom()
        => Assert.Equal(510, EditorSurface.ToolbarTop(regionTop: 100, regionBottom: 500, Bar, WorkTop, WorkBottom));

    [Fact]
    public void Toolbar_AboveRegion_WhenNoRoomBelow()
        => Assert.Equal(542, EditorSurface.ToolbarTop(regionTop: 600, regionBottom: 1000, Bar, WorkTop, WorkBottom));

    [Fact]
    public void Toolbar_InsideAboveTaskbar_WhenRegionIsWholeScreen()
        => Assert.Equal(976, EditorSurface.ToolbarTop(regionTop: 0, regionBottom: 1080, Bar, WorkTop, WorkBottom));

    // По ширине: экран 1920, панель инструментов 380, панель комментариев 230 у края экрана (отступ 12)
    private const double Screen = 1920, BarWidth = 380, PanelWidth = 230;

    [Fact]
    public void Toolbar_AtRegionLeft_WhenCommentsPanelIsFarAway()
        => Assert.Equal(600, EditorSurface.ToolbarLeft(regionLeft: 600, BarWidth, Screen, panelLeft: 12, PanelWidth));

    [Fact]
    public void Toolbar_NotUnderCommentsPanelOnLeft_WhenSnapshotCoversScreen()
    {
        // Картинка больше экрана: её левый край за экраном, панель инструментов прижимается влево —
        // туда же, где панель комментариев, и кнопки инструментов оказываются под ней
        var left = EditorSurface.ToolbarLeft(regionLeft: -540, BarWidth, Screen, panelLeft: 12, PanelWidth);
        Assert.True(left >= 12 + PanelWidth, $"панель инструментов под панелью комментариев: {left}");
    }

    [Fact]
    public void Toolbar_NotUnderCommentsPanelOnRight()
    {
        var panelLeft = Screen - PanelWidth - 12;
        var left = EditorSurface.ToolbarLeft(regionLeft: 1700, BarWidth, Screen, panelLeft, PanelWidth);
        Assert.True(left + BarWidth <= panelLeft, $"панель инструментов под панелью комментариев: {left}");
    }

    [Fact]
    public void Toolbar_StaysOnScreen_WhenImageIsBiggerThanScreen()
    {
        // Картинка 3000×2000 из буфера стоит по центру экрана: её края далеко за краями монитора
        var top = EditorSurface.ToolbarTop(regionTop: -460, regionBottom: 1540, Bar, WorkTop, WorkBottom);
        Assert.InRange(top, WorkTop, WorkBottom - Bar);
    }
}
