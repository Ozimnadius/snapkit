namespace Snapkit.Core.Session;

/// <summary>Что говорит программа после «Готово» и когда новый снимок некуда поставить.</summary>
public static class CaptureFeedback
{
    /// <summary>После «Готово» в редакторе снимок в буфере — следующий Ctrl+V вставит его.</summary>
    public const string Copied = "Снимок скопирован — Ctrl+V вставит";

    /// <summary>Новый снимок некуда поставить: все снимки ленты неотправленные.</summary>
    public static string StripFull => $"В ленте {SessionModel.MaxCaptures} неотправленных снимков. Вставьте или удалите лишние";
}
