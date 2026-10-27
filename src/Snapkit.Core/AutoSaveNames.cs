using Snapkit.Core.Model;

namespace Snapkit.Core;

/// <summary>Имена файлов автосохранения: правка снимка перезаписывает тот же файл.</summary>
public static class AutoSaveNames
{
    /// <summary>
    /// Имя файла без расширения. Уже выданное снимку (AutoSaveName) — оно и остаётся, даже если соседи
    /// ушли с ленты; иначе — время снимка («2026-10-27_09-13-06»), а если оно занято — «(2)», «(3)»…
    /// </summary>
    public static string BaseName(Capture capture, Func<string, bool> isTaken)
        => capture.AutoSaveName ?? PackageExport.UniqueName(PackageExport.TimestampName(capture.CreatedAt.LocalDateTime), isTaken);
}
