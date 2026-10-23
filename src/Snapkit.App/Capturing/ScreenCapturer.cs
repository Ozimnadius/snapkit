using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Snapkit.App.Imaging;
using Snapkit.Core;
using Forms = System.Windows.Forms;

namespace Snapkit.App.Capturing;

/// <summary>Мониторы и снимок экрана в физических пикселях (программа работает в режиме PerMonitorV2).</summary>
public static partial class ScreenCapturer
{
    public static IReadOnlyList<MonitorInfo> GetMonitors()
        => Forms.Screen.AllScreens.Select(screen =>
        {
            var b = screen.Bounds;
            var w = screen.WorkingArea;
            var handle = MonitorFromPoint(new NativePoint(b.X + b.Width / 2, b.Y + b.Height / 2), MONITOR_DEFAULTTONEAREST);
            var scale = GetDpiForMonitor(handle, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 ? dpiX / 96.0 : 1.0;
            return new MonitorInfo(handle, new RectI(b.X, b.Y, b.Width, b.Height),
                new RectI(w.X, w.Y, w.Width, w.Height), scale, screen.Primary);
        }).ToList();

    /// <summary>Монитор, на котором сейчас курсор мыши.</summary>
    public static MonitorInfo MonitorUnderCursor()
    {
        var monitors = GetMonitors();
        GetCursorPos(out var cursor);
        return monitors.FirstOrDefault(m => m.Contains(cursor.X, cursor.Y)) ?? monitors.First(m => m.IsPrimary);
    }

    /// <summary>Снимок прямоугольника экрана: Bgra32, 96 DPI, непрозрачный, замороженный.</summary>
    public static BitmapSource Capture(RectI physicalBounds)
    {
        var screenDc = GetDC(0);
        var memoryDc = CreateCompatibleDC(screenDc);
        var bitmap = CreateCompatibleBitmap(screenDc, physicalBounds.Width, physicalBounds.Height);
        var previous = SelectObject(memoryDc, bitmap);
        try
        {
            if (!BitBlt(memoryDc, 0, 0, physicalBounds.Width, physicalBounds.Height,
                    screenDc, physicalBounds.X, physicalBounds.Y, SRCCOPY | CAPTUREBLT))
            {
                throw new InvalidOperationException("Не удалось снять экран.");
            }
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            return ImageCodecs.Opaque(source);
        }
        finally
        {
            SelectObject(memoryDc, previous);
            DeleteObject(bitmap);
            DeleteDC(memoryDc);
            _ = ReleaseDC(0, screenDc);
        }
    }

    private const uint SRCCOPY = 0x00CC0020;
    private const uint CAPTUREBLT = 0x40000000;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint hWnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint hWnd, nint hDc);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleDC(nint hDc);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleBitmap(nint hDc, int width, int height);

    [LibraryImport("gdi32.dll")]
    private static partial nint SelectObject(nint hDc, nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint hDc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BitBlt(nint destDc, int x, int y, int width, int height, nint srcDc, int srcX, int srcY, uint rop);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromPoint(NativePoint point, uint flags);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out NativePoint point);
}
