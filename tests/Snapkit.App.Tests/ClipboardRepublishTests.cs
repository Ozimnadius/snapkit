using Snapkit.App.Clipboarding;
using Snapkit.App.Session;

namespace Snapkit.App.Tests;

/// <summary>Что класть в буфер после изменения ленты.</summary>
public class ClipboardRepublishTests
{
    [Theory]
    [InlineData(SessionChangeKind.Added)]
    [InlineData(SessionChangeKind.Updated)]
    public void NewOrEditedCapture_LeavesClipboardToDoneButton(SessionChangeKind kind)
        // Снимок копирует сама кнопка «Готово» (в том числе без правок) — изменение ленты буфер не трогает
        => Assert.Equal(ClipboardAction.None,
            ClipboardRepublish.After(kind, hasCaptureId: true, hasCaptures: true, stripPackageInClipboard: true));

    [Theory]
    [InlineData(SessionChangeKind.Reordered)]
    [InlineData(SessionChangeKind.Removed)]
    [InlineData(SessionChangeKind.Restored)]
    public void ChangeOfStrip_WhenSingleCaptureOrForeignDataInClipboard_KeepsClipboard(SessionChangeKind kind)
        // В буфере «Копировать снимок», черновик или чужое — Ctrl+V должен вставить именно его
        => Assert.Equal(ClipboardAction.None,
            ClipboardRepublish.After(kind, hasCaptureId: true, hasCaptures: true, stripPackageInClipboard: false));

    [Theory]
    [InlineData(SessionChangeKind.Reordered)]
    [InlineData(SessionChangeKind.Removed)]
    [InlineData(SessionChangeKind.Restored)]
    public void ChangeOfStrip_WhenStripPackageInClipboard_RepublishesPackage(SessionChangeKind kind)
        // В буфере наш пакет ленты — обновляем, чтобы буквы и снимки совпадали с лентой
        => Assert.Equal(ClipboardAction.Package,
            ClipboardRepublish.After(kind, hasCaptureId: true, hasCaptures: true, stripPackageInClipboard: true));

    [Fact]
    public void ChangeWithoutCapture_KeepsClipboard()
        => Assert.Equal(ClipboardAction.None,
            ClipboardRepublish.After(SessionChangeKind.Updated, hasCaptureId: false, hasCaptures: true, stripPackageInClipboard: true));

    [Fact]
    public void EmptyStrip_NoPackage()
        => Assert.Equal(ClipboardAction.None,
            ClipboardRepublish.After(SessionChangeKind.Reordered, hasCaptureId: true, hasCaptures: false, stripPackageInClipboard: true));

}
