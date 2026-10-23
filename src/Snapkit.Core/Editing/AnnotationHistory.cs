using System.Collections.Immutable;
using Snapkit.Core.Model;

namespace Snapkit.Core.Editing;

/// <summary>
/// История правок разметки для «Отменить / Повторить». Каждое состояние — неизменяемый список,
/// поэтому шаги хранятся без копирования.
/// </summary>
public sealed class AnnotationHistory(ImmutableList<Annotation> initial)
{
    private readonly Stack<ImmutableList<Annotation>> _undo = new();
    private readonly Stack<ImmutableList<Annotation>> _redo = new();

    public ImmutableList<Annotation> Current { get; private set; } = initial;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Разметка отличается от той, с которой открыли редактор.</summary>
    public bool IsChanged => !ReferenceEquals(Current, initial);

    public void Push(ImmutableList<Annotation> next)
    {
        _undo.Push(Current);
        _redo.Clear();
        Current = next;
    }

    public void Undo()
    {
        if (!CanUndo) return;
        _redo.Push(Current);
        Current = _undo.Pop();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        _undo.Push(Current);
        Current = _redo.Pop();
    }
}
