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
