using System.Text;
using Snapkit.Core.Updates;

namespace Snapkit.Core.Tests;

public class UpdatePolicyTests
{
    private static readonly Version Current = new(0, 3, 1);
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.FromHours(3));
    private static ReleaseInfo Latest(string version) => new(Version.Parse(version), "https://example/Snapkit.exe", "00");

    [Fact]
    public void ShouldOffer_Newer() => Assert.True(UpdatePolicy.ShouldOffer(Current, Latest("0.3.2"), "", null, Now));

    [Theory]
    [InlineData("0.3.1")]
    [InlineData("0.2.0")]
    public void ShouldOffer_SameOrOlder_No(string version) => Assert.False(UpdatePolicy.ShouldOffer(Current, Latest(version), "", null, Now));

    [Fact]
    public void ShouldOffer_SnoozedThisVersionUntilTomorrow_No()
        => Assert.False(UpdatePolicy.ShouldOffer(Current, Latest("0.3.2"), "0.3.2", Now.AddDays(1), Now));

    [Fact]
    public void ShouldOffer_SnoozedOlderVersion_NewerOut_Yes()
        => Assert.True(UpdatePolicy.ShouldOffer(Current, Latest("0.3.3"), "0.3.2", Now.AddDays(1), Now));

    [Fact]
    public void ShouldOffer_NoHash_No()
        // Версию узнали со страницы релизов, а хеша нет — кнопкой «Обновить» такую не поставить
        => Assert.False(UpdatePolicy.ShouldOffer(Current, new ReleaseInfo(new Version(0, 3, 2), "https://example/Snapkit.exe", null), "", null, Now));

    [Fact]
    public void ShouldOffer_SnoozeExpired_Yes()
        => Assert.True(UpdatePolicy.ShouldOffer(Current, Latest("0.3.2"), "0.3.2", Now.AddDays(-1), Now));

    [Fact]
    public void HashMatches_RightHash_AnyCase()
    {
        var file = Encoding.ASCII.GetBytes("abc");
        Assert.True(UpdatePolicy.HashMatches(file, "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
        Assert.True(UpdatePolicy.HashMatches(file, "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD"));
    }

    [Fact]
    public void HashMatches_OtherHash_No()
        => Assert.False(UpdatePolicy.HashMatches(Encoding.ASCII.GetBytes("abd"), "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
}
