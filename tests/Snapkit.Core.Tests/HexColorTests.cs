using Snapkit.Core.Editing;

namespace Snapkit.Core.Tests;

public class HexColorTests
{
    [Theory]
    [InlineData("e53935", "#E53935")]
    [InlineData("#E53935", "#E53935")]
    [InlineData("#abc", "#AABBCC")]
    [InlineData(" #43a047 ", "#43A047")]
    public void TryNormalize_AcceptsCommonForms(string input, string expected)
    {
        Assert.True(HexColor.TryNormalize(input, out var hex));
        Assert.Equal(expected, hex);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("zzzzzz")]
    [InlineData("#1234567")]
    [InlineData("12345g")]
    public void TryNormalize_RejectsGarbage(string input) => Assert.False(HexColor.TryNormalize(input, out _));

    [Theory]
    [InlineData("#E53935")]
    [InlineData("#FB8C00")]
    [InlineData("#FDD835")]
    [InlineData("#43A047")]
    [InlineData("#1E88E5")]
    [InlineData("#8E24AA")]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    public void Hsv_RoundTrip_KeepsColor(string hex)
    {
        var (h, s, v) = HexColor.ToHsv(hex);
        var back = HexColor.FromHsv(h, s, v);
        for (var i = 1; i < 7; i += 2)
            Assert.InRange(Convert.ToInt32(back.Substring(i, 2), 16) - Convert.ToInt32(hex.Substring(i, 2), 16), -1, 1);
    }

    [Fact]
    public void FromHsv_PureHues()
    {
        Assert.Equal("#FF0000", HexColor.FromHsv(0, 1, 1));
        Assert.Equal("#00FF00", HexColor.FromHsv(120, 1, 1));
        Assert.Equal("#0000FF", HexColor.FromHsv(240, 1, 1));
    }

    [Fact]
    public void AddRecent_PutsFirst_NoDuplicates_LimitsToMax()
    {
        Assert.Equal("#111111", HexColor.AddRecent("", "#111111"));
        Assert.Equal("#222222,#111111", HexColor.AddRecent("#111111,#222222", "#222222"));
        Assert.Equal("#222222,#111111", HexColor.AddRecent("#111111", "#222222"));
        Assert.Equal("#AABBCC,#111111", HexColor.AddRecent("#111111,#aabbcc", "#AABBCC"));
        var eight = string.Join(",", Enumerable.Range(1, 8).Select(i => $"#00000{i}"));
        var added = HexColor.Parse(HexColor.AddRecent(eight, "#FFFFFF"));
        Assert.Equal(8, added.Count);
        Assert.Equal("#FFFFFF", added[0]);
        Assert.DoesNotContain("#000008", added);
    }

    [Fact]
    public void Parse_SkipsGarbage()
        => Assert.Equal(["#111111", "#AABBCC"], HexColor.Parse(" #111111, мусор, aabbcc ,"));
}
