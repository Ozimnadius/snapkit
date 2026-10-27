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

    [Fact]
    public void Parse_NonStringFields_IsNull()
        => Assert.Null(ReleaseInfo.Parse("""{ "tag_name": 5, "assets": [ { "name": 7, "browser_download_url": [], "digest": {} } ] }"""));

    [Fact]
    public void Parse_ForeignDownloadUrl_IsNull()
        // Подменённый ответ задал бы и ссылку, и хеш — файл берём только из релизов Snapkit
        => Assert.Null(ReleaseInfo.Parse(Json($$"""{ "name": "Snapkit.exe", "browser_download_url": "https://example.com/Snapkit.exe", "digest": "sha256:{{Hash}}" }""")));

    // Страница github.com/…/releases/latest переадресует на …/releases/tag/vX.Y.Z — без лимита GitHub API
    [Theory]
    [InlineData("https://github.com/Ozimnadius/snapkit/releases/tag/v0.4.1", "0.4.1")]
    [InlineData("https://github.com/Ozimnadius/snapkit/releases/tag/V1.2.0", "1.2.0")]
    public void VersionFromTagUrl_Tag(string url, string version)
        => Assert.Equal(Version.Parse(version), ReleaseInfo.VersionFromTagUrl(url));

    [Theory]
    [InlineData(null)]
    [InlineData("https://github.com/Ozimnadius/snapkit/releases")]
    [InlineData("https://github.com/Other/snapkit/releases/tag/v9.9.9")]
    [InlineData("https://github.com/Ozimnadius/snapkit/releases/tag/v0.4.1/extra")]
    [InlineData("https://github.com/Ozimnadius/snapkit/releases/tag/beta")]
    public void VersionFromTagUrl_Other_IsNull(string? url) => Assert.Null(ReleaseInfo.VersionFromTagUrl(url));

    [Fact]
    public void FromPage_LinksToReleaseFiles()
    {
        var version = new Version(0, 4, 1);
        Assert.Equal(new ReleaseInfo(version, "https://github.com/Ozimnadius/snapkit/releases/download/v0.4.1/Snapkit.exe", Hash),
            ReleaseInfo.FromPage(version, Hash));
        Assert.Equal("https://github.com/Ozimnadius/snapkit/releases/download/v0.4.1/Snapkit.exe.sha256", ReleaseInfo.HashFileUrl(version));
    }

    [Fact]
    public void CanInstall_OnlyWithHash()
    {
        Assert.True(ReleaseInfo.FromPage(new Version(0, 4, 1), Hash).CanInstall);
        Assert.False(ReleaseInfo.FromPage(new Version(0, 4, 1), null).CanInstall);
    }

    [Theory]
    [InlineData(Hash + "  Snapkit.exe\n")]
    [InlineData(Hash + " *Snapkit.exe")]
    [InlineData(Hash)]
    [InlineData("﻿" + Hash + "\r\n")]
    public void ParseHashFile_Hash(string text) => Assert.Equal(Hash, ReleaseInfo.ParseHashFile(text));

    [Fact]
    public void ParseHashFile_UpperCase_ToLower() => Assert.Equal(Hash, ReleaseInfo.ParseHashFile(Hash.ToUpperInvariant()));

    [Theory]
    [InlineData("")]
    [InlineData("не хеш")]
    [InlineData("a67740352646dffbfac17436d695b04b55f198cd8060b9fdc2873e00265105c")]
    [InlineData("a67740352646dffbfac17436d695b04b55f198cd8060b9fdc2873e00265105c00")]
    [InlineData("zz7740352646dffbfac17436d695b04b55f198cd8060b9fdc2873e00265105c0")]
    public void ParseHashFile_Garbage_IsNull(string text) => Assert.Null(ReleaseInfo.ParseHashFile(text));
}
