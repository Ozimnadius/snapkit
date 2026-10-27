using System.Collections.Immutable;
using System.Windows.Media.Imaging;
using Snapkit.App.Imaging;
using Snapkit.App.Notifications;
using Snapkit.App.Rendering;
using Snapkit.App.Session;
using Snapkit.Core;
using Snapkit.Core.Model;
using Snapkit.Core.Session;

namespace Snapkit.App.Clipboarding;

/// <summary>
/// Что вставить по Ctrl+V: картинки по порядку, текст комментариев и снимки ленты в плане.
/// FromStrip — пакет всей ленты (после него срабатывает «Очищать ленту после вставки»).
/// </summary>
public sealed record PastePlan(IReadOnlyList<BitmapSource> Images, string Text, IReadOnlyList<Guid> CaptureIds, int NotesCount, bool FromStrip)
{
    public static PastePlan Empty { get; } = new([], "", [], 0, false);
    public bool IsEmpty => Images.Count == 0;
}

/// <summary>
/// Пакет в буфере обмена: картинка последнего снимка пакета и текст для чата.
/// Пакетом бывает вся лента, один снимок из ленты или черновик из редактора; после вставки
/// пакет возвращается в буфер.
/// Пока пакет в буфере, Ctrl+V вставляет его целиком — картинки и текст; без перехвата обычная
/// вставка даст хотя бы картинку (Paint) или текст (Блокнот).
/// </summary>
public sealed class PackagePublisher(SessionService session, ClipboardService clipboard)
{
    private abstract record Source;
    private sealed record StripSource : Source;
    private sealed record SingleSource(Guid CaptureId) : Source;
    private sealed record DraftSource(Capture Capture, BitmapSource Image) : Source;

    // Что лежит в буфере сейчас; меняется только на UI-потоке
    private Source? _current;

    /// <summary>Есть что вставить по Ctrl+V из последнего скопированного пакета.</summary>
    public bool HasPasteContent => _current switch
    {
        StripSource => session.Model.Captures.Count > 0,
        SingleSource single => session.Model.Captures.Any(c => c.Id == single.CaptureId),
        DraftSource => true,
        _ => false,
    };

    /// <summary>Последним в буфер положен пакет ленты (а не один снимок или черновик).</summary>
    public bool IsStripPackage => _current is StripSource;

    /// <summary>План вставки для того, что скопировано последним (строится на момент вставки).</summary>
    public PastePlan CreatePastePlan() => _current switch
    {
        StripSource => Plan(PackageSelector.All(session.Model.Captures), fromStrip: true),
        SingleSource single when session.Model.Captures.FirstOrDefault(c => c.Id == single.CaptureId) is { } capture
            => Plan([new LetteredCapture(CaptureLetters.Assign(session.Model.Captures)[capture.Id], capture)], fromStrip: false),
        DraftSource draft => Plan([new LetteredCapture(CaptureLetters.ForNew(session.Model.Captures.Count, SessionModel.MaxCaptures),
            draft.Capture)], fromStrip: false, draft.Image),
        _ => PastePlan.Empty,
    };

    /// <summary>
    /// После вставки по Ctrl+V вернуть в буфер то, что вставили: следующий Ctrl+V в другом чате снова
    /// вставит картинки и текст, а не последний шаг вставки. В историю буфера повтор не пишется —
    /// там уже есть первая копия.
    /// </summary>
    public Task RestoreAfterPasteAsync(PastePlan pasted)
        => _current is null ? Task.CompletedTask : PublishAsync(_current, excludeFromHistory: true);

    /// <summary>Пакет всей ленты.</summary>
    public Task PublishPackageAsync() => PublishAsync(new StripSource());

    /// <summary>«Копировать всё»: вся лента — Ctrl+V вставит все снимки по порядку.</summary>
    public async Task CopyAllCommandAsync()
    {
        if (session.Model.Captures.Count == 0)
        {
            Toasts.Show("Сначала сделайте снимок.");
            return;
        }
        await PublishPackageAsync();
        var count = RussianPlural.Format(session.Model.Captures.Count, "снимок", "снимка", "снимков");
        Toasts.Show($"Скопировано: {count}. Нажмите Ctrl+V в чате");
    }

    /// <summary>Один снимок из ленты — картинка и его комментарии (кнопка «Копировать снимок»).</summary>
    public async Task CopyCaptureAsync(Guid id)
    {
        if (session.Model.Captures.All(c => c.Id != id)) return;
        await PublishCaptureAsync(id);
        Toasts.Show("Снимок скопирован. Нажмите Ctrl+V в чате");
    }

    /// <summary>Один снимок в буфер без сообщения — после «Готово» в редакторе.</summary>
    public Task PublishCaptureAsync(Guid id)
        => session.Model.Captures.Any(c => c.Id == id) ? PublishAsync(new SingleSource(id)) : Task.CompletedTask;

    /// <summary>Снимок из редактора (Ctrl+Shift+C), которого ещё нет в ленте, — картинка и комментарии.</summary>
    public Task CopyDraftAsync(BitmapSource source, ImmutableList<Annotation> annotations, string snapshotNote)
    {
        var draft = new Capture(Guid.Empty, source.PixelWidth, source.PixelHeight, new RectI(0, 0, source.PixelWidth, source.PixelHeight),
            annotations, snapshotNote, DateTimeOffset.Now);
        return PublishAsync(new DraftSource(draft, source));
    }

    private async Task PublishAsync(Source source, bool excludeFromHistory = false)
    {
        var previous = _current;
        _current = source;
        var plan = CreatePastePlan();
        if (plan.IsEmpty)
        {
            _current = previous;
            return;
        }
        var last = plan.Images[^1];
        await clipboard.PublishAsync(
            new ClipboardPayload(ImageCodecs.EncodePng(last), ImageCodecs.EncodeDib(last), plan.Text.Length == 0 ? null : plan.Text),
            ClipboardKind.Package, excludeFromHistory);
    }

    private PastePlan Plan(IReadOnlyList<LetteredCapture> package, bool fromStrip, BitmapSource? draftImage = null)
    {
        if (package.Count == 0) return PastePlan.Empty;
        var images = package
            .Select(p => CaptureRenderer.Render(draftImage ?? session.GetSource(p.Capture.Id), p.Capture, p.Letter))
            .ToList();
        var ids = draftImage is null ? package.Select(p => p.Capture.Id).ToList() : [];
        return new PastePlan(images, PromptBuilder.Build(package), ids, package.Sum(p => p.Capture.NoteCount), fromStrip);
    }
}
