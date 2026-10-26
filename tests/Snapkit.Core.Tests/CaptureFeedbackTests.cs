using Snapkit.Core.Session;

namespace Snapkit.Core.Tests;

public class CaptureFeedbackTests
{
    [Fact]
    public void Copied_SaysWhatCtrlVDoes() => Assert.Equal("Снимок скопирован — Ctrl+V вставит", CaptureFeedback.Copied);

    [Fact]
    public void StripFull_NamesLimit()
        => Assert.Equal("В ленте 20 неотправленных снимков. Вставьте или удалите лишние", CaptureFeedback.StripFull);
}
