using System.Collections.Immutable;
using System.Windows.Media.Imaging;
using Snapkit.App.Imaging;
using Snapkit.App.Notifications;
using Snapkit.App.Rendering;
using Snapkit.App.Session;
using Snapkit.Core;
using Snapkit.Core.Model;

namespace Snapkit.App.Clipboarding;

/// <summary>
/// Что вставить по Ctrl+V: картинки по порядку, текст комментариев и снимки ленты,
/// которые после вставки пометить отправленными. FromStrip — это неотправленные снимки ленты.
/// </summary>
public sealed record PastePlan(IReadOnlyList<BitmapSource> Images, string Text, IReadOnlyList<Guid> CaptureIds, int NotesCount, bool FromStrip)
{
    public static PastePlan Empty { get; } = new([], "", [], 0, false);
    public bool IsEmpty => Images.Count == 0;
}

/// <summary>
/// Пакет в буфере обмена: картинка последнего снимка пакета и текст для чата.
/// Пакетом бывает лента (неотправленные снимки), один снимок из ленты или черновик из редактора.
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
        StripSource => session.Model.UnsentCount > 0,
        SingleSource single => session.Model.Captures.Any(c => c.Id == single.CaptureId),
        DraftSource => true,
        _ => false,
    };

    /// <summary>План вставки для того, что скопировано последним (строится на момент вставки).</summary>
    public PastePlan CreatePastePlan() => _current switch
    {
        StripSource => Plan(PackageSelector.Select(session.Model.Captures), fromStrip: true),
        SingleSource single when session.Model.Captures.FirstOrDefault(c => c.Id == single.CaptureId) is { } capture
            => Plan([new LetteredCapture('A', capture)], fromStrip: false),
        DraftSource draft => Plan([new LetteredCapture('A', draft.Capture)], fromStrip: false, draft.Image),
        _ => PastePlan.Empty,
    };

    /// <summary>Пакет ленты — неотправленные снимки.</summary>
    public Task PublishPackageAsync() => PublishAsync(new StripSource());

    /// <summary>«Копировать пакет»: если всё уже отправлено — снова вся лента (вставить во второй чат).</summary>
    public async Task CopyPackageCommandAsync()
    {
        if (session.Model.Captures.Count == 0)
        {
            Toasts.Show("Сначала сделайте снимок.");
            return;
        }
        if (session.Model.UnsentCount == 0) session.MarkAllUnsent();
        await PublishPackageAsync();
        var count = RussianPlural.Format(session.Model.UnsentCount, "снимок", "снимка", "снимков");
        Toasts.Show($"Пакет скопирован: {count}. Нажмите Ctrl+V в чате");
    }

    /// <summary>Один снимок из ленты — картинка и его комментарии.</summary>
    public async Task CopyCaptureAsync(Guid id)
    {
        if (session.Model.Captures.All(c => c.Id != id)) return;
        await PublishAsync(new SingleSource(id));
        Toasts.Show("Снимок скопирован. Нажмите Ctrl+V в чате");
    }

    /// <summary>Снимок из редактора (Ctrl+Shift+C), которого ещё нет в ленте, — картинка и комментарии.</summary>
    public Task CopyDraftAsync(BitmapSource source, ImmutableList<Annotation> annotations, string snapshotNote)
    {
        var draft = new Capture(Guid.Empty, source.PixelWidth, source.PixelHeight, new RectI(0, 0, source.PixelWidth, source.PixelHeight),
            annotations, snapshotNote, IsSent: false, DateTimeOffset.Now);
        return PublishAsync(new DraftSource(draft, source));
    }

    private async Task PublishAsync(Source source)
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
            ClipboardKind.Package, excludeFromHistory: false);
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
