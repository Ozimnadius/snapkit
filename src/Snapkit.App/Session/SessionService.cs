using System.Collections.Immutable;
using System.IO;
using System.Windows.Media.Imaging;
using Snapkit.App.Imaging;
using Snapkit.App.Rendering;
using Snapkit.App.Shell;
using Snapkit.Core;
using Snapkit.Core.Editing;
using Snapkit.Core.Model;
using Snapkit.Core.Session;

namespace Snapkit.App.Session;

public enum SessionChangeKind { Added, Updated, Removed, Cleared, SentMarked, Reordered, Restored }

public sealed record SessionChange(SessionChangeKind Kind, Guid? CaptureId);

/// <summary>Удалённый снимок для «Вернуть»: разметка, картинка и место в ленте.</summary>
public sealed record RemovedCapture(Capture Capture, BitmapSource Source, int Index);

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

    /// <summary>
    /// Новый снимок в конец ленты. Полная лента сначала освобождает место от самого старого
    /// отправленного снимка; если все неотправленные — бросает (вызывающий проверяет Model.CanMakeRoom).
    /// </summary>
    public Guid AddCapture(BitmapSource source, RectI screenBounds, ImmutableList<Annotation> annotations, string snapshotNote)
    {
        MakeRoom();
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

    /// <summary>Снимок обрезали: новый исходник в памяти и на диске, модель обновлена.</summary>
    public void UpdateCaptureImage(Capture updated, BitmapSource source)
    {
        var frozen = source.IsFrozen ? source : ImageCodecs.Normalize(source);
        _sources[updated.Id] = frozen;
        // Картинка другая — снимок снова нужно отправить, даже если разметка та же
        Model.Update(updated with { IsSent = false });
        Enqueue(() => store.WriteSourceImage(updated.Id, ImageCodecs.EncodePng(frozen)));
        Persist();
        Raise(SessionChangeKind.Updated, updated.Id);
    }

    /// <summary>Удалить снимок; возвращает всё, что нужно для «Вернуть», или null, если его нет.</summary>
    public RemovedCapture? Remove(Guid id)
    {
        var index = Model.Captures.ToList().FindIndex(c => c.Id == id);
        if (index < 0) return null;
        BitmapSource source;
        try { source = GetSource(id); }
        catch (Exception) { source = EmptySource; }
        var capture = Model.Remove(id)!;
        _sources.Remove(id);
        Persist();
        Raise(SessionChangeKind.Removed, id);
        return new RemovedCapture(capture, source, index);
    }

    /// <summary>«Вернуть»: снимок на прежнее место с прежней отметкой. false — места нет.</summary>
    public bool Restore(RemovedCapture removed)
    {
        if (Model.Captures.Any(c => c.Id == removed.Capture.Id) || !Model.CanMakeRoom) return false;
        MakeRoom();
        _sources[removed.Capture.Id] = removed.Source;
        Model.Insert(removed.Index, removed.Capture);
        // Картинка на диске могла быть уже убрана — пишем её заново
        Enqueue(() => store.WriteSourceImage(removed.Capture.Id, ImageCodecs.EncodePng(removed.Source)));
        Persist();
        Raise(SessionChangeKind.Restored, removed.Capture.Id);
        return true;
    }

    /// <summary>Перетащили карточку: поставить снимок перед карточкой с номером slot.</summary>
    public void MoveBefore(Guid id, int slot)
    {
        var before = Model.Captures.ToList();
        Model.MoveBefore(id, slot);
        if (before.SequenceEqual(Model.Captures)) return;
        Persist();
        Raise(SessionChangeKind.Reordered, id);
    }

    // Полная лента: вытеснить самый старый отправленный снимок (бросает, если вытеснять нечего)
    private void MakeRoom()
    {
        if (!Model.IsFull) return;
        var evicted = Model.EvictOldestSent()
                      ?? throw new InvalidOperationException($"В ленте {SessionModel.MaxCaptures} неотправленных снимков.");
        _sources.Remove(evicted.Id);
        Log.Info("Лента полна — вытеснен самый старый отправленный снимок");
        Persist();
        Raise(SessionChangeKind.Removed, evicted.Id);
    }

    private static readonly BitmapSource EmptySource = CreateEmptySource();

    private static BitmapSource CreateEmptySource()
    {
        var image = BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[4], 4);
        image.Freeze();
        return image;
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
        Log.Info($"Лента очищена, снимков было {Model.Captures.Count}");
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
                if (a is null || !AnnotationGeometry.IsWellFormed(a)) return false;
                // Бросит, если цвет не читается
                _ = Palette.Color(a.Color);
                if (a.FillColor is not null) _ = Palette.Color(a.FillColor);
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
