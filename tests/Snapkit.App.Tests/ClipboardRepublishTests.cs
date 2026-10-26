using Snapkit.App.Clipboarding;
using Snapkit.App.Session;

namespace Snapkit.App.Tests;

/// <summary>Что класть в буфер после изменения ленты.</summary>
public class ClipboardRepublishTests
{
    [Theory]
    [InlineData(SessionChangeKind.Added)]
    [InlineData(SessionChangeKind.Updated)]
    public void Done_CopiesOnlyThatCapture(SessionChangeKind kind)
        // «Готово» в редакторе — как «Копировать»: следующий Ctrl+V вставит только этот снимок
        => Assert.Equal(ClipboardAction.Capture,
            ClipboardRepublish.After(kind, hasCaptureId: true, hasUnsent: true, stripPackageInClipboard: true));

    [Theory]
    [InlineData(SessionChangeKind.Reordered)]
    [InlineData(SessionChangeKind.Removed)]
    [InlineData(SessionChangeKind.Restored)]
    public void ChangeOfStrip_WhenSingleCaptureOrForeignDataInClipboard_KeepsClipboard(SessionChangeKind kind)
        // В буфере «Копировать снимок», черновик или чужое — Ctrl+V должен вставить именно его
        => Assert.Equal(ClipboardAction.None,
            ClipboardRepublish.After(kind, hasCaptureId: true, hasUnsent: true, stripPackageInClipboard: false));

    [Theory]
    [InlineData(SessionChangeKind.Reordered)]
    [InlineData(SessionChangeKind.Removed)]
    [InlineData(SessionChangeKind.Restored)]
    public void ChangeOfStrip_WhenStripPackageInClipboard_RepublishesPackage(SessionChangeKind kind)
        // В буфере наш пакет ленты — обновляем, чтобы буквы и снимки совпадали с лентой
        => Assert.Equal(ClipboardAction.Package,
            ClipboardRepublish.After(kind, hasCaptureId: true, hasUnsent: true, stripPackageInClipboard: true));

    [Fact]
    public void ChangeWithoutCapture_KeepsClipboard()
        => Assert.Equal(ClipboardAction.None,
            ClipboardRepublish.After(SessionChangeKind.Updated, hasCaptureId: false, hasUnsent: true, stripPackageInClipboard: true));

    [Fact]
    public void NothingUnsent_NoPackage()
        => Assert.Equal(ClipboardAction.None,
            ClipboardRepublish.After(SessionChangeKind.Reordered, hasCaptureId: true, hasUnsent: false, stripPackageInClipboard: true));

    [Fact]
    public void DoneOnSentCapture_StillCopiesIt()
        // Открыли отправленный снимок с ленты и нажали «Готово» без правок — он всё равно копируется
        => Assert.Equal(ClipboardAction.Capture,
            ClipboardRepublish.After(SessionChangeKind.Updated, hasCaptureId: true, hasUnsent: false, stripPackageInClipboard: false));
}
