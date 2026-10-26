namespace Snapkit.Core.Session;

/// <summary>Что говорит программа о новом снимке, когда лента не на экране или полна.</summary>
public static class CaptureFeedback
{
    /// <summary>Лента выключена: снимок встал в пачку — следующий Ctrl+V его вставит.</summary>
    public static string Ready(char letter) => $"Снимок {letter} готов — Ctrl+V вставит";

    /// <summary>Новый снимок некуда поставить: все снимки ленты неотправленные.</summary>
    public static string StripFull => $"В ленте {SessionModel.MaxCaptures} неотправленных снимков. Вставьте или удалите лишние";
}
