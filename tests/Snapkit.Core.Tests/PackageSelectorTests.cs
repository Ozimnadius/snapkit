using Snapkit.Core.Model;
using static Snapkit.Core.Tests.TestData;

namespace Snapkit.Core.Tests;

public class PackageSelectorTests
{
    [Fact]
    public void ForExport_NoUnsent_TakesWholeStripWithoutMarking()
    {
        Capture[] strip = [Cap(sent: true), Cap(sent: true), Cap(sent: true)];
        var package = PackageSelector.ForExport(strip);
        Assert.Equal(['A', 'B', 'C'], package.Select(p => p.Letter));
        Assert.Equal(strip.Select(c => c.Id), package.Select(p => p.Capture.Id));
        Assert.All(package, p => Assert.True(p.Capture.IsSent));
    }

    [Fact]
    public void ForExport_WithUnsent_TakesOnlyThem()
    {
        Capture[] strip = [Cap(sent: true), Cap(), Cap(sent: true), Cap()];
        var package = PackageSelector.ForExport(strip);
        // Буква — место на ленте: выбранные второй и четвёртый — B и D
        Assert.Equal([('B', strip[1].Id), ('D', strip[3].Id)], package.Select(p => (p.Letter, p.Capture.Id)));
    }
}
