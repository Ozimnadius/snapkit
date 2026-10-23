using Snapkit.Core.Model;

namespace Snapkit.Core.Session;

/// <summary>
/// Снимки ленты в её порядке. Не потокобезопасна: живёт на UI-потоке.
/// </summary>
public sealed class SessionModel
{
    public const int MaxCaptures = 20;
    public const int SoftWarningUnsent = 10;

    private readonly List<Capture> _captures = [];

    public IReadOnlyList<Capture> Captures => _captures;
    public int UnsentCount => _captures.Count(c => !c.IsSent);
    public bool IsFull => _captures.Count >= MaxCaptures;

    public event EventHandler? Changed;

    public void Add(Capture capture)
    {
        if (IsFull) throw new InvalidOperationException($"В ленте максимум {MaxCaptures} снимков.");
        _captures.Add(capture);
        RaiseChanged();
    }

    /// <summary>
    /// Заменяет снимок с тем же Id. Если изменилась разметка или общая заметка,
    /// снимок снова становится неотправленным. Неизвестный Id (снимок уже удалён) игнорируется.
    /// </summary>
    public void Update(Capture updated)
    {
        var index = _captures.FindIndex(c => c.Id == updated.Id);
        if (index < 0) return;
        var old = _captures[index];
        var edited = !ReferenceEquals(old.Annotations, updated.Annotations) || old.SnapshotNote != updated.SnapshotNote;
        _captures[index] = edited ? updated with { IsSent = false } : updated;
        RaiseChanged();
    }

    public Capture? Remove(Guid id)
    {
        var index = _captures.FindIndex(c => c.Id == id);
        if (index < 0) return null;
        var removed = _captures[index];
        _captures.RemoveAt(index);
        RaiseChanged();
        return removed;
    }

    public void Clear()
    {
        _captures.Clear();
        RaiseChanged();
    }

    public void MarkSent(IEnumerable<Guid> ids)
    {
        var set = ids.ToHashSet();
        for (var i = 0; i < _captures.Count; i++)
        {
            if (set.Contains(_captures[i].Id)) _captures[i] = _captures[i] with { IsSent = true };
        }
        RaiseChanged();
    }

    public void MarkAllUnsent()
    {
        for (var i = 0; i < _captures.Count; i++) _captures[i] = _captures[i] with { IsSent = false };
        RaiseChanged();
    }

    public void Load(IEnumerable<Capture> captures)
    {
        _captures.Clear();
        _captures.AddRange(captures.Take(MaxCaptures));
        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
