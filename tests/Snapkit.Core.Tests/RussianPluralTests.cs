using Snapkit.Core;

namespace Snapkit.Core.Tests;

public class RussianPluralTests
{
    [Theory]
    [InlineData(0, "0 изображений")]
    [InlineData(1, "1 изображение")]
    [InlineData(2, "2 изображения")]
    [InlineData(5, "5 изображений")]
    [InlineData(11, "11 изображений")]
    [InlineData(12, "12 изображений")]
    [InlineData(21, "21 изображение")]
    [InlineData(22, "22 изображения")]
    [InlineData(111, "111 изображений")]
    public void Plural(int n, string expected)
        => Assert.Equal(expected, RussianPlural.Format(n, "изображение", "изображения", "изображений"));
}
