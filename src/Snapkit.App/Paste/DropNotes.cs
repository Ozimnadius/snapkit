namespace Snapkit.App.Paste;

/// <summary>
/// Перенесли карточку в другое окно: картинку получатель забрал перетаскиванием, а текст заметок
/// Snapkit вставляет туда сам — в Telegram он встаёт подписью к фото.
/// </summary>
public static class DropNotes
{
    /// <summary>
    /// Вставлять ли текст: есть заметки и окно чужое. В Проводник (папка, рабочий стол) переносят
    /// файл — туда текст не вставляется.
    /// </summary>
    public static bool ShouldPaste(string? targetProcess, string text)
        => text.Length > 0 && targetProcess is not null && !targetProcess.Equals("explorer", StringComparison.OrdinalIgnoreCase);
}
