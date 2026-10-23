using Snapkit.Core.Model;
using Snapkit.Core.Session;
using static Snapkit.Core.Tests.TestData;

namespace Snapkit.Core.Tests;

public sealed class SessionStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "snapkit-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new SessionStore(_dir);
        var original = Cap(sent: true,
            Shape(AnnotationKind.Rectangle, note: "рамка", points: [new(1, 2), new(30, 40)]),
            Shape(AnnotationKind.Arrow, points: [new(5, 5), new(50, 6)]),
            Shape(AnnotationKind.Text, text: "надпись", points: [new(7, 8)]),
            Shape(AnnotationKind.Pin, note: "точка", points: [new(9, 9)]))
            with { SnapshotNote = "общая" };
        store.WriteSourceImage(original.Id, Png1x1);
        store.Save([original]);

        var loaded = Assert.Single(new SessionStore(_dir).Load());
        Assert.Equal(original.Id, loaded.Id);
        Assert.Equal(original.PixelWidth, loaded.PixelWidth);
        Assert.Equal(original.PixelHeight, loaded.PixelHeight);
        Assert.Equal(original.ScreenBounds, loaded.ScreenBounds);
        Assert.Equal(original.SnapshotNote, loaded.SnapshotNote);
        Assert.Equal(original.IsSent, loaded.IsSent);
        Assert.Equal(original.CreatedAt, loaded.CreatedAt);
        Assert.Equal(original.Annotations.Count, loaded.Annotations.Count);
        for (var i = 0; i < original.Annotations.Count; i++)
        {
            var (a, b) = (original.Annotations[i], loaded.Annotations[i]);
            Assert.Equal((a.Id, a.Kind, a.Color, a.Thickness, a.Text, a.Note), (b.Id, b.Kind, b.Color, b.Thickness, b.Text, b.Note));
            Assert.True(a.Points.SequenceEqual(b.Points));
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmpty()
        => Assert.Empty(new SessionStore(_dir).Load());

    [Fact]
    public void Load_DropsCapturesWithoutImageFile()
    {
        var store = new SessionStore(_dir);
        var withImage = Cap(); var withoutImage = Cap();
        store.WriteSourceImage(withImage.Id, Png1x1);
        store.Save([withImage, withoutImage]);
        Assert.Equal(withImage.Id, Assert.Single(store.Load()).Id);
    }

    [Fact]
    public void Load_CorruptJson_ReturnsEmptyAndKeepsBadCopy()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "session.json"), "{не json");
        Assert.Empty(new SessionStore(_dir).Load());
        Assert.True(File.Exists(Path.Combine(_dir, "session.bad.json")));
        Assert.False(File.Exists(Path.Combine(_dir, "session.json")));
    }

    [Fact]
    public void Save_LeavesNoTempFile()
    {
        var store = new SessionStore(_dir);
        var c = Cap();
        store.WriteSourceImage(c.Id, Png1x1);
        store.Save([c]);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
        Assert.True(File.Exists(Path.Combine(_dir, "session.json")));
    }

    [Fact]
    public void SourceImagePath_IsIdPngInDirectory()
    {
        var id = Guid.NewGuid();
        Assert.Equal(Path.Combine(_dir, id + ".png"), new SessionStore(_dir).SourceImagePath(id));
    }

    [Fact]
    public void RemoveOrphanImages_KeepsOnlyListedCaptures()
    {
        var store = new SessionStore(_dir);
        var kept = Cap(); var orphan = Cap();
        store.WriteSourceImage(kept.Id, Png1x1);
        store.WriteSourceImage(orphan.Id, Png1x1);
        store.RemoveOrphanImages([kept]);
        Assert.True(File.Exists(store.SourceImagePath(kept.Id)));
        Assert.False(File.Exists(store.SourceImagePath(orphan.Id)));
    }

    [Fact]
    public void Clear_RemovesJsonAndImages()
    {
        var store = new SessionStore(_dir);
        var c = Cap();
        store.WriteSourceImage(c.Id, Png1x1);
        store.Save([c]);
        store.Clear();
        Assert.False(File.Exists(Path.Combine(_dir, "session.json")));
        Assert.Empty(Directory.GetFiles(_dir, "*.png"));
    }
}
