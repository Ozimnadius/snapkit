using System.ComponentModel;
using System.IO;
using Snapkit.App.Clipboarding;
using Snapkit.App.Paste;
using Snapkit.App.Session;
using Snapkit.Core.Input;
using Snapkit.Core.Session;

namespace Snapkit.App.Tests;

public class PasteCoordinatorTests
{
    [Fact]
    public void PasteBatch_WhenKeyRestoreFails_StillResetsFlags() => StaRunner.Run(() =>
    {
        // Windows может отказаться принять программные нажатия (экран блокировки, запрос UAC).
        // Флаг «идёт вставка» всё равно должен сняться, иначе Ctrl+V будет глотаться до перезапуска.
        var dir = Path.Combine(Path.GetTempPath(), "snapkit-tests-" + Guid.NewGuid());
        using var clipboard = new ClipboardService();
        var router = new KeyRouter(() => default);
        var paste = new PasteCoordinator(new SessionService(new SessionStore(dir)), clipboard, router, () => PastePlan.Empty,
            () => false, send: _ => throw new Win32Exception(5));

        var task = paste.PasteBatchAsync(); // пустая лента — завершается сразу, без ожиданий
        Assert.True(task.IsCompletedSuccessfully);
#pragma warning disable xUnit1031 // задача уже завершена, блокировки нет
        Assert.Equal(PasteStatus.NothingToPaste, task.Result.Status);
#pragma warning restore xUnit1031
        Assert.False(router.PasteInProgress);
        Assert.False(paste.IsRunning);
    });

    [Fact]
    public void ConfirmTimeout_IsShortAfterFirstUnconfirmedStep()
    {
        // Если получатель не подтвердил первую картинку, дальше подтверждения тоже не будет —
        // не ждём на каждом шаге по полторы секунды
        Assert.Equal(TimeSpan.FromMilliseconds(1500), PasteCoordinator.ConfirmTimeoutFor(earlierStepUnconfirmed: false));
        Assert.Equal(TimeSpan.FromMilliseconds(300), PasteCoordinator.ConfirmTimeoutFor(earlierStepUnconfirmed: true));
    }

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
