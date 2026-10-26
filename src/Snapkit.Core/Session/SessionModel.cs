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

    /// <summary>Есть место для нового снимка: лента не полна или в ней есть отправленный, которого можно вытеснить.</summary>
    public bool CanMakeRoom => !IsFull || _captures.Any(c => c.IsSent);

    /// <summary>Отметка «отправлен» одного снимка (щелчок по значку карточки). Неизвестный Id — ничего.</summary>
    public void SetSent(Guid id, bool isSent)
    {
        var index = _captures.FindIndex(c => c.Id == id);
        if (index < 0 || _captures[index].IsSent == isSent) return;
        _captures[index] = _captures[index] with { IsSent = isSent };
        RaiseChanged();
    }

    /// <summary>«Повторить последнюю вставку»: эти снимки — неотправленные, остальные — отправленные. Отсутствующие Id пропускаются.</summary>
    public void ResendOnly(IReadOnlyCollection<Guid> ids)
    {
        var set = ids.ToHashSet();
        for (var i = 0; i < _captures.Count; i++) _captures[i] = _captures[i] with { IsSent = !set.Contains(_captures[i].Id) };
        RaiseChanged();
    }

    /// <summary>Поставить снимок перед карточкой с номером slot в нынешнем порядке (slot == Count — в конец).</summary>
    public void MoveBefore(Guid id, int slot)
    {
        var index = _captures.FindIndex(c => c.Id == id);
        if (index < 0) return;
        slot = Math.Clamp(slot, 0, _captures.Count);
        // После изъятия карточки все места за ней сдвигаются на одно
        var target = slot > index ? slot - 1 : slot;
        if (target == index) return;
        var capture = _captures[index];
        _captures.RemoveAt(index);
        _captures.Insert(target, capture);
        RaiseChanged();
    }

    /// <summary>Вставить снимок на место index (обрезается до 0…Count). Бросает, если лента полна.</summary>
    public void Insert(int index, Capture capture)
    {
        if (IsFull) throw new InvalidOperationException($"В ленте максимум {MaxCaptures} снимков.");
        _captures.Insert(Math.Clamp(index, 0, _captures.Count), capture);
        RaiseChanged();
    }

    /// <summary>Удалить отправленный снимок с самым ранним CreatedAt; null — отправленных нет.</summary>
    public Capture? EvictOldestSent()
    {
        var oldest = _captures.Where(c => c.IsSent).MinBy(c => c.CreatedAt);
        return oldest is null ? null : Remove(oldest.Id);
    }

    public void Load(IEnumerable<Capture> captures)
    {
        _captures.Clear();
        _captures.AddRange(captures.Take(MaxCaptures));
        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
