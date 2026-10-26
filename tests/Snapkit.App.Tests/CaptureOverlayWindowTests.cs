using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Snapkit.App.Capturing;
using Snapkit.Core;

namespace Snapkit.App.Tests;

/// <summary>
/// Тесты показывают настоящее окно на экране — при сборке не запускаются. Идут в одной очереди
/// с тестами буфера: показанное окно становится активным, а тест вставки следит, чтобы активное окно не менялось.
/// </summary>
[Trait("Category", "Desktop")]
[Collection("Clipboard")]
public partial class CaptureOverlayWindowTests
{
    private static readonly RectI Bounds = new(60, 60, 400, 300);

    private static CaptureOverlayWindow CreateWindow()
    {
        var frame = BitmapSource.Create(Bounds.Width, Bounds.Height, 96, 96, PixelFormats.Bgra32, null,
            new byte[Bounds.Width * Bounds.Height * 4], Bounds.Width * 4);
        frame.Freeze();
        return new CaptureOverlayWindow(new MonitorInfo(0, Bounds, Bounds, 1.0, IsPrimary: true), frame);
    }

    [Fact]
    public void DpiChange_KeepsWindowExactlyOnMonitor() => StaRunner.Run(() =>
    {
        var window = CreateWindow();
        window.Show();
        var hwnd = new WindowInteropHelper(window).Handle;

        // Windows при переходе на монитор с масштабом 150 % предлагает окно в 1,5 раза больше
        var suggested = new RECT { Left = 0, Top = 0, Right = 600, Bottom = 450 };
        SendMessageW(hwnd, WM_DPICHANGED, (144 << 16) | 144, ref suggested);

        GetWindowRect(hwnd, out var actual);
        window.AllowClose();
        window.Close();
        Assert.Equal((Bounds.X, Bounds.Y, Bounds.Right, Bounds.Bottom), (actual.Left, actual.Top, actual.Right, actual.Bottom));
    });

    [Fact]
    public void CloseFromOutside_CancelsCaptureInsteadOfClosing() => StaRunner.Run(() =>
    {
        // Alt+F4 закрыл бы окно в обход программы, и она решила бы, что снимок всё ещё открыт
        var window = CreateWindow();
        var cancelled = false;
        window.Cancelled += (_, _) => cancelled = true;
        window.Show();

        window.Close();
        DoEvents();
        var stillOpen = window.IsVisible;
        window.AllowClose();
        window.Close();

        Assert.True(cancelled);
        Assert.True(stillOpen);
    });

    [Fact]
    public void CloseFromOutside_CancelHandlerMayCloseWindow() => StaRunner.Run(() =>
    {
        // Так отменяет снимок CaptureController: в ответ на отмену сразу закрывает все окна.
        // Закрывать окно, пока WPF ещё внутри его Closing, нельзя — было исключение
        var window = CreateWindow();
        window.Cancelled += (_, _) =>
        {
            window.AllowClose();
            window.Close();
        };
        window.Show();

        window.Close();
        DoEvents();

        Assert.False(window.IsVisible);
    });

    // Выполнить отложенные действия окна (Dispatcher.BeginInvoke)
    private static void DoEvents() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);

    private const uint WM_DPICHANGED = 0x02E0;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [LibraryImport("user32.dll")]
    private static partial nint SendMessageW(nint hWnd, uint msg, nint wParam, ref RECT lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hWnd, out RECT rect);
}
