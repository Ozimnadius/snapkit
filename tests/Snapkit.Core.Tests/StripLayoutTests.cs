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
}
