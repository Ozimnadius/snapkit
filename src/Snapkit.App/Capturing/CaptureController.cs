using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Snapkit.App.Imaging;
using Snapkit.App.Notifications;
using Snapkit.App.Session;
using Snapkit.App.Shell;
using Snapkit.Core;
using Snapkit.Core.Session;

namespace Snapkit.App.Capturing;

/// <summary>
/// Режим снимка: снимает кадры всех мониторов, показывает окна выделения
/// и отдаёт выделенную область в ленту.
/// </summary>
/// <param name="hideChrome">Спрятать ленту и уведомления перед снимком.</param>
/// <param name="restoreChrome">Вернуть их после.</param>
public sealed partial class CaptureController(SessionService session, Action hideChrome, Action restoreChrome)
{
    private readonly List<CaptureOverlayWindow> _windows = [];
    private nint _previousForeground;

    public bool IsOpen => _windows.Count > 0;

    public event EventHandler? Closed;

    public void StartNewCapture()
    {
        if (IsOpen) return;
        if (session.Model.IsFull)
        {
            Toasts.Show($"В ленте максимум {SessionModel.MaxCaptures} снимков. Отправьте или удалите лишние");
            return;
        }

        _previousForeground = GetForegroundWindow();
        List<(MonitorInfo Monitor, BitmapSource Frame)> frames;
        try
        {
            hideChrome();
            // Дождаться, пока Windows перерисует экран без ленты, иначе она попадёт в кадр
            DwmFlush();
            DwmFlush();
            frames = ScreenCapturer.GetMonitors().Select(m => (m, ScreenCapturer.Capture(m.Bounds))).ToList();
        }
        catch (Exception ex)
        {
            Log.Error("снимок экрана", ex);
            restoreChrome();
            Toasts.Error("Не удалось снять экран");
            return;
        }

        foreach (var (monitor, frame) in frames)
        {
            var window = new CaptureOverlayWindow(monitor, frame);
            window.SelectionStarted += (_, _) =>
            {
                foreach (var other in _windows.Where(w => w != window)) other.ClearSelection();
            };
            window.Confirmed += (_, region) => Complete(window, region);
            window.Cancelled += (_, _) => Close();
            _windows.Add(window);
            window.Show();
        }

        var underCursor = ScreenCapturer.MonitorUnderCursor();
        var active = _windows.FirstOrDefault(w => w.Monitor.Handle == underCursor.Handle) ?? _windows[0];
        active.Activate();
        active.Focus();
    }

    private void Complete(CaptureOverlayWindow window, RectI region)
    {
        var crop = ImageCodecs.Normalize(new CroppedBitmap(window.Frame, new Int32Rect(region.X, region.Y, region.Width, region.Height)));
        var screenBounds = new RectI(window.Monitor.Bounds.X + region.X, window.Monitor.Bounds.Y + region.Y, region.Width, region.Height);
        Close();
        session.AddCapture(crop, screenBounds, [], "");
    }

    private void Close()
    {
        foreach (var window in _windows) window.Close();
        _windows.Clear();
        restoreChrome();
        if (_previousForeground != 0) SetForegroundWindow(_previousForeground);
        Closed?.Invoke(this, EventArgs.Empty);
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmFlush();
}
