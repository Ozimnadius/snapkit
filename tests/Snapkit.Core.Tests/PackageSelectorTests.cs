using Snapkit.Core.Model;
using static Snapkit.Core.Tests.TestData;

namespace Snapkit.Core.Tests;

public class PackageSelectorTests
{
    [Fact]
    public void All_DoesNotChangeSentMarks()
    {
        Capture[] strip = [Cap(sent: true), Cap(sent: true), Cap(sent: true)];
        var package = PackageSelector.All(strip);
        Assert.Equal(['A', 'B', 'C'], package.Select(p => p.Letter));
        Assert.Equal(strip.Select(c => c.Id), package.Select(p => p.Capture.Id));
        Assert.All(package, p => Assert.True(p.Capture.IsSent));
    }

    [Fact]
    public void All_TakesWholeStripEvenWithUnsent()
    {
        // «Сохранить всё…» — вся лента, и отправленные тоже, с буквами их мест
        Capture[] strip = [Cap(sent: true), Cap(), Cap(sent: true), Cap()];
        var package = PackageSelector.All(strip);
        Assert.Equal(strip.Select(c => c.Id), package.Select(p => p.Capture.Id));
        Assert.Equal(['A', 'B', 'C', 'D'], package.Select(p => p.Letter));
    }
}
