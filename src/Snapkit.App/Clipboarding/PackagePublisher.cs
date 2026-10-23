using System.Windows.Media.Imaging;
using Snapkit.App.Imaging;
using Snapkit.App.Notifications;
using Snapkit.App.Rendering;
using Snapkit.App.Session;
using Snapkit.Core;

namespace Snapkit.App.Clipboarding;

/// <summary>
/// Пакет в буфере обмена: картинка последнего снимка пакета и текст для чата.
/// Пока он в буфере, Ctrl+V вставляет всю пачку; без перехвата обычная вставка даст хотя бы
/// картинку (Paint) или текст (Блокнот).
/// </summary>
public sealed class PackagePublisher(SessionService session, ClipboardService clipboard)
{
    public async Task PublishPackageAsync()
    {
        var package = PackageSelector.Select(session.Model.Captures);
        if (package.Count == 0) return;
        var last = package[^1];
        var image = CaptureRenderer.Render(session.GetSource(last.Capture.Id), last.Capture, last.Letter);
        var text = PromptBuilder.Build(package);
        await clipboard.PublishAsync(
            new ClipboardPayload(ImageCodecs.EncodePng(image), ImageCodecs.EncodeDib(image), text.Length == 0 ? null : text),
            ClipboardKind.Package, excludeFromHistory: false);
    }

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

    /// <summary>Один снимок из ленты как обычная картинка — без перехвата вставки.</summary>
    public async Task CopyCaptureAsync(Guid id)
    {
        if (session.Model.Captures.FirstOrDefault(c => c.Id == id) is not { } capture) return;
        var letter = CaptureLetters.Assign(session.Model.Captures).TryGetValue(id, out var l) ? l : 'A';
        await CopyImageAsync(CaptureRenderer.Render(session.GetSource(id), capture, letter));
        Toasts.Show($"Снимок {letter} скопирован");
    }

    /// <summary>Картинка из редактора (Ctrl+Shift+C) как обычная картинка.</summary>
    public Task CopyImageAsync(BitmapSource image)
        => clipboard.PublishAsync(new ClipboardPayload(ImageCodecs.EncodePng(image), ImageCodecs.EncodeDib(image), null),
            ClipboardKind.SingleCapture, excludeFromHistory: false);
}
