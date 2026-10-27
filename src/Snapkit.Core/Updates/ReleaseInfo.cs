using System.Text.Json;

namespace Snapkit.Core.Updates;

/// <summary>Последний релиз на GitHub: версия, ссылка на Snapkit.exe и его SHA-256 (строчными, без «sha256:»).</summary>
public sealed record ReleaseInfo(Version Version, string ExeUrl, string Sha256)
{
    private const string ExeName = "Snapkit.exe";
    private const string DigestPrefix = "sha256:";

    /// <summary>Ответ GitHub «latest release»: tag_name «v0.3.2», файл Snapkit.exe и его digest «sha256:…»; иначе null.</summary>
    public static ReleaseInfo? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("tag_name", out var tag) || tag.GetString() is not { } tagName
                || !Version.TryParse(tagName.TrimStart('v', 'V'), out var version)
                || !root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var name) && name.GetString() == ExeName
                    && asset.TryGetProperty("browser_download_url", out var url) && url.GetString() is { Length: > 0 } link
                    && asset.TryGetProperty("digest", out var digest) && digest.GetString() is { } hash
                    && hash.StartsWith(DigestPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return new ReleaseInfo(version, link, hash[DigestPrefix.Length..].ToLowerInvariant());
                }
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
