using System.Collections.Immutable;
using Snapkit.Core.Editing;
using Snapkit.Core.Model;
using static Snapkit.Core.Tests.TestData;

namespace Snapkit.Core.Tests;

public class AnnotationHistoryTests
{
    private static readonly ImmutableList<Annotation> Empty = [];

    [Fact]
    public void Push_Undo_Redo_RestoresStates()
    {
        var h = new AnnotationHistory(Empty);
        var one = Empty.Add(Shape(AnnotationKind.Rectangle));
        var two = one.Add(Shape(AnnotationKind.Arrow));
        h.Push(one);
        h.Push(two);
        Assert.Same(two, h.Current);
        h.Undo();
        Assert.Same(one, h.Current);
        Assert.True(h.CanRedo);
        h.Redo();
        Assert.Same(two, h.Current);
        Assert.False(h.CanRedo);
    }

    [Fact]
    public void Push_AfterUndo_ClearsRedo()
    {
        var h = new AnnotationHistory(Empty);
        h.Push(Empty.Add(Shape(AnnotationKind.Rectangle)));
        h.Undo();
        h.Push(Empty.Add(Shape(AnnotationKind.Arrow)));
        Assert.False(h.CanRedo);
    }

    [Fact]
    public void Undo_WithNothingToUndo_KeepsCurrent()
    {
        var h = new AnnotationHistory(Empty);
        Assert.False(h.CanUndo);
        h.Undo();
        Assert.Same(Empty, h.Current);
    }

    [Fact]
    public void IsChanged_FalseAfterUndoToInitial()
    {
        var h = new AnnotationHistory(Empty);
        Assert.False(h.IsChanged);
        h.Push(Empty.Add(Shape(AnnotationKind.Rectangle)));
        Assert.True(h.IsChanged);
        h.Undo();
        Assert.False(h.IsChanged);
    }
}
