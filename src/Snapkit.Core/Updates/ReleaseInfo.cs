using System.Text.Json;

namespace Snapkit.Core.Updates;

/// <summary>
/// Последний релиз на GitHub: версия, ссылка на Snapkit.exe и его SHA-256 (строчными, без «sha256:»).
/// Sha256 = null — хеш взять неоткуда: такую версию можно показать, но не поставить.
/// </summary>
public sealed record ReleaseInfo(Version Version, string ExeUrl, string? Sha256)
{
    private const string ExeName = "Snapkit.exe";
    private const string DigestPrefix = "sha256:";
    // Файл берём только из релизов Snapkit: подменённый ответ задал бы и ссылку, и хеш
    private const string DownloadPrefix = "https://github.com/Ozimnadius/snapkit/releases/download/";
    private const string TagPrefix = "https://github.com/Ozimnadius/snapkit/releases/tag/";

    /// <summary>Есть хеш — файл можно скачать и проверить.</summary>
    public bool CanInstall => Sha256 is not null;

    /// <summary>Релиз по версии со страницы релизов: Snapkit.exe лежит в нём по известному адресу.</summary>
    public static ReleaseInfo FromPage(Version version, string? sha256) => new(version, $"{DownloadPrefix}v{version}/{ExeName}", sha256);

    /// <summary>Файл с хешем, который сборка релиза кладёт рядом с Snapkit.exe.</summary>
    public static string HashFileUrl(Version version) => $"{DownloadPrefix}v{version}/{ExeName}.sha256";

    /// <summary>
    /// Куда переадресует github.com/…/releases/latest: «…/releases/tag/v0.4.1» → 0.4.1; другое — null.
    /// Эта страница не считается в лимит GitHub API.
    /// </summary>
    public static Version? VersionFromTagUrl(string? url)
    {
        if (url is null || !url.StartsWith(TagPrefix, StringComparison.Ordinal)) return null;
        var tag = url[TagPrefix.Length..];
        return tag.Length > 1 && tag[0] is 'v' or 'V' && Version.TryParse(tag[1..], out var version) ? version : null;
    }

    /// <summary>Содержимое Snapkit.exe.sha256: «хеш  Snapkit.exe» или только хеш → хеш строчными; иначе null.</summary>
    public static string? ParseHashFile(string text)
    {
        var hash = text.TrimStart('﻿').Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return hash is { Length: 64 } && hash.All(Uri.IsHexDigit) ? hash.ToLowerInvariant() : null;
    }

    /// <summary>Ответ GitHub API «latest release»: tag_name «v0.3.2», файл Snapkit.exe и его digest «sha256:…»; иначе null.</summary>
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
