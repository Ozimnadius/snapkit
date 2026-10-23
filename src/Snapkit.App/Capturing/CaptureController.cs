using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Snapkit.App.Editor;
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
    private EditorSurface? _editor;

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
            window.RegionChanged += (_, _) =>
            {
                if (_editor is null && window.Region is not null) StartEditor(window);
                else if (_editor is not null && window.Editor == _editor && window.Region is { } region) _editor.SetRegion(region);
            };
            window.Confirmed += (_, _) => _editor?.Finish();
            window.Cancelled += (_, _) => Close();
            _windows.Add(window);
            window.Show();
        }

        var underCursor = ScreenCapturer.MonitorUnderCursor();
        var active = _windows.FirstOrDefault(w => w.Monitor.Handle == underCursor.Handle) ?? _windows[0];
        active.Activate();
        active.Focus();
    }

    /// <summary>Область выделена — в этом окне открывается редактор, остальные мониторы больше не реагируют.</summary>
    private void StartEditor(CaptureOverlayWindow window)
    {
        foreach (var other in _windows.Where(w => w != window)) other.SelectionLocked = true;

        var scale = window.Scale;
        var bounds = window.Monitor.Bounds;
        var work = window.Monitor.WorkArea;
        var context = new EditorContext(
            new Size(bounds.Width / scale, bounds.Height / scale), scale, window.Region!.Value,
            new RectI(work.X - bounds.X, work.Y - bounds.Y, work.Width, work.Height),
            Background: null, Annotations: [], SnapshotNote: "",
            Letter: (char)('A' + session.Model.UnsentCount), RegionEditable: true);

        var editor = new EditorSurface(context);
        editor.Done += (_, result) => CompleteNew(window, editor.Region, result);
        editor.Cancelled += (_, _) =>
        {
            if (editor.IsChanged && MessageBox.Show(window, "Отменить снимок? Разметка будет потеряна.", "Snapkit",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }
            Close();
        };
        _editor = editor;
        window.ShowEditor(editor);
        window.Activate();
    }

    private void CompleteNew(CaptureOverlayWindow window, RectI region, EditorResult result)
    {
        var crop = ImageCodecs.Normalize(new CroppedBitmap(window.Frame, new Int32Rect(region.X, region.Y, region.Width, region.Height)));
        var screenBounds = new RectI(window.Monitor.Bounds.X + region.X, window.Monitor.Bounds.Y + region.Y, region.Width, region.Height);
        Close();
        session.AddCapture(crop, screenBounds, result.Annotations, result.SnapshotNote);
    }

    private void Close()
    {
        foreach (var window in _windows) window.Close();
        _windows.Clear();
        _editor = null;
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
