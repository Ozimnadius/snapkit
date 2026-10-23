using System.Collections.Immutable;
using System.IO;
using System.Windows.Media.Imaging;
using Snapkit.App.Imaging;
using Snapkit.App.Rendering;
using Snapkit.App.Shell;
using Snapkit.Core;
using Snapkit.Core.Model;
using Snapkit.Core.Session;

namespace Snapkit.App.Session;

public enum SessionChangeKind { Added, Updated, Removed, Cleared, SentMarked }

public sealed record SessionChange(SessionChangeKind Kind, Guid? CaptureId);

/// <summary>
/// Лента снимков для всей программы: модель, исходные картинки в памяти и запись на диск.
/// Живёт на UI-потоке. Запись идёт в фоне строго по очереди, ошибки записи не роняют программу.
/// </summary>
public sealed class SessionService(SessionStore store)
{
    private readonly Dictionary<Guid, BitmapSource> _sources = [];
    private readonly object _queueLock = new();
    private Task _tail = Task.CompletedTask;
    private bool _saveQueued;
    private volatile IReadOnlyList<Capture> _latest = [];

    public SessionModel Model { get; } = new();

    public event EventHandler<SessionChange>? Changed;

    /// <summary>Ошибка фоновой записи на диск (вызывается с фонового потока).</summary>
    public event EventHandler<Exception>? SaveFailed;

    public BitmapSource GetSource(Guid id)
    {
        if (_sources.TryGetValue(id, out var cached)) return cached;
        var source = ImageCodecs.DecodePng(File.ReadAllBytes(store.SourceImagePath(id)));
        _sources[id] = source;
        return source;
    }

    public Guid AddCapture(BitmapSource source, RectI screenBounds, ImmutableList<Annotation> annotations, string snapshotNote)
    {
        var frozen = source.IsFrozen ? source : ImageCodecs.Normalize(source);
        var capture = new Capture(Guid.NewGuid(), frozen.PixelWidth, frozen.PixelHeight, screenBounds,
            annotations, snapshotNote, IsSent: false, DateTimeOffset.Now);
        _sources[capture.Id] = frozen;
        Model.Add(capture);
        Enqueue(() => store.WriteSourceImage(capture.Id, ImageCodecs.EncodePng(frozen)));
        Persist();
        Raise(SessionChangeKind.Added, capture.Id);
        return capture.Id;
    }

    public void UpdateCapture(Capture updated)
    {
        Model.Update(updated);
        Persist();
        Raise(SessionChangeKind.Updated, updated.Id);
    }

    public void Remove(Guid id)
    {
        if (Model.Remove(id) is null) return;
        _sources.Remove(id);
        Persist();
        Raise(SessionChangeKind.Removed, id);
    }

    public void MarkSent(IEnumerable<Guid> ids)
    {
        Model.MarkSent(ids);
        Persist();
        Raise(SessionChangeKind.SentMarked, null);
    }

    public void MarkAllUnsent()
    {
        Model.MarkAllUnsent();
        Persist();
        Raise(SessionChangeKind.SentMarked, null);
    }

    /// <summary>Очистить ленту в памяти и на диске.</summary>
    public void ClearAll()
    {
        Model.Clear();
        _sources.Clear();
        _latest = [];
        Enqueue(store.Clear);
        Raise(SessionChangeKind.Cleared, null);
    }

    /// <summary>
    /// Загрузить ленту, оставшуюся на диске после сбоя или перезагрузки. Снимки, которые не читаются
    /// (битая картинка, испорченная разметка), отбрасываются — иначе программа ломалась бы при каждом запуске.
    /// </summary>
    public int RestoreFromDisk()
    {
        var usable = new List<Capture>();
        var dropped = 0;
        foreach (var capture in store.Load())
        {
            var normalized = capture with { SnapshotNote = capture.SnapshotNote ?? "" };
            if (IsUsable(normalized)) usable.Add(normalized);
            else dropped++;
        }
        if (dropped > 0) Log.Info($"При восстановлении ленты отброшено испорченных снимков: {dropped}");

        Model.Load(usable);
        _latest = Model.Captures.ToList();
        if (dropped > 0) Persist();
        Enqueue(() => store.RemoveOrphanImages(_latest));
        if (Model.Captures.Count > 0) Raise(SessionChangeKind.Updated, null);
        return Model.Captures.Count;
    }

    private bool IsUsable(Capture capture)
    {
        try
        {
            if (capture.Annotations is null || capture.PixelWidth <= 0 || capture.PixelHeight <= 0) return false;
            foreach (var a in capture.Annotations)
            {
                if (a is null || a.Points.IsDefault || !Enum.IsDefined(a.Kind) || !double.IsFinite(a.Thickness)) return false;
                var required = a.Kind is AnnotationKind.Rectangle or AnnotationKind.Arrow ? 2 : 1;
                if (a.Points.Length < required) return false;
                _ = Palette.Color(a.Color); // бросит, если цвет не читается
            }
            return GetSource(capture.Id).PixelWidth > 0;
        }
        catch (Exception)
        {
            _sources.Remove(capture.Id);
            return false;
        }
    }

    /// <summary>Дождаться окончания фоновой записи (при выходе и в тестах).</summary>
    public Task FlushAsync()
    {
        lock (_queueLock) return _tail;
    }

    // Запись состояния ставится в очередь один раз; к моменту выполнения берётся последнее состояние.
    private void Persist()
    {
        _latest = Model.Captures.ToList();
        lock (_queueLock)
        {
            if (_saveQueued) return;
            _saveQueued = true;
        }
        Enqueue(() =>
        {
            lock (_queueLock) _saveQueued = false;
            store.Save(_latest);
        });
    }

    private void Enqueue(Action work)
    {
        lock (_queueLock)
        {
            _tail = _tail.ContinueWith(_ =>
            {
                try { work(); }
                catch (Exception ex) { SaveFailed?.Invoke(this, ex); }
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }

    private void Raise(SessionChangeKind kind, Guid? id) => Changed?.Invoke(this, new SessionChange(kind, id));
}
