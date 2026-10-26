using Snapkit.App.Session;

namespace Snapkit.App.Clipboarding;

/// <summary>Что положить в буфер после изменения ленты.</summary>
public enum ClipboardAction { None, Package, Capture }

/// <summary>Что класть в буфер после изменения ленты.</summary>
public static class ClipboardRepublish
{
    /// <summary>
    /// «Готово» в редакторе (новый или исправленный снимок) — как «Копировать»: в буфер только этот
    /// снимок, следующий Ctrl+V вставит его одного. Удаление, «Вернуть» и перестановка меняют буквы,
    /// но пакет ленты переиздаётся, только если в буфере сейчас пакет ленты: «Копировать снимок»,
    /// черновик из редактора и то, что пользователь скопировал сам, не затираются.
    /// </summary>
    public static ClipboardAction After(SessionChangeKind kind, bool hasCaptureId, bool hasUnsent, bool stripPackageInClipboard)
        => kind switch
        {
            SessionChangeKind.Added or SessionChangeKind.Updated when hasCaptureId => ClipboardAction.Capture,
            SessionChangeKind.Removed or SessionChangeKind.Reordered or SessionChangeKind.Restored
                when hasUnsent && stripPackageInClipboard => ClipboardAction.Package,
            _ => ClipboardAction.None,
        };
}
