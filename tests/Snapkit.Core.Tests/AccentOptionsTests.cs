using Snapkit.Core.Theming;

namespace Snapkit.Core.Tests;

public class AccentOptionsTests
{
    [Fact]
    public void AllAccents_WhiteTextIsReadable()
    {
        // Белые надписи на кнопках и в кружках заметок должны читаться на любом цвете программы
        foreach (var option in AccentOptions.All)
        foreach (var color in new[] { option.First, option.Second }.OfType<string>())
            Assert.True(ColorContrast.Ratio("#FFFFFF", color) >= 3, $"белый на {color}");
    }

    [Fact]
    public void All_HasTwelveWithDefaultFirst()
    {
        Assert.Equal(12, AccentOptions.All.Count);
        Assert.Equal(AccentOptions.Default, AccentOptions.All[0].Value);
        Assert.Equal(6, AccentOptions.All.Count(o => o.Second is null));
        Assert.All(AccentOptions.All.Skip(6), o => Assert.NotNull(o.Second));
    }

    [Fact]
    public void Parse_Gradient()
        => Assert.Equal(new AccentOption("#6366F1|#3B82F6", "#6366F1", "#3B82F6"), AccentOptions.Parse("#6366F1|#3B82F6"));

    [Theory]
    [InlineData("зелёный")]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("#6366F1|мусор")]
    public void Parse_Invalid_IsDefault(string value)
        => Assert.Equal(new AccentOption("#2F7BF5", "#2F7BF5", null), AccentOptions.Parse(value));
}
