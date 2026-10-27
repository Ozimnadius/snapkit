using System.Text.Json;

namespace Snapkit.Core.Updates;

/// <summary>Последний релиз на GitHub: версия, ссылка на Snapkit.exe и его SHA-256 (строчными, без «sha256:»).</summary>
public sealed record ReleaseInfo(Version Version, string ExeUrl, string Sha256)
{
    private const string ExeName = "Snapkit.exe";
    private const string DigestPrefix = "sha256:";
    // Файл берём только из релизов Snapkit: подменённый ответ задал бы и ссылку, и хеш
    private const string DownloadPrefix = "https://github.com/Ozimnadius/snapkit/releases/download/";

    /// <summary>Ответ GitHub «latest release»: tag_name «v0.3.2», файл Snapkit.exe и его digest «sha256:…»; иначе null.</summary>
    public static ReleaseInfo? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || Text(root, "tag_name") is not { } tagName
                || !Version.TryParse(tagName.TrimStart('v', 'V'), out var version)
                || !root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.ValueKind == JsonValueKind.Object
                    && Text(asset, "name") == ExeName
                    && Text(asset, "browser_download_url") is { } link && link.StartsWith(DownloadPrefix, StringComparison.Ordinal)
                    && Text(asset, "digest") is { } hash && hash.StartsWith(DigestPrefix, StringComparison.OrdinalIgnoreCase))
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

    // Строковое поле объекта; другой тип или нет поля — null (GetString на числе бросил бы исключение)
    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
