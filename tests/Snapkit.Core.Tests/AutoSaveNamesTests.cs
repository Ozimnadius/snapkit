using Snapkit.Core.Settings;
using static Snapkit.Core.Tests.TestData;

namespace Snapkit.Core.Tests;

public class AutoSaveNamesTests
{
    private static readonly DateTimeOffset Moment = new(new DateTime(2026, 10, 27, 9, 13, 6, 100, DateTimeKind.Local));

    [Fact]
    public void AutoSaveName_UsesCaptureTime()
    {
        var capture = Cap() with { CreatedAt = Moment };
        Assert.Equal("2026-10-27_09-13-06.png", AutoSaveNames.For(capture, [capture], SaveFormat.Png));
    }

    [Fact]
    public void AutoSaveName_SameSecond_GetsNumber()
    {
        // Два снимка в одну секунду: второй не затирает первый
        var first = Cap() with { CreatedAt = Moment };
        var second = Cap() with { CreatedAt = Moment.AddMilliseconds(300) };
        Assert.Equal("2026-10-27_09-13-06.png", AutoSaveNames.For(first, [first, second], SaveFormat.Png));
        Assert.Equal("2026-10-27_09-13-06 (2).png", AutoSaveNames.For(second, [first, second], SaveFormat.Png));
    }

    [Fact]
    public void AutoSaveName_StableAfterReorder()
    {
        // Перестановка на ленте не меняет имя: правка снимка перезапишет тот же файл
        var first = Cap() with { CreatedAt = Moment };
        var second = Cap() with { CreatedAt = Moment.AddMilliseconds(300) };
        Assert.Equal("2026-10-27_09-13-06 (2).png", AutoSaveNames.For(second, [second, first], SaveFormat.Png));
    }

    [Fact]
    public void AutoSaveName_Jpeg_UsesJpg()
    {
        var capture = Cap() with { CreatedAt = Moment };
        Assert.Equal("2026-10-27_09-13-06.jpg", AutoSaveNames.For(capture, [capture], SaveFormat.Jpeg));
    }
}
