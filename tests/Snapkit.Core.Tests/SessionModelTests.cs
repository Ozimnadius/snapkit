using Snapkit.Core.Model;
using Snapkit.Core.Session;
using static Snapkit.Core.Tests.TestData;

namespace Snapkit.Core.Tests;

public class SessionModelTests
{
    [Fact]
    public void Add_AppendsAndRaisesChanged()
    {
        var model = new SessionModel();
        model.Add(Cap());
        var changed = 0;
        model.Changed += (_, _) => changed++;
        var last = Cap();
        model.Add(last);
        Assert.Same(last, model.Captures[^1]);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Add_WhenFull_Throws()
    {
        var model = new SessionModel();
        for (var i = 0; i < SessionModel.MaxCaptures; i++) model.Add(Cap());
        Assert.True(model.IsFull);
        Assert.Throws<InvalidOperationException>(() => model.Add(Cap()));
    }

    [Fact]
    public void Update_ChangedAnnotationsOnSentCapture_MakesItUnsent()
    {
        var model = new SessionModel();
        var c = Cap(sent: true);
        model.Add(c);
        model.Update(c with { Annotations = c.Annotations.Add(Shape(AnnotationKind.Rectangle)) });
        Assert.False(model.Captures[0].IsSent);
        Assert.Single(model.Captures[0].Annotations);
    }

    [Fact]
    public void Update_ChangedSnapshotNoteOnSentCapture_MakesItUnsent()
    {
        var model = new SessionModel();
        var c = Cap(sent: true);
        model.Add(c);
        model.Update(c with { SnapshotNote = "новая" });
        Assert.False(model.Captures[0].IsSent);
    }

    [Fact]
    public void Update_SameAnnotationsAndNote_KeepsSent()
    {
        var model = new SessionModel();
        var c = Cap(sent: true);
        model.Add(c);
        model.Update(c with { });
        Assert.True(model.Captures[0].IsSent);
    }

    [Fact]
    public void Update_UnknownId_IsIgnored()
    {
        var model = new SessionModel();
        model.Add(Cap());
        model.Update(Cap());
        Assert.Single(model.Captures);
    }

    [Fact]
    public void MarkSent_And_MarkAllUnsent_UpdateUnsentCount()
    {
        var model = new SessionModel();
        var a = Cap(); var b = Cap(); var c = Cap();
        model.Add(a); model.Add(b); model.Add(c);
        model.MarkSent([a.Id, c.Id]);
        Assert.Equal(1, model.UnsentCount);
        model.MarkAllUnsent();
        Assert.Equal(3, model.UnsentCount);
    }

    [Fact]
    public void Remove_ReturnsRemovedCapture()
    {
        var model = new SessionModel();
        var a = Cap(); var b = Cap();
        model.Add(a); model.Add(b);
        Assert.Same(a, model.Remove(a.Id));
        Assert.Equal([b], model.Captures);
        Assert.Null(model.Remove(Guid.NewGuid()));
    }

    [Fact]
    public void Load_ReplacesContentAndRaisesChangedOnce()
    {
        var model = new SessionModel();
        model.Add(Cap());
        var changed = 0;
        model.Changed += (_, _) => changed++;
        var a = Cap(); var b = Cap();
        model.Load([a, b]);
        Assert.Equal([a, b], model.Captures);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var model = new SessionModel();
        model.Add(Cap());
        model.Clear();
        Assert.Empty(model.Captures);
        Assert.Equal(0, model.UnsentCount);
    }
}
