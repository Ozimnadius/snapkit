using Snapkit.Core.Input;

namespace Snapkit.Core.Tests;

public class HotkeyParserTests
{
    [Theory]
    [InlineData("PrintScreen", HotkeyMods.None, 0x2C)]
    [InlineData("Ctrl+F12", HotkeyMods.Ctrl, 0x7B)]
    [InlineData("ctrl + alt + s", HotkeyMods.Ctrl | HotkeyMods.Alt, 0x53)]
    [InlineData("Shift+Win+5", HotkeyMods.Shift | HotkeyMods.Win, 0x35)]
    [InlineData("F1", HotkeyMods.None, 0x70)]
    public void Parse_KnownCombinations(string text, HotkeyMods mods, int vk)
        => Assert.Equal(new Hotkey(mods, vk), HotkeyParser.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Ctrl")]
    [InlineData("Ctrl+Банан")]
    [InlineData("F25")]
    public void Parse_Invalid_ReturnsNull(string text)
        => Assert.Null(HotkeyParser.Parse(text));

    [Fact]
    public void ParseList_SplitsByCommaAndSkipsInvalid()
        => Assert.Equal(
            new[] { new Hotkey(HotkeyMods.None, Vk.Snapshot), new Hotkey(HotkeyMods.Ctrl, 0x7B) },
            HotkeyParser.ParseList("PrintScreen, Ctrl+F12, мусор"));

    [Fact]
    public void ParseList_NothingValid_FallsBackToPrintScreen()
        => Assert.Equal(new[] { new Hotkey(HotkeyMods.None, Vk.Snapshot) }, HotkeyParser.ParseList("мусор"));

    [Theory]
    [InlineData(HotkeyMods.None, 0x2C, "Print Screen")]
    [InlineData(HotkeyMods.Ctrl, 0x7B, "Ctrl+F12")]
    [InlineData(HotkeyMods.Ctrl | HotkeyMods.Shift, 0x53, "Ctrl+Shift+S")]
    public void Display_IsHumanReadable(HotkeyMods mods, int vk, string expected)
        => Assert.Equal(expected, HotkeyParser.Display(new Hotkey(mods, vk)));

    [Fact]
    public void ParseList_EmptyWithoutDefault_IsEmpty()
        => Assert.Empty(HotkeyParser.ParseList("", orPrintScreen: false));

    [Fact]
    public void FormatList_RoundTrips()
    {
        var text = HotkeyParser.FormatList(HotkeyParser.ParseList("PrintScreen, Ctrl+F12"));
        Assert.Equal("Print Screen, Ctrl+F12", text);
        Assert.Equal(
            new[] { new Hotkey(HotkeyMods.None, Vk.Snapshot), new Hotkey(HotkeyMods.Ctrl, 0x7B) },
            HotkeyParser.ParseList(text));
    }
}
