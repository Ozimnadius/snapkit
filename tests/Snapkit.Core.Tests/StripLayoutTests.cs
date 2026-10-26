namespace Snapkit.Core.Tests;

public class StripLayoutTests
{
    [Theory]
    [InlineData(-5, 0)]
    [InlineData(10, 0)]
    [InlineData(20, 1)]
    [InlineData(60, 2)]
    [InlineData(118, 2)]
    [InlineData(138, 3)]
    [InlineData(500, 3)]
    public void DropSlot_ThreeCards(double y, int expected)
        => Assert.Equal(expected, StripLayout.DropSlot(y, count: 3, cardHeight: 124, stripeHeight: 34));

    [Fact]
    public void DropSlot_NoCards_IsZero() => Assert.Equal(0, StripLayout.DropSlot(50, 0, 124, 34));

    [Theory]
    [InlineData(3, 2, false)]
    [InlineData(-4, 0, true)]
    [InlineData(0, 5, true)]
    public void IsDragStarted_ComparesWithSystemThreshold(double dx, double dy, bool expected)
        => Assert.Equal(expected, StripLayout.IsDragStarted(dx, dy, 4, 4));

    // Раскрывается карточка, чья видимая часть под мышью, а не та, что уже поднята над стопкой
    [Theory]
    [InlineData(-1, -1)]
    [InlineData(0, 0)]
    [InlineData(33, 0)]
    [InlineData(34, 1)]
    [InlineData(67, 1)]
    [InlineData(68, 2)]
    [InlineData(191, 2)]
    [InlineData(192, -1)]
    public void HoverIndex_ThreeCards(double y, int expected)
        => Assert.Equal(expected, StripLayout.HoverIndex(y, count: 3, cardHeight: 124, stripeHeight: 34));

    [Fact]
    public void HoverIndex_NoCards_IsNone() => Assert.Equal(-1, StripLayout.HoverIndex(10, 0, 124, 34));

    // Бросок мимо списка карточек (заголовок, кнопки ленты) порядок не меняет
    [Theory]
    [InlineData(10, -3, null)]
    [InlineData(-1, 20, null)]
    [InlineData(177, 20, null)]
    [InlineData(10, 200, null)]
    [InlineData(10, 20, 1)]
    [InlineData(10, 199, 3)]
    public void DropSlotInside_OnlyOverList(double x, double y, int? expected)
        => Assert.Equal(expected, StripLayout.DropSlotInside(x, y, width: 176, height: 200, count: 3, cardHeight: 124, stripeHeight: 34));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 68)]
    [InlineData(3, 192)]
    public void SlotTop_IsWhereTheLineIsDrawn(int slot, double expected)
        => Assert.Equal(expected, StripLayout.SlotTop(slot, count: 3, cardHeight: 124, stripeHeight: 34));
}
