using Snapkit.App.Session;

namespace Snapkit.App.Clipboarding;

/// <summary>Когда после изменения ленты класть в буфер пакет ленты заново.</summary>
public static class ClipboardRepublish
{
    /// <summary>
    /// Новый или исправленный снимок — пакет в буфер всегда. Удаление, «Вернуть» и перестановка меняют
    /// буквы, но пакет переиздаётся, только если в буфере сейчас наш пакет ленты: «Копировать снимок»,
    /// черновик из редактора и то, что пользователь скопировал сам, не затираются.
    /// </summary>
    public static bool After(SessionChangeKind kind, bool hasCaptureId, bool hasUnsent, bool stripPackageInClipboard)
    {
        if (!hasUnsent) return false;
        return kind switch
        {
            SessionChangeKind.Added or SessionChangeKind.Updated => hasCaptureId,
            SessionChangeKind.Removed or SessionChangeKind.Reordered or SessionChangeKind.Restored => stripPackageInClipboard,
            _ => false,
        };
    }
}
