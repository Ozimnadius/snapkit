using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.App.Clipboarding;
using Snapkit.App.Paste;
using Snapkit.App.Session;
using Snapkit.Core;
using Snapkit.Core.Input;
using Snapkit.Core.Model;
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

    [Fact]
    public void TextNotTakenFirstTime_IsPastedAgainOnce()
    {
        // Telegram после картинки открывает окно с подписью: первый Ctrl+V с текстом приходит,
        // пока поле ещё не готово, и теряется — Snapkit нажимает Ctrl+V ещё раз
        var dir = Path.Combine(Path.GetTempPath(), "snapkit-tests-" + Guid.NewGuid());
        using var host = new ClipboardTestHost();
        using var reader = new ForeignClipboard();
        var image = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, new byte[4 * 4 * 4], 4 * 4);
        image.Freeze();
        var plan = new PastePlan([image], "A1 — проверка", [], NotesCount: 1, FromStrip: false);
        var presses = 0;
        PasteOutcome? outcome = null;
        host.Run(async () =>
        {
            var paste = new PasteCoordinator(new SessionService(new SessionStore(dir)), host.Service, new KeyRouter(() => default),
                () => plan, () => false,
                send: strokes =>
                {
                    // Считаем только Ctrl+V; в конце вставки тем же путём возвращаются модификаторы
                    if (!strokes.Any(k => k.Vk == 0x56 && !k.Up)) return;
                    presses++;
                    if (presses == 1) reader.ReadAnonymously(ForeignClipboard.Format("PNG"));
                    if (presses == 3) reader.ReadAnonymously(ForeignClipboard.CF_UNICODETEXT);
                });
            outcome = await paste.PasteBatchAsync();
        });

        Assert.Contains(outcome?.Status, new PasteStatus?[] { PasteStatus.CompletedVerified, PasteStatus.StoppedTargetChanged });
        if (outcome?.Status == PasteStatus.CompletedVerified) Assert.Equal(3, presses);
    }

    [Fact]
    public void AfterPaste_PackageIsPutBack()
    {
        // После вставки в один чат Ctrl+V в другом снова вставит картинку и текст, а не один текст
        var dir = Path.Combine(Path.GetTempPath(), "snapkit-tests-" + Guid.NewGuid());
        using var host = new ClipboardTestHost();
        using var reader = new ForeignClipboard();
        SessionService? session = null;
        PackagePublisher? publisher = null;
        PasteOutcome? outcome = null;
        host.Run(async () =>
        {
            var image = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, new byte[4 * 4 * 4], 4 * 4);
            image.Freeze();
            session = new SessionService(new SessionStore(dir));
            session.AddCapture(image, new RectI(0, 0, 4, 4), [], "");
            publisher = new PackagePublisher(session, host.Service);
            await publisher.PublishPackageAsync();
            var paste = new PasteCoordinator(session, host.Service, new KeyRouter(() => default),
                publisher.CreatePastePlan, () => false,
                send: _ => reader.ReadAnonymously(reader.HasFormat("PNG")
                    ? ForeignClipboard.Format("PNG")
                    : ForeignClipboard.CF_UNICODETEXT),
                restoreAfter: publisher.RestoreAfterPasteAsync);
            outcome = await paste.PasteBatchAsync();
        });

        Assert.Contains(outcome?.Status, new PasteStatus?[] { PasteStatus.CompletedVerified, PasteStatus.StoppedTargetChanged });
        Assert.True(host.Service.IsPackageCurrent());
        Assert.True(publisher!.HasPasteContent);
        host.Run(() => session!.FlushAsync());
    }

    [Fact]
    public void PasteText_DoesNotPutPackageBack()
    {
        // Заметки после переноса карточки: если окно их не забрало, они остаются в буфере для ручного
        // Ctrl+V — пакет (к тому же, может быть, другого снимка) поверх не кладётся
        var dir = Path.Combine(Path.GetTempPath(), "snapkit-tests-" + Guid.NewGuid());
        using var host = new ClipboardTestHost();
        using var reader = new ForeignClipboard();
        var restored = 0;
        host.Run(async () =>
        {
            await host.Service.PublishAsync(new ClipboardPayload(null, null, "пакет"), ClipboardKind.Package, excludeFromHistory: true);
            var paste = new PasteCoordinator(new SessionService(new SessionStore(dir)), host.Service, new KeyRouter(() => default),
                () => PastePlan.Empty, () => false,
                send: strokes =>
                {
                    if (strokes.Any(k => k.Vk == 0x56 && !k.Up)) reader.ReadAnonymously(ForeignClipboard.CF_UNICODETEXT);
                },
                restoreAfter: _ =>
                {
                    restored++;
                    return Task.CompletedTask;
                });
            await paste.PasteTextAsync("Снимок A", notesCount: 0);
        });

        Assert.Equal(0, restored);
    }

    [Fact]
    public void PasteText_PastesOnlyText()
    {
        // После переноса карточки в Telegram туда же вставляется текст заметок — без картинки
        var dir = Path.Combine(Path.GetTempPath(), "snapkit-tests-" + Guid.NewGuid());
        using var host = new ClipboardTestHost();
        using var reader = new ForeignClipboard();
        var presses = 0;
        PasteOutcome? outcome = null;
        host.Run(async () =>
        {
            var paste = new PasteCoordinator(new SessionService(new SessionStore(dir)), host.Service, new KeyRouter(() => default),
                () => PastePlan.Empty, () => false,
                send: strokes =>
                {
                    if (!strokes.Any(k => k.Vk == 0x56 && !k.Up)) return;
                    presses++;
                    reader.ReadAnonymously(ForeignClipboard.CF_UNICODETEXT);
                });
            outcome = await paste.PasteTextAsync("Снимок A" + Environment.NewLine + "A1 — кнопка", notesCount: 1);
        });

        Assert.Contains(outcome?.Status, new PasteStatus?[] { PasteStatus.CompletedVerified, PasteStatus.StoppedTargetChanged });
        if (outcome?.Status == PasteStatus.CompletedVerified)
            Assert.Equal((1, 0, 1), (presses, outcome.ImagesPasted, outcome.NotesCount));
    }
}
