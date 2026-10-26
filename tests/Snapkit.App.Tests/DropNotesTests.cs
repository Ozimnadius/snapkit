using Snapkit.App.Paste;

namespace Snapkit.App.Tests;

/// <summary>Когда после переноса карточки в другое окно вставлять туда текст заметок.</summary>
public class DropNotesTests
{
    [Fact]
    public void Telegram_WithNotes_Pastes() => Assert.True(DropNotes.ShouldPaste("Telegram", "Снимок A\nA1 — кнопка"));

    [Fact]
    public void NoNotes_DoesNothing() => Assert.False(DropNotes.ShouldPaste("Telegram", ""));

    [Theory]
    [InlineData("explorer")]
    [InlineData("Explorer")]
    public void Explorer_SavesFileOnly(string process)
        // В папку или на рабочий стол переносят файл — Ctrl+V с текстом там ни к чему
        => Assert.False(DropNotes.ShouldPaste(process, "Снимок A\nA1 — кнопка"));

    [Fact]
    public void OwnOrUnknownWindow_DoesNothing() => Assert.False(DropNotes.ShouldPaste(null, "Снимок A\nA1 — кнопка"));
}
