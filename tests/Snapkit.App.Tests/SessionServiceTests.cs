using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.App.Session;
using Snapkit.Core;
using Snapkit.Core.Model;
using Snapkit.Core.Session;

namespace Snapkit.App.Tests;

public sealed class SessionServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "snapkit-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static BitmapSource Image(int w, int h)
    {
        var image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, new byte[w * h * 4], w * 4);
        image.Freeze();
        return image;
    }

    private static Annotation Box(string note) => new(
        Guid.NewGuid(), AnnotationKind.Rectangle, [new PointD(1, 1), new PointD(5, 5)], "#E53935", 4, null, note);

    [Fact]
    public void Restore_PutsBackAtIndex() => StaRunner.Run(() =>
    {
        var service = new SessionService(new SessionStore(_dir));
        var a = service.AddCapture(Image(10, 10), new RectI(0, 0, 10, 10), [], "");
        var source = Image(20, 10);
        var b = service.AddCapture(source, new RectI(0, 0, 20, 10), [], "");
        var c = service.AddCapture(Image(10, 10), new RectI(0, 0, 10, 10), [], "");

        var removed = service.Remove(b);
        Assert.NotNull(removed);
        Assert.True(service.Restore(removed));
        service.FlushAsync().Wait();

        Assert.Equal([a, b, c], service.Model.Captures.Select(x => x.Id));
        Assert.Same(source, service.GetSource(b));
    });

    [Fact]
    public void Restore_WhenFull_EvictsOldest() => StaRunner.Run(() =>
    {
        // Удалили снимок, сняли новый — лента снова полная; «Вернуть» всё равно возвращает, уходит самый старый
        var service = new SessionService(new SessionStore(_dir));
        var ids = Enumerable.Range(0, SessionModel.MaxCaptures)
            .Select(_ => service.AddCapture(Image(4, 4), new RectI(0, 0, 4, 4), [], "")).ToList();
        var removed = service.Remove(ids[3])!;
        service.AddCapture(Image(4, 4), new RectI(0, 0, 4, 4), [], "");

        Assert.True(service.Restore(removed));
        Assert.Equal(SessionModel.MaxCaptures, service.Model.Captures.Count);
        Assert.Contains(service.Model.Captures, x => x.Id == ids[3]);
        Assert.DoesNotContain(service.Model.Captures, x => x.Id == ids[0]);
        service.FlushAsync().Wait();
    });

    [Fact]
    public void AddCapture_WhenFull_EvictsOldest() => StaRunner.Run(() =>
    {
        // Полная лента: новый снимок вытесняет самый старый (по времени снимка, а не по месту), об этом сообщается
        var service = new SessionService(new SessionStore(_dir));
        var ids = Enumerable.Range(0, SessionModel.MaxCaptures)
            .Select(_ => service.AddCapture(Image(4, 4), new RectI(0, 0, 4, 4), [], "")).ToList();
        service.MoveBefore(ids[0], SessionModel.MaxCaptures); // самый старый — в конце ленты
        var changes = new List<SessionChange>();
        service.Changed += (_, change) => changes.Add(change);
        Guid? evicted = null;
        service.Evicted += (_, id) => evicted = id;

        var added = service.AddCapture(Image(4, 4), new RectI(0, 0, 4, 4), [], "");
        service.FlushAsync().Wait();

        Assert.Equal(SessionModel.MaxCaptures, service.Model.Captures.Count);
        Assert.DoesNotContain(service.Model.Captures, x => x.Id == ids[0]);
        Assert.Equal(ids[0], evicted);
        Assert.Equal([new SessionChange(SessionChangeKind.Removed, ids[0]), new SessionChange(SessionChangeKind.Added, added)], changes);
    });

    [Fact]
    public void Restore_RaisesRestored() => StaRunner.Run(() =>
    {
        var service = new SessionService(new SessionStore(_dir));
        var a = service.AddCapture(Image(4, 4), new RectI(0, 0, 4, 4), [], "");
        var removed = service.Remove(a)!;
        var changes = new List<SessionChange>();
        service.Changed += (_, change) => changes.Add(change);

        service.Restore(removed);
        service.FlushAsync().Wait();

        Assert.Equal(new SessionChange(SessionChangeKind.Restored, a), Assert.Single(changes));
    });

    [Fact]
    public void MoveBefore_RaisesReordered() => StaRunner.Run(() =>
    {
        var service = new SessionService(new SessionStore(_dir));
        var a = service.AddCapture(Image(4, 4), new RectI(0, 0, 4, 4), [], "");
        var b = service.AddCapture(Image(4, 4), new RectI(0, 0, 4, 4), [], "");
        var changes = new List<SessionChange>();
        service.Changed += (_, change) => changes.Add(change);

        service.MoveBefore(b, 0);
        service.FlushAsync().Wait();

        Assert.Equal([b, a], service.Model.Captures.Select(x => x.Id));
        Assert.Equal(new SessionChange(SessionChangeKind.Reordered, b), Assert.Single(changes));
        var restored = new SessionService(new SessionStore(_dir));
        restored.RestoreFromDisk();
        Assert.Equal([b, a], restored.Model.Captures.Select(x => x.Id));
        restored.FlushAsync().Wait();
    });

    [Fact]
    public void AddCapture_WritesImageAndSession() => StaRunner.Run(() =>
    {
        var service = new SessionService(new SessionStore(_dir));
        var changes = new List<SessionChange>();
        service.Changed += (_, c) => changes.Add(c);

        var id = service.AddCapture(Image(30, 20), new RectI(100, 200, 30, 20), [Box("заметка")], "общая");
        service.FlushAsync().Wait();

        var capture = Assert.Single(service.Model.Captures);
        Assert.Equal(id, capture.Id);
        Assert.Equal((30, 20, new RectI(100, 200, 30, 20), "общая"), (capture.PixelWidth, capture.PixelHeight, capture.ScreenBounds, capture.SnapshotNote));
        Assert.Equal(new SessionChange(SessionChangeKind.Added, id), Assert.Single(changes));
        Assert.True(File.Exists(Path.Combine(_dir, id + ".png")));
        Assert.True(File.Exists(Path.Combine(_dir, "session.json")));
    });

    [Fact]
    public void UpdateCaptureImage_ReplacesSourceOnDiskAndInMemory() => StaRunner.Run(() =>
    {
        // Обрезали снимок из ленты: меньше картинка, сдвинулось место на экране
        var service = new SessionService(new SessionStore(_dir));
        var id = service.AddCapture(Image(100, 80), new RectI(10, 20, 100, 80), [], "");
        var cropped = service.Model.Captures.Single() with { PixelWidth = 40, PixelHeight = 30, ScreenBounds = new RectI(30, 40, 40, 30) };

        service.UpdateCaptureImage(cropped, Image(40, 30));
        Assert.Equal((40, 30), (service.GetSource(id).PixelWidth, service.GetSource(id).PixelHeight));
        service.FlushAsync().Wait();

        var restored = new SessionService(new SessionStore(_dir));
        Assert.Equal(1, restored.RestoreFromDisk());
        var again = restored.Model.Captures.Single();
        Assert.Equal((40, 30, new RectI(30, 40, 40, 30)), (again.PixelWidth, again.PixelHeight, again.ScreenBounds));
        Assert.Equal((40, 30), (restored.GetSource(id).PixelWidth, restored.GetSource(id).PixelHeight));
        restored.FlushAsync().Wait();
    });

    [Fact]
    public void UpdateCaptureImage_RaisesUpdated() => StaRunner.Run(() =>
    {
        var service = new SessionService(new SessionStore(_dir));
        var id = service.AddCapture(Image(100, 80), new RectI(10, 20, 100, 80), [], "");
        var changes = new List<SessionChange>();
        service.Changed += (_, c) => changes.Add(c);

        service.UpdateCaptureImage(service.Model.Captures.Single() with { PixelWidth = 40, PixelHeight = 30 }, Image(40, 30));
        service.FlushAsync().Wait();

        Assert.Equal(new SessionChange(SessionChangeKind.Updated, id), Assert.Single(changes));
    });

    [Fact]
    public void RestoreFromDisk_LoadsSavedCaptures() => StaRunner.Run(() =>
    {
        var first = new SessionService(new SessionStore(_dir));
        var id = first.AddCapture(Image(30, 20), new RectI(0, 0, 30, 20), [Box("a")], "");
        first.FlushAsync().Wait();

        var second = new SessionService(new SessionStore(_dir));
        Assert.Equal(1, second.RestoreFromDisk());
        Assert.Equal(id, Assert.Single(second.Model.Captures).Id);
        var source = second.GetSource(id);
        Assert.Equal((30, 20), (source.PixelWidth, source.PixelHeight));
    });

    [Fact]
    public void RestoreFromDisk_DropsCaptureWithCorruptImage() => StaRunner.Run(() =>
    {
        var store = new SessionStore(_dir);
        var good = new SessionService(store);
        var keptId = good.AddCapture(Image(10, 10), new RectI(0, 0, 10, 10), [], "");
        good.FlushAsync().Wait();
        var broken = new Capture(Guid.NewGuid(), 10, 10, new RectI(0, 0, 10, 10), [], "", DateTimeOffset.UnixEpoch);
        store.WriteSourceImage(broken.Id, [1, 2, 3]); // не PNG
        store.Save([.. store.Load(), broken]);

        var restored = new SessionService(new SessionStore(_dir));
        Assert.Equal(1, restored.RestoreFromDisk());
        Assert.Equal(keptId, Assert.Single(restored.Model.Captures).Id);
        restored.FlushAsync().Wait();
    });

    [Theory]
    [InlineData("не цвет", 2)]   // цвет не читается
    [InlineData("#E53935", 1)]   // у рамки одна точка вместо двух
    public void RestoreFromDisk_DropsCaptureWithBrokenAnnotation(string color, int points) => StaRunner.Run(() =>
    {
        var service = new SessionService(new SessionStore(_dir));
        var bad = new Annotation(Guid.NewGuid(), AnnotationKind.Rectangle,
            [.. Enumerable.Range(0, points).Select(i => new PointD(i, i))], color, 4, null, "x");
        service.AddCapture(Image(10, 10), new RectI(0, 0, 10, 10), [bad], "");
        service.FlushAsync().Wait();

        var restored = new SessionService(new SessionStore(_dir));
        Assert.Equal(0, restored.RestoreFromDisk());
        Assert.Empty(restored.Model.Captures);
        restored.FlushAsync().Wait();
    });

    [Fact]
    public void RestoreFromDisk_DropsCaptureWithNullAnnotations() => StaRunner.Run(() =>
    {
        var service = new SessionService(new SessionStore(_dir));
        var id = service.AddCapture(Image(10, 10), new RectI(0, 0, 10, 10), [], "");
        service.FlushAsync().Wait();
        var json = Path.Combine(_dir, "session.json");
        File.WriteAllText(json, File.ReadAllText(json).Replace("\"annotations\": []", "\"annotations\": null"));

        var restored = new SessionService(new SessionStore(_dir));
        Assert.Equal(0, restored.RestoreFromDisk());
        restored.FlushAsync().Wait();
    });

    [Fact]
    public void Remove_RaisesRemovedAndForgetsCapture() => StaRunner.Run(() =>
    {
        var service = new SessionService(new SessionStore(_dir));
        var id = service.AddCapture(Image(10, 10), new RectI(0, 0, 10, 10), [], "");
        SessionChange? last = null;
        service.Changed += (_, c) => last = c;
        service.Remove(id);
        Assert.Empty(service.Model.Captures);
        Assert.Equal(new SessionChange(SessionChangeKind.Removed, id), last);
        service.FlushAsync().Wait();
    });

    [Fact]
    public void ClearAll_RemovesFiles() => StaRunner.Run(() =>
    {
        var service = new SessionService(new SessionStore(_dir));
        service.AddCapture(Image(10, 10), new RectI(0, 0, 10, 10), [], "");
        service.FlushAsync().Wait();
        service.ClearAll();
        service.FlushAsync().Wait();
        Assert.Empty(service.Model.Captures);
        Assert.False(File.Exists(Path.Combine(_dir, "session.json")));
        Assert.Empty(Directory.GetFiles(_dir, "*.png"));
    });
}
