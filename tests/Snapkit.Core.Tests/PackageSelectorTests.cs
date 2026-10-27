using Snapkit.Core.Model;
using static Snapkit.Core.Tests.TestData;

namespace Snapkit.Core.Tests;

public class PackageSelectorTests
{
    [Fact]
    public void All_TakesWholeStripInOrderWithLetters()
    {
        Capture[] strip = [Cap(), Cap(), Cap()];
        var package = PackageSelector.All(strip);
        Assert.Equal(['A', 'B', 'C'], package.Select(p => p.Letter));
        Assert.Equal(strip.Select(c => c.Id), package.Select(p => p.Capture.Id));
    }
}
