using System.Security.Cryptography;

namespace Snapkit.Core.Updates;

/// <summary>Когда предлагать обновление и годится ли скачанный файл.</summary>
public static class UpdatePolicy
{
    /// <summary>Версия новее и не отложена: snoozedVersion совпадает и now раньше snoozedUntil — не предлагать.</summary>
    public static bool ShouldOffer(Version current, ReleaseInfo latest, string snoozedVersion, DateTimeOffset? snoozedUntil, DateTimeOffset now)
    {
        if (latest.Version <= current) return false;
        var snoozed = Version.TryParse(snoozedVersion, out var version) && version == latest.Version
                      && snoozedUntil is { } until && now < until;
        return !snoozed;
    }

    /// <summary>SHA-256 файла совпадает с ожидаемым (регистр не важен).</summary>
    public static bool HashMatches(ReadOnlySpan<byte> file, string sha256Hex)
        => string.Equals(Convert.ToHexString(SHA256.HashData(file)), sha256Hex, StringComparison.OrdinalIgnoreCase);
}
