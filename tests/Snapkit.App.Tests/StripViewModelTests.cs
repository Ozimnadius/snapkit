using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snapkit.App.Strip;
using Snapkit.Core;
using Snapkit.Core.Model;

namespace Snapkit.App.Tests;

public class StripViewModelTests
{
    private static Capture Cap(string snapshotNote = "") => new(
        Guid.NewGuid(), 10, 10, new RectI(0, 0, 10, 10), [], snapshotNote, DateTimeOffset.UnixEpoch);

    private static BitmapSource Thumb()
    {
        var image = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
        image.Freeze();
        return image;
    }

    [Fact]
    public void Refresh_AssignsLettersCountsAndStack() => StaRunner.Run(() =>
    {
        var vm = new StripViewModel();
        vm.Refresh([Cap(), Cap(snapshotNote: "общая"), Cap()], (_, _) => Thumb());

        Assert.Equal(3, vm.Cards.Count);
        // Число рядом с «Snapkit» — сколько снимков на ленте; буква у каждой карточки — её место
        Assert.Equal(3, vm.CaptureCount);
        Assert.Equal(["A", "B", "C"], vm.Cards.Select(c => c.Letter));
        Assert.Equal(1, vm.Cards[1].NoteCount);
        // Стопка: первая карточка без сдвига, каждая следующая наезжает на предыдущую
        Assert.Equal(0, vm.Cards[0].TopMargin.Top);
        Assert.Equal(-(StripViewModel.CardHeight - StripViewModel.StripeHeight), vm.Cards[1].TopMargin.Top);
        Assert.False(vm.IsEmpty);
    });

    [Fact]
    public void Refresh_ReusesThumbnailForUnchangedCapture() => StaRunner.Run(() =>
    {
        var vm = new StripViewModel();
        var a = Cap(); var b = Cap();
        var rendered = 0;
        BitmapSource Render(Capture c, char l) { rendered++; return Thumb(); }
        vm.Refresh([a], Render);
        vm.Refresh([a, b], Render);
        Assert.Equal(2, rendered); // a — один раз, b — один раз
        vm.Refresh([b], Render);    // у b сменилась буква (B → A) — перерисовка
        Assert.Equal(3, rendered);
    });

    [Fact]
    public void ForgetThumbnails_RedrawsOnNextRefresh() => StaRunner.Run(() =>
    {
        // Сменили цвет программы — кружки заметок на миниатюрах надо перерисовать
        var vm = new StripViewModel();
        var a = Cap();
        var rendered = 0;
        BitmapSource Render(Capture c, char l) { rendered++; return Thumb(); }
        vm.Refresh([a], Render);
        vm.ForgetThumbnails();
        vm.Refresh([a], Render);
        Assert.Equal(2, rendered);
    });

    [Fact]
    public void SoftWarning_FromTenCaptures() => StaRunner.Run(() =>
    {
        var vm = new StripViewModel();
        vm.Refresh(Enumerable.Range(0, 9).Select(_ => Cap()).ToList(), (_, _) => Thumb());
        Assert.False(vm.ShowSoftWarning);
        vm.Refresh(Enumerable.Range(0, 10).Select(_ => Cap()).ToList(), (_, _) => Thumb());
        Assert.True(vm.ShowSoftWarning);
    });

    [Fact]
    public void IsEmpty_WhenNoCaptures() => StaRunner.Run(() =>
    {
        var vm = new StripViewModel();
        vm.Refresh([], (_, _) => Thumb());
        Assert.True(vm.IsEmpty);
        Assert.Equal(0, vm.CaptureCount);
    });
}
