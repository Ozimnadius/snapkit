using Snapkit.App.Paste;

namespace Snapkit.App.Tests;

public class PasteCoordinatorTests
{
    [Theory]
    [InlineData(PasteStatus.CompletedVerified, 2, 3, "Вставлено: 2 изображения и 3 заметки")]
    [InlineData(PasteStatus.CompletedVerified, 1, 0, "Вставлено: 1 изображение")]
    [InlineData(PasteStatus.CompletedUnverified, 5, 1, "Вставлено: 5 изображений и 1 заметка. Проверить вставку не удалось")]
    [InlineData(PasteStatus.StoppedTargetChanged, 1, 1, "Вставка остановлена: окно сменилось")]
    [InlineData(PasteStatus.StoppedByUser, 0, 0, "Вставка остановлена")]
    [InlineData(PasteStatus.ClipboardBusy, 0, 0, "Буфер обмена занят другой программой. Повторите вставку")]
    public void Describe_GivesRussianMessage(PasteStatus status, int images, int notes, string expected)
        => Assert.Equal(expected, PasteCoordinator.Describe(new PasteOutcome(status, images, notes)));
}
