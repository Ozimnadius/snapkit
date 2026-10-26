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
        var broken = new Capture(Guid.NewGuid(), 10, 10, new RectI(0, 0, 10, 10), [], "", false, DateTimeOffset.UnixEpoch);
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

    [Fact]
    public void MarkSent_RaisesSentMarked() => StaRunner.Run(() =>
    {
        var service = new SessionService(new SessionStore(_dir));
        var id = service.AddCapture(Image(10, 10), new RectI(0, 0, 10, 10), [], "");
        SessionChange? last = null;
        service.Changed += (_, c) => last = c;
        service.MarkSent([id]);
        Assert.True(service.Model.Captures[0].IsSent);
        Assert.Equal(SessionChangeKind.SentMarked, last?.Kind);
        service.FlushAsync().Wait();
    });
}
