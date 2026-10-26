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

    // Лента a, b, c — все неотправленные
    private static (SessionModel Model, Capture A, Capture B, Capture C) Strip()
    {
        var model = new SessionModel();
        var (a, b, c) = (Cap(), Cap(), Cap());
        model.Add(a);
        model.Add(b);
        model.Add(c);
        return (model, a, b, c);
    }

    private static List<(char, Guid)> Package(SessionModel model)
        => PackageSelector.Select(model.Captures).Select(p => (p.Letter, p.Capture.Id)).ToList();

    [Fact]
    public void MoveBefore_ChangesPackageOrder()
    {
        var (model, a, b, c) = Strip();
        model.MoveBefore(c.Id, 0);
        Assert.Equal([('A', c.Id), ('B', a.Id), ('C', b.Id)], Package(model));
    }

    [Theory]
    [InlineData(0, 3, "bca")]   // a в конец
    [InlineData(1, 1, "abc")]   // b перед собой — на месте
    [InlineData(1, 2, "abc")]   // b перед c — тоже на месте
    [InlineData(2, 1, "acb")]
    [InlineData(0, 99, "bca")]  // за пределами — в конец
    public void MoveBefore_Slots(int index, int slot, string expected)
    {
        var (model, a, b, c) = Strip();
        var names = new Dictionary<Guid, char> { [a.Id] = 'a', [b.Id] = 'b', [c.Id] = 'c' };
        model.MoveBefore(model.Captures[index].Id, slot);
        Assert.Equal(expected, new string(model.Captures.Select(x => names[x.Id]).ToArray()));
    }

    [Fact]
    public void Insert_PutsAtIndex()
    {
        var (model, a, b, c) = Strip();
        model.Remove(b.Id);
        model.Insert(1, b);
        Assert.Equal([a.Id, b.Id, c.Id], model.Captures.Select(x => x.Id));
        var x = Cap();
        model.Insert(99, x);
        Assert.Equal(x.Id, model.Captures[^1].Id);
    }

    [Fact]
    public void EvictOldestSent_AllUnsent_ReturnsNull()
    {
        var model = new SessionModel();
        for (var i = 0; i < SessionModel.MaxCaptures; i++) model.Add(Cap());
        Assert.False(model.CanMakeRoom);
        Assert.Null(model.EvictOldestSent());
        Assert.Equal(SessionModel.MaxCaptures, model.Captures.Count);
    }

    [Fact]
    public void CanMakeRoom_FullWithSent_IsTrue()
    {
        var model = new SessionModel();
        for (var i = 0; i < SessionModel.MaxCaptures; i++) model.Add(Cap(sent: i == 5));
        Assert.True(model.CanMakeRoom);
    }

    [Fact]
    public void EvictOldestSent_TakesEarliestCreated()
    {
        var model = new SessionModel();
        var a = Cap(sent: true) with { CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(1) };
        var b = Cap();
        var c = Cap(sent: true) with { CreatedAt = DateTimeOffset.UnixEpoch };
        model.Add(c);
        model.Add(a);
        model.Add(b);
        model.MoveBefore(c.Id, 3); // самый старый — в конце ленты
        Assert.Equal(c.Id, model.EvictOldestSent()?.Id);
        Assert.Equal([a.Id, b.Id], model.Captures.Select(x => x.Id));
    }
}
