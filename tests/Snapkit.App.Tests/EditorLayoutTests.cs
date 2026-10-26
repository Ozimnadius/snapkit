using System.Windows;
using Snapkit.App.Editor;
using Snapkit.Core.Model;

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

    // По ширине: экран 1920, настоящая панель инструментов, панель комментариев 300 у края экрана (отступ 12)
    private const double Screen = 1920, PanelWidth = 300, Gap = 12, Margin = 8;
    private static readonly double BarWidth = MeasureToolbar(fillApplicable: true);

    private static double MeasureToolbar(bool fillApplicable)
    {
        var width = 0.0;
        UiApp.Run(() =>
        {
            var bar = new EditorToolbar();
            bar.Show(EditorTool.Rectangle, new Dictionary<ToolGroup, EditorTool>(), "#E53935", 4,
                FillMode.None, null, LineStyle.Solid, fillApplicable, canUndo: true, canRedo: true);
            bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            width = bar.DesiredSize.Width;
        });
        return width;
    }

    [Fact]
    public void Toolbar_KeepsWidth_WhenFillDoesNotApply()
        // Иначе при смене инструмента кнопки правее «Заливки» прыгают, а панель вылезает за край
        => Assert.Equal(BarWidth, MeasureToolbar(fillApplicable: false));

    // Ноутбук 1920×1080 при масштабе 175 % (1366×768 при 125 % почти такой же): экран 1097 DIP
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Toolbar_FitsBesideCommentsPanel_OnLaptopScreen(bool panelOnRight)
    {
        const double screen = 1097;
        var panelWidth = EditorSurface.CommentsWidth(screen, BarWidth);
        var panelLeft = panelOnRight ? screen - panelWidth - Gap : Gap;
        // Снимок у того края, где нет панели комментариев
        var left = EditorSurface.ToolbarLeft(regionLeft: panelOnRight ? 0 : 900, BarWidth, screen, panelLeft, panelWidth);

        Assert.InRange(left, Margin, screen - Margin - BarWidth);
        Assert.True(left + BarWidth <= panelLeft || left >= panelLeft + panelWidth,
            $"панель инструментов {left}…{left + BarWidth} под панелью комментариев {panelLeft}…{panelLeft + panelWidth}");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Toolbar_StaysOnScreen_WhenNoRoomBesideCommentsPanel(bool panelOnRight)
    {
        // Экран 960 DIP (1920×1080 при масштабе 200 %): места рядом с панелью комментариев нет,
        // панель инструментов ложится поверх неё, но все кнопки на экране
        const double screen = 960;
        var panelWidth = EditorSurface.CommentsWidth(screen, BarWidth);
        var panelLeft = panelOnRight ? screen - panelWidth - Gap : Gap;
        var left = EditorSurface.ToolbarLeft(regionLeft: panelOnRight ? 0 : 900, BarWidth, screen, panelLeft, panelWidth);
        Assert.InRange(left, Margin, screen - Margin - BarWidth);
    }

    [Fact]
    public void CommentsWidth_WideScreen_IsFull()
        => Assert.Equal(PanelWidth, EditorSurface.CommentsWidth(Screen, BarWidth));

    [Fact]
    public void Toolbar_AtRegionLeft_WhenCommentsPanelIsFarAway()
        => Assert.Equal(600, EditorSurface.ToolbarLeft(regionLeft: 600, BarWidth, Screen, panelLeft: Gap, PanelWidth));

    [Fact]
    public void Toolbar_NotUnderCommentsPanelOnLeft_WhenSnapshotCoversScreen()
    {
        // Картинка больше экрана: её левый край за экраном, панель инструментов прижимается влево —
        // туда же, где панель комментариев, и кнопки инструментов оказываются под ней
        var left = EditorSurface.ToolbarLeft(regionLeft: -540, BarWidth, Screen, panelLeft: Gap, PanelWidth);
        Assert.True(left >= Gap + PanelWidth, $"панель инструментов под панелью комментариев: {left}");
    }

    [Fact]
    public void Toolbar_NotUnderCommentsPanelOnRight()
    {
        var panelLeft = Screen - PanelWidth - Gap;
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

    [Theory]
    [InlineData(1920)]
    [InlineData(0)]   // монитор слева от основного
    public void CommentsPanel_IsAlwaysOnTheRight(double workRight)
        // Пользователь попросил: панель комментариев — справа, где бы ни был снимок
        => Assert.Equal(workRight - PanelWidth - Gap, EditorSurface.CommentsLeft(workRight, PanelWidth));
}
