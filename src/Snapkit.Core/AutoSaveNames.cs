using Snapkit.Core.Model;
using Snapkit.Core.Settings;

namespace Snapkit.Core;

/// <summary>Имена файлов автосохранения: правка снимка перезаписывает тот же файл.</summary>
public static class AutoSaveNames
{
    /// <summary>
    /// Время снимка («2026-10-27_09-13-06»), для соседей той же секунды — «(2)», «(3)»… по (CreatedAt, Id),
    /// и расширение формата. Порядок на ленте на имя не влияет.
    /// </summary>
    public static string For(Capture capture, IReadOnlyList<Capture> strip, SaveFormat format)
    {
        var name = PackageExport.TimestampName(capture.CreatedAt.LocalDateTime);
        var rank = strip
            .Where(c => PackageExport.TimestampName(c.CreatedAt.LocalDateTime) == name)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .ToList()
            .FindIndex(c => c.Id == capture.Id);
        return (rank > 0 ? $"{name} ({rank + 1})" : name) + PackageExport.Extension(format);
    }
}
