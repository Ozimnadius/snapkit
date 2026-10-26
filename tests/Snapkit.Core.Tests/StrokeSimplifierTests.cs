using Snapkit.Core.Editing;

namespace Snapkit.Core.Tests;

public class StrokeSimplifierTests
{
    [Fact]
    public void Simplify_DropsTooClosePoints()
        => Assert.Equal([new(0, 0), new(2, 0), new(10, 0)],
            StrokeSimplifier.Simplify([new(0, 0), new(1, 0), new(2, 0), new(3, 0), new(10, 0)], 2).ToArray());

    [Fact]
    public void Simplify_KeepsFirstAndLast()
        => Assert.Equal([new(0, 0), new(1, 0)],
            StrokeSimplifier.Simplify([new(0, 0), new(0.5, 0), new(1, 0)], 2).ToArray());

    [Fact]
    public void Simplify_SinglePoint_StaysSingle()
        => Assert.Equal([new(5, 5)], StrokeSimplifier.Simplify([new(5, 5)], 2).ToArray());

    [Fact]
    public void Simplify_Empty_IsEmpty()
        => Assert.Empty(StrokeSimplifier.Simplify([], 2));
}
