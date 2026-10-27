using Snapkit.App.Session;

namespace Snapkit.App.Clipboarding;

/// <summary>Что положить в буфер после изменения ленты.</summary>
public enum ClipboardAction { None, Package }

/// <summary>Что класть в буфер после изменения ленты.</summary>
public static class ClipboardRepublish
{
    /// <summary>
    /// Новый или исправленный снимок буфер не трогает: снимок копирует сама кнопка «Готово» в редакторе
    /// (CaptureController.CaptureDone), в том числе без правок. Удаление, «Вернуть» и перестановка меняют
    /// буквы, но пакет ленты переиздаётся, только если в буфере сейчас пакет ленты: «Копировать снимок»,
    /// черновик из редактора и то, что пользователь скопировал сам, не затираются.
    /// </summary>
    public static ClipboardAction After(SessionChangeKind kind, bool hasCaptureId, bool hasCaptures, bool stripPackageInClipboard)
        => kind switch
        {
            SessionChangeKind.Removed or SessionChangeKind.Reordered or SessionChangeKind.Restored
                when hasCaptures && stripPackageInClipboard => ClipboardAction.Package,
            _ => ClipboardAction.None,
        };
}
