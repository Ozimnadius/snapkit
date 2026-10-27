using Snapkit.Core.Updates;

namespace Snapkit.Core.Tests;

public class ReleaseInfoTests
{
    private const string Hash = "a67740352646dffbfac17436d695b04b55f198cd8060b9fdc2873e00265105c0";
    private const string Url = "https://github.com/Ozimnadius/snapkit/releases/download/v0.3.1/Snapkit.exe";

    private static string Json(string assets) => $$"""{ "tag_name": "v0.3.1", "name": "Snapkit v0.3.1", "assets": [ {{assets}} ] }""";

    [Fact]
    public void Parse_LatestRelease()
    {
        var info = ReleaseInfo.Parse(Json($$"""{ "name": "Snapkit.exe", "browser_download_url": "{{Url}}", "digest": "sha256:{{Hash}}" }"""));
        Assert.Equal(new ReleaseInfo(new Version(0, 3, 1), Url, Hash), info);
    }

    [Fact]
    public void Parse_WithoutExe_IsNull()
        => Assert.Null(ReleaseInfo.Parse(Json($$"""{ "name": "readme.txt", "browser_download_url": "{{Url}}", "digest": "sha256:{{Hash}}" }""")));

    [Fact]
    public void Parse_WithoutDigest_IsNull()
        // Без хеша файл нечем проверить — обновление не предлагается
        => Assert.Null(ReleaseInfo.Parse(Json($$"""{ "name": "Snapkit.exe", "browser_download_url": "{{Url}}" }""")));

    [Theory]
    [InlineData("не json")]
    [InlineData("{}")]
    [InlineData("""{ "tag_name": "версия", "assets": [] }""")]
    public void Parse_Garbage_IsNull(string json) => Assert.Null(ReleaseInfo.Parse(json));
}
