using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.App.Clipboarding;
using Snapkit.App.Imaging;
using Snapkit.App.Session;
using Snapkit.Core;
using Snapkit.Core.Model;
using Snapkit.Core.Session;

namespace Snapkit.App.Tests;

[Trait("Category", "Clipboard")]
[Collection("Clipboard")]
public sealed class PackagePublisherTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "snapkit-tests-" + Guid.NewGuid());
    private readonly ClipboardTestHost _host = new();
    private readonly ForeignClipboard _foreign = new();

    public void Dispose()
    {
        _foreign.Dispose();
        _host.Dispose();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static BitmapSource Gray(int w, int h)
    {
        var image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat<byte>(200, w * h * 4).ToArray(), w * 4);
        image.Freeze();
        return image;
    }

    private static Annotation Box(string note) => new(
        Guid.NewGuid(), AnnotationKind.Rectangle, [new PointD(1, 1), new PointD(5, 5)], "#E53935", 2, null, note);

    [Fact]
    public void PublishPackage_PutsPromptTextAndLastImage()
    {
        SessionService? session = null;
        _host.Run(async () =>
        {
            session = new SessionService(new SessionStore(_dir));
            session.AddCapture(Gray(30, 20), new RectI(0, 0, 30, 20), [Box("первый")], "");
            session.AddCapture(Gray(40, 10), new RectI(0, 0, 40, 10), [Box("второй")], "");
            await new PackagePublisher(session, _host.Service).PublishPackageAsync();
        });

        Assert.Equal("Снимок A\r\nA1 — первый\r\n\r\nСнимок B\r\nB1 — второй", _foreign.ReadText());
        Assert.True(_host.Service.IsPackageCurrent());
        BitmapSource? image = null;
        var png = _foreign.Read(ForeignClipboard.Format("PNG"))!;
        StaRunner.Run(() => image = ImageCodecs.DecodePng(png));
        Assert.Equal((40, 10), (image!.PixelWidth, image.PixelHeight)); // картинка последнего снимка пакета
        _host.Run(() => session!.FlushAsync());
    }

    [Fact]
    public void CopyCapture_PutsImageAndItsCommentText()
    {
        // Один снимок вставляется как пакет из одного: картинка и его комментарии, буква A
        SessionService? session = null;
        PackagePublisher? publisher = null;
        Guid second = default;
        _host.Run(async () =>
        {
            session = new SessionService(new SessionStore(_dir));
            session.AddCapture(Gray(30, 20), new RectI(0, 0, 30, 20), [Box("первый")], "");
            second = session.AddCapture(Gray(40, 10), new RectI(0, 0, 40, 10), [Box("второй")], "");
            publisher = new PackagePublisher(session, _host.Service);
            await publisher.CopyCaptureAsync(second);
        });

        Assert.Equal("Снимок A\r\nA1 — второй", _foreign.ReadText());
        Assert.True(_host.Service.IsPackageCurrent());
        PastePlan? plan = null;
        _host.Run(() => { plan = publisher!.CreatePastePlan(); return Task.CompletedTask; });
        Assert.Single(plan!.Images);
        Assert.Equal(new[] { second }, plan.CaptureIds);
        Assert.Equal("Снимок A\nA1 — второй", plan.Text);
        Assert.False(plan.FromStrip);
        _host.Run(() => session!.FlushAsync());
    }

    [Fact]
    public void CopyCapture_WorksForAlreadySentCapture()
    {
        SessionService? session = null;
        PackagePublisher? publisher = null;
        _host.Run(async () =>
        {
            session = new SessionService(new SessionStore(_dir));
            var id = session.AddCapture(Gray(10, 10), new RectI(0, 0, 10, 10), [Box("отправлен")], "");
            session.MarkSent([id]);
            publisher = new PackagePublisher(session, _host.Service);
            await publisher.CopyCaptureAsync(id);
        });

        Assert.True(publisher!.HasPasteContent);
        Assert.Equal("Снимок A\r\nA1 — отправлен", _foreign.ReadText());
        _host.Run(() => session!.FlushAsync());
    }

    [Fact]
    public void CopyDraft_PutsImageAndCommentText()
    {
        // Ctrl+Shift+C в редакторе: снимка ещё нет в ленте
        SessionService? session = null;
        PackagePublisher? publisher = null;
        _host.Run(async () =>
        {
            session = new SessionService(new SessionStore(_dir));
            publisher = new PackagePublisher(session, _host.Service);
            await publisher.CopyDraftAsync(Gray(20, 20), [Box("черновик")], "общая");
        });

        Assert.Equal("Снимок A\r\nA1 — черновик\r\nКомментарий к снимку: общая", _foreign.ReadText());
        Assert.True(publisher!.HasPasteContent);
        PastePlan? plan = null;
        _host.Run(() => { plan = publisher.CreatePastePlan(); return Task.CompletedTask; });
        Assert.Single(plan!.Images);
        Assert.Empty(plan.CaptureIds);
    }

    [Fact]
    public void StripPackage_PlanIsUnsentCaptures()
    {
        SessionService? session = null;
        PackagePublisher? publisher = null;
        Guid a = default, b = default;
        _host.Run(async () =>
        {
            session = new SessionService(new SessionStore(_dir));
            a = session.AddCapture(Gray(10, 10), new RectI(0, 0, 10, 10), [Box("a")], "");
            b = session.AddCapture(Gray(10, 10), new RectI(0, 0, 10, 10), [Box("b")], "");
            publisher = new PackagePublisher(session, _host.Service);
            await publisher.PublishPackageAsync();
        });

        PastePlan? plan = null;
        _host.Run(() => { plan = publisher!.CreatePastePlan(); return Task.CompletedTask; });
        Assert.Equal(2, plan!.Images.Count);
        Assert.Equal(new[] { a, b }, plan.CaptureIds);
        Assert.True(plan.FromStrip);
        _host.Run(() => session!.FlushAsync());
    }

    [Fact]
    public void CopyPackage_WhenAllSent_ResendsWholeStrip()
    {
        SessionService? session = null;
        _host.Run(async () =>
        {
            session = new SessionService(new SessionStore(_dir));
            var a = session.AddCapture(Gray(10, 10), new RectI(0, 0, 10, 10), [Box("a")], "");
            var b = session.AddCapture(Gray(10, 10), new RectI(0, 0, 10, 10), [Box("b")], "");
            session.MarkSent([a, b]);
            await new PackagePublisher(session, _host.Service).CopyPackageCommandAsync();
        });

        Assert.Equal(2, session!.Model.UnsentCount);
        Assert.Contains("Снимок B", _foreign.ReadText());
        _host.Run(() => session.FlushAsync());
    }
}
