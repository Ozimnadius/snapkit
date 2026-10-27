using Snapkit.Core.Model;

namespace Snapkit.Core.Session;

/// <summary>
/// Снимки ленты в её порядке. Не потокобезопасна: живёт на UI-потоке.
/// </summary>
public sealed class SessionModel
{
    public const int MaxCaptures = 20;
    public const int SoftWarningCount = 10;

    private readonly List<Capture> _captures = [];

    public IReadOnlyList<Capture> Captures => _captures;
    public bool IsFull => _captures.Count >= MaxCaptures;

    public event EventHandler? Changed;

    public void Add(Capture capture)
    {
        if (IsFull) throw new InvalidOperationException($"В ленте максимум {MaxCaptures} снимков.");
        _captures.Add(capture);
        RaiseChanged();
    }

    /// <summary>Заменяет снимок с тем же Id. Неизвестный Id (снимок уже удалён) игнорируется.</summary>
    public void Update(Capture updated)
    {
        var index = _captures.FindIndex(c => c.Id == updated.Id);
        if (index < 0) return;
        _captures[index] = updated;
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

    /// <summary>Удалить самый старый снимок (самый ранний CreatedAt, где бы он ни стоял); null — лента пуста.</summary>
    public Capture? EvictOldest()
    {
        var oldest = _captures.MinBy(c => c.CreatedAt);
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
