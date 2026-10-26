using Snapkit.App.Clipboarding;
using Snapkit.App.Imaging;
using Snapkit.App.Input;
using Snapkit.App.Interop;
using Snapkit.App.Session;
using Snapkit.App.Shell;
using Snapkit.Core;
using Snapkit.Core.Input;

namespace Snapkit.App.Paste;

public enum PasteStatus { CompletedVerified, CompletedUnverified, StoppedTargetChanged, StoppedByUser, ClipboardBusy, NothingToPaste }

public sealed record PasteOutcome(PasteStatus Status, int ImagesPasted, int NotesCount);

/// <summary>
/// Вставка пачки по перехваченному Ctrl+V: каждая картинка, затем текст — через буфер обмена
/// и программный Ctrl+V. Enter не нажимается никогда: сообщение отправляет пользователь.
/// </summary>
/// <param name="createPlan">Что вставить: план для последнего скопированного пакета.</param>
/// <param name="clearAfterPaste">Настройка «Очищать ленту после вставки».</param>
/// <param name="send">Отправка программных нажатий; по умолчанию SendInput (подменяется в тестах).</param>
public sealed class PasteCoordinator(SessionService session, ClipboardService clipboard, KeyRouter router, Func<PastePlan> createPlan,
    Func<bool> clearAfterPaste, Action<IReadOnlyList<KeyStroke>>? send = null)
{
    private readonly Action<IReadOnlyList<KeyStroke>> _send = send ?? InputSender.Send;

    // Сколько ждать, пока окно заберёт данные из буфера, и паузы между шагами.
    // Значения предварительные: проверены на тестовом окне WPF; в Claude Desktop вставка
    // проходит, в Chrome ещё не проверялась.
    private static readonly TimeSpan ConfirmTimeout = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan ShortConfirmTimeout = TimeSpan.FromMilliseconds(300);
    // Текст чаты забирают за миллисекунды; не забрали за это время — Ctrl+V, скорее всего, потерялся
    private static readonly TimeSpan TextFirstTimeout = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan VerifiedPause = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan UnverifiedPause = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan KeyReleaseWait = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan KeyPoll = TimeSpan.FromMilliseconds(15);
    // AutoHotkey после своего Ctrl+V возвращает Shift — даём ему закончить
    private static readonly TimeSpan SettleAfterRelease = TimeSpan.FromMilliseconds(40);

    public bool IsRunning { get; private set; }

    /// <summary>
    /// Сколько ждать подтверждения шага. Если получатель не подтвердил предыдущую картинку,
    /// дальше подтверждения тоже не будет — не ждём каждый раз по полторы секунды.
    /// </summary>
    public static TimeSpan ConfirmTimeoutFor(bool earlierStepUnconfirmed)
        => earlierStepUnconfirmed ? ShortConfirmTimeout : ConfirmTimeout;

    public async Task<PasteOutcome> PasteBatchAsync()
    {
        if (IsRunning) return new PasteOutcome(PasteStatus.NothingToPaste, 0, 0);
        IsRunning = true;
        router.EscapePressedDuringPaste = false;
        router.PasteInProgress = true;
        try
        {
            var target = Foreground.Current;
            var targetPid = Foreground.ProcessOf(target);
            Log.Info($"Вставка пачки в {ProcessTree.NameOf(targetPid)} ({targetPid})");
            var plan = createPlan();
            if (plan.IsEmpty) return new PasteOutcome(PasteStatus.NothingToPaste, 0, 0);
            // «Повторить последнюю вставку» вернёт эту пачку, даже если вставку остановят
            if (plan.FromStrip) session.RememberPaste(plan.CaptureIds);

            // Всё готовим заранее, чтобы между вставками не было долгих пауз
            var steps = plan.Images
                .Select(image => new ClipboardPayload(ImageCodecs.EncodePng(image), ImageCodecs.EncodeDib(image), null))
                .ToList();
            if (plan.Text.Length > 0) steps.Add(new ClipboardPayload(null, null, plan.Text));
            var notes = plan.NotesCount;

            await WaitForCtrlRelease();

            var verified = true;
            var images = 0;
            foreach (var step in steps)
            {
                if (Foreground.Current != target) return new PasteOutcome(PasteStatus.StoppedTargetChanged, images, notes);
                if (router.EscapePressedDuringPaste) return new PasteOutcome(PasteStatus.StoppedByUser, images, notes);

                var isText = step.Png is null;
                var taken = clipboard.ExpectRender(targetPid,
                    isText && verified ? TextFirstTimeout : ConfirmTimeoutFor(earlierStepUnconfirmed: !verified));
                await clipboard.PublishAsync(step, ClipboardKind.PasteStep, excludeFromHistory: true);
                router.Resync();
                clipboard.PasteKeysSending();
                _send(PasteKeys.CtrlV(router.Logical));
                var confirmed = await taken;
                // Telegram после картинки открывает окно с подписью: первый Ctrl+V с текстом приходит,
                // пока поле ещё не готово, и теряется. Текст точно не прочитан — нажимаем Ctrl+V ещё раз
                if (!confirmed && isText)
                {
                    if (Foreground.Current != target) return new PasteOutcome(PasteStatus.StoppedTargetChanged, images, notes);
                    if (router.EscapePressedDuringPaste) return new PasteOutcome(PasteStatus.StoppedByUser, images, notes);
                    Log.Info("Текст не забран — повторяю Ctrl+V");
                    taken = clipboard.ExpectRender(targetPid, ConfirmTimeout);
                    router.Resync();
                    clipboard.PasteKeysSending();
                    _send(PasteKeys.CtrlV(router.Logical));
                    confirmed = await taken;
                }
                verified &= confirmed;
                if (step.Png is not null) images++;
                await Task.Delay(confirmed ? VerifiedPause : UnverifiedPause);
            }

            if (plan.CaptureIds.Count > 0) session.MarkSent(plan.CaptureIds);
            if (plan.FromStrip && clearAfterPaste()) session.ClearAll();
            return new PasteOutcome(verified ? PasteStatus.CompletedVerified : PasteStatus.CompletedUnverified, images, notes);
        }
        catch (ClipboardBusyException)
        {
            return new PasteOutcome(PasteStatus.ClipboardBusy, 0, 0);
        }
        finally
        {
            // Флаги снимаются первыми: если Windows не примет нажатия, Ctrl+V не должен глотаться навсегда
            router.PasteInProgress = false;
            IsRunning = false;
            try
            {
                // Ни одна клавиша не должна остаться залипшей
                router.Resync();
                _send(PasteKeys.Restore(router.Physical, router.Logical));
            }
            catch (Exception ex)
            {
                Log.Error("восстановление клавиш после вставки", ex);
            }
        }
    }

    private async Task WaitForCtrlRelease()
    {
        var deadline = DateTime.UtcNow + KeyReleaseWait;
        router.Resync();
        while ((router.Logical & (Mod.LCtrl | Mod.RCtrl)) != 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(KeyPoll);
            router.Resync();
        }
        await Task.Delay(SettleAfterRelease);
    }

    /// <summary>Сообщение для пользователя по итогу вставки.</summary>
    public static string Describe(PasteOutcome outcome)
    {
        var images = RussianPlural.Format(outcome.ImagesPasted, "изображение", "изображения", "изображений");
        var notes = RussianPlural.Format(outcome.NotesCount, "заметка", "заметки", "заметок");
        var summary = outcome.NotesCount > 0 ? $"{images} и {notes}" : images;
        return outcome.Status switch
        {
            PasteStatus.CompletedVerified => $"Вставлено: {summary}",
            PasteStatus.CompletedUnverified => $"Вставлено: {summary}. Проверить вставку не удалось",
            PasteStatus.StoppedTargetChanged => "Вставка остановлена: окно сменилось",
            PasteStatus.StoppedByUser => "Вставка остановлена",
            PasteStatus.ClipboardBusy => "Буфер обмена занят другой программой. Повторите вставку",
            _ => "",
        };
    }
}
