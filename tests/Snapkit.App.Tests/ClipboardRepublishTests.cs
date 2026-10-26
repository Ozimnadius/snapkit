using Snapkit.App.Clipboarding;
using Snapkit.App.Session;

namespace Snapkit.App.Tests;

/// <summary>Когда класть пакет ленты в буфер после изменения ленты.</summary>
public class ClipboardRepublishTests
{
    [Theory]
    [InlineData(SessionChangeKind.Reordered)]
    [InlineData(SessionChangeKind.Removed)]
    [InlineData(SessionChangeKind.Restored)]
    public void ChangeOfStrip_WhenSingleCaptureOrForeignDataInClipboard_KeepsClipboard(SessionChangeKind kind)
        // В буфере «Копировать снимок», черновик или чужое — Ctrl+V должен вставить именно его
        => Assert.False(ClipboardRepublish.After(kind, hasCaptureId: true, hasUnsent: true, stripPackageInClipboard: false));

    [Theory]
    [InlineData(SessionChangeKind.Reordered)]
    [InlineData(SessionChangeKind.Removed)]
    [InlineData(SessionChangeKind.Restored)]
    public void ChangeOfStrip_WhenStripPackageInClipboard_Republishes(SessionChangeKind kind)
        // В буфере наш пакет ленты — обновляем, чтобы буквы и снимки совпадали с лентой
        => Assert.True(ClipboardRepublish.After(kind, hasCaptureId: true, hasUnsent: true, stripPackageInClipboard: true));

    [Theory]
    [InlineData(SessionChangeKind.Added)]
    [InlineData(SessionChangeKind.Updated)]
    public void NewOrEditedCapture_AlwaysPublishes(SessionChangeKind kind)
        => Assert.True(ClipboardRepublish.After(kind, hasCaptureId: true, hasUnsent: true, stripPackageInClipboard: false));

    [Fact]
    public void NothingUnsent_NeverPublishes()
        => Assert.False(ClipboardRepublish.After(SessionChangeKind.Added, hasCaptureId: true, hasUnsent: false, stripPackageInClipboard: true));
}
