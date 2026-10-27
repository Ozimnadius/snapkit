using static Snapkit.Core.Tests.TestData;

namespace Snapkit.Core.Tests;

public class AutoSaveNamesTests
{
    private static readonly DateTimeOffset Moment = new(new DateTime(2026, 10, 27, 9, 13, 6, 100, DateTimeKind.Local));

    [Fact]
    public void AutoSaveName_UsesCaptureTime()
        => Assert.Equal("2026-10-27_09-13-06", AutoSaveNames.BaseName(Cap() with { CreatedAt = Moment }, _ => false));

    [Fact]
    public void AutoSaveName_TakenByNeighbour_GetsNumber()
    {
        // Два снимка в одну секунду: второй не затирает файл первого
        var second = Cap() with { CreatedAt = Moment.AddMilliseconds(300) };
        Assert.Equal("2026-10-27_09-13-06 (2)", AutoSaveNames.BaseName(second, name => name == "2026-10-27_09-13-06"));
    }

    [Fact]
    public void AutoSaveName_Remembered_StaysAfterNeighbourLeft()
    {
        // Имя закреплено за снимком: сосед ушёл с ленты — правка всё равно перезапишет свой файл, а не его
        var second = Cap() with { CreatedAt = Moment.AddMilliseconds(300), AutoSaveName = "2026-10-27_09-13-06 (2)" };
        Assert.Equal("2026-10-27_09-13-06 (2)", AutoSaveNames.BaseName(second, _ => false));
    }
}
