using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

/// <summary>Вставка через настоящий буфер обмена — при сборке не запускается.</summary>
[Trait("Category", "Clipboard")]
[Collection("Clipboard")]
public class PasteCoordinatorClipboardTests
{
    [Fact]
    public void AnonymousReadWhileCtrlVIsBeingSent_ConfirmsStep()
    {
        // Chrome читает буфер без своего окна и так быстро, что запрос данных приходит,
        // пока SendInput ещё не вернулся: поток в это время обрабатывает входящие сообщения
        var dir = Path.Combine(Path.GetTempPath(), "snapkit-tests-" + Guid.NewGuid());
        using var host = new ClipboardTestHost();
        using var reader = new ForeignClipboard();
        var image = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, new byte[4 * 4 * 4], 4 * 4);
        image.Freeze();
        var plan = new PastePlan([image], "A1 — проверка", [], NotesCount: 1, FromStrip: false);

        PasteOutcome? outcome = null;
        host.Run(async () =>
        {
            var paste = new PasteCoordinator(new SessionService(new SessionStore(dir)), host.Service, new KeyRouter(() => default),
                () => plan, () => false,
                send: _ => reader.ReadAnonymously(reader.HasFormat("PNG")
                    ? ForeignClipboard.Format("PNG")
                    : ForeignClipboard.CF_UNICODETEXT));
            outcome = await paste.PasteBatchAsync();
        });

        Assert.Equal(new PasteOutcome(PasteStatus.CompletedVerified, 1, 1), outcome);
    }
}
