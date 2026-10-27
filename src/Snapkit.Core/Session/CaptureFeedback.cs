namespace Snapkit.Core.Session;

/// <summary>Что говорит программа после «Готово» и когда полная лента вытесняет снимок.</summary>
public static class CaptureFeedback
{
    /// <summary>После «Готово» в редакторе снимок в буфере — следующий Ctrl+V вставит его.</summary>
    public const string Copied = "Снимок скопирован — Ctrl+V вставит";

    /// <summary>Лента полная: новый снимок вытеснил самый старый.</summary>
    public const string Evicted = "Лента полная — удалён самый старый снимок";
}
