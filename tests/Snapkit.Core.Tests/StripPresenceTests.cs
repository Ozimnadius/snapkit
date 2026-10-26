using Snapkit.Core.Session;

namespace Snapkit.Core.Tests;

public class StripPresenceTests
{
    [Theory]
    [InlineData(false, false, StripView.Hidden)]
    [InlineData(false, true, StripView.Hidden)]
    [InlineData(true, false, StripView.Strip)]
    [InlineData(true, true, StripView.Capsule)]
    public void OnStartup(bool showStrip, bool collapsed, StripView expected)
        => Assert.Equal(expected, StripPresence.OnStartup(showStrip, collapsed));

    [Theory]
    [InlineData(StripView.Capsule, true, StripView.Capsule)]   // свёрнута — остаётся капсулой, растёт счётчик
    [InlineData(StripView.Hidden, true, StripView.Strip)]      // закрыта — после снимка появляется
    [InlineData(StripView.Strip, true, StripView.Strip)]
    [InlineData(StripView.Hidden, false, StripView.Hidden)]    // лента выключена — только уведомление
    [InlineData(StripView.Strip, false, StripView.Strip)]      // показана на время щелчком у часов — так и остаётся
    public void AfterNewCapture(StripView current, bool showStrip, StripView expected)
        => Assert.Equal(expected, StripPresence.AfterNewCapture(current, showStrip));

    [Theory]
    [InlineData(true, StripView.Capsule)]
    [InlineData(false, StripView.Hidden)]
    public void OnMinimize(bool showStrip, StripView expected) => Assert.Equal(expected, StripPresence.OnMinimize(showStrip));

    [Theory]
    [InlineData(false, true, StripView.Hidden)]
    [InlineData(true, true, StripView.Capsule)]
    [InlineData(true, false, StripView.Strip)]
    public void OnShowStripChanged(bool showStrip, bool collapsed, StripView expected)
        => Assert.Equal(expected, StripPresence.OnShowStripChanged(showStrip, collapsed));
}
