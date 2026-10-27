using Snapkit.Core.Session;

namespace Snapkit.Core.Tests;

public class CaptureFeedbackTests
{
    [Fact]
    public void Copied_SaysWhatCtrlVDoes() => Assert.Equal("Снимок скопирован — Ctrl+V вставит", CaptureFeedback.Copied);

    [Fact]
    public void Evicted_SaysOldestRemoved()
        => Assert.Equal("Лента полная — удалён самый старый снимок", CaptureFeedback.Evicted);
}
