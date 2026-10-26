using Snapkit.Core.Session;

namespace Snapkit.Core.Tests;

public class CaptureFeedbackTests
{
    [Fact]
    public void Ready_UsesLetter() => Assert.Equal("Снимок B готов — Ctrl+V вставит", CaptureFeedback.Ready('B'));

    [Fact]
    public void StripFull_NamesLimit()
        => Assert.Equal("В ленте 20 неотправленных снимков. Вставьте или удалите лишние", CaptureFeedback.StripFull);
}
