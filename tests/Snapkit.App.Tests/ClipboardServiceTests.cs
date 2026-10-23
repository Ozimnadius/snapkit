using System.Diagnostics;
using Snapkit.App.Clipboarding;

namespace Snapkit.App.Tests;

/// <summary>Тесты работают с настоящим буфером обмена и затирают его — при сборке не запускаются.</summary>
[Trait("Category", "Clipboard")]
[Collection("Clipboard")]
public sealed class ClipboardServiceTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4, 5];
    private readonly ClipboardTestHost _host = new();
    private readonly ForeignClipboard _foreign = new();

    public void Dispose()
    {
        _foreign.Dispose();
        _host.Dispose();
    }

    private void Publish(ClipboardPayload payload, ClipboardKind kind = ClipboardKind.Package)
        => _host.Run(() => _host.Service.PublishAsync(payload, kind, excludeFromHistory: false));

    [Fact]
    public void Publish_ThenForeignRead_GetsPngBytes()
    {
        Publish(new ClipboardPayload(Png, null, null));
        var read = _foreign.Read(ForeignClipboard.Format("PNG"));
        Assert.NotNull(read);
        Assert.Equal(Png, read![..Png.Length]); // Windows может округлить размер блока памяти
    }

    [Fact]
    public void RenderedText_UsesCrLf()
    {
        Publish(new ClipboardPayload(null, null, "a\nb"));
        Assert.Equal("a\r\nb", _foreign.ReadText());
    }

    [Fact]
    public void ForeignRead_KeepsPackageCurrent()
    {
        Publish(new ClipboardPayload(Png, null, "текст"));
        _foreign.Read(ForeignClipboard.Format("PNG"));
        _foreign.ReadText();
        Assert.True(_host.Service.IsPackageCurrent());
    }

    [Fact]
    public void SingleCapture_IsNotPackage()
    {
        Publish(new ClipboardPayload(Png, null, null), ClipboardKind.SingleCapture);
        Assert.False(_host.Service.IsPackageCurrent());
    }

    [Fact]
    public async Task ForeignRead_DoesNotConfirmRenderForOtherPid()
    {
        var confirmed = _host.Service.ExpectRender(targetPid: 1, TimeSpan.FromMilliseconds(300));
        Publish(new ClipboardPayload(Png, null, null));
        _foreign.Read(ForeignClipboard.Format("PNG"));
        Assert.False(await confirmed);
    }

    [Fact]
    public async Task ForeignRead_ConfirmsRenderForOwnPid()
    {
        // «Чужое» окно живёт в процессе тестов — его и ждём
        var confirmed = _host.Service.ExpectRender(Environment.ProcessId, TimeSpan.FromSeconds(2));
        Publish(new ClipboardPayload(Png, null, null));
        _foreign.Read(ForeignClipboard.Format("PNG"));
        Assert.True(await confirmed);
    }

    [Fact]
    public void OtherOwnerWrites_PackageNotCurrent()
    {
        Publish(new ClipboardPayload(Png, null, "пакет"));
        _foreign.WriteText("скопировал своё");
        Assert.False(_host.Service.IsPackageCurrent());
    }

    [Fact]
    public void Publish_WhileClipboardHeld_ThrowsBusy()
    {
        _foreign.Hold();
        try
        {
            var watch = Stopwatch.StartNew();
            Assert.Throws<ClipboardBusyException>(() => Publish(new ClipboardPayload(Png, null, null)));
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"Ждали {watch.Elapsed}");
        }
        finally
        {
            _foreign.Release();
        }
    }
}
