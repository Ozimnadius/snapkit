using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Snapkit.App.Editor;
using Snapkit.App.Imaging;
using Snapkit.App.Notifications;
using Snapkit.App.Session;
using Snapkit.App.Shell;
using Snapkit.Core;
using Snapkit.Core.Model;
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

    /// <summary>
    /// Открыть снимок из ленты: он встаёт на то же место экрана, где его сняли,
    /// поверх свежего кадра монитора. Границы снимка в этом режиме не меняются.
    /// </summary>
    public void Reopen(Guid captureId)
    {
        if (IsOpen) return;
        if (session.Model.Captures.FirstOrDefault(c => c.Id == captureId) is not { } capture) return;

        BitmapSource source;
        MonitorInfo monitor;
        RectI local;
        BitmapSource frame;
        _previousForeground = GetForegroundWindow();
        try
        {
            source = session.GetSource(captureId);
            (monitor, local) = PlaceForReopen(capture.ScreenBounds);
            hideChrome();
            DwmFlush();
            DwmFlush();
            frame = ScreenCapturer.Capture(monitor.Bounds);
        }
        catch (Exception ex)
        {
            Log.Error("открытие снимка", ex);
            restoreChrome();
            Toasts.Error("Не удалось открыть снимок");
            return;
        }

        var window = new CaptureOverlayWindow(monitor, frame) { SelectionLocked = true };
        window.Cancelled += (_, _) => Close();
        _windows.Add(window);
        window.Show();
        window.PresetRegion(local);

        // Буква — та, которую снимок получит, если после правки снова станет неотправленным
        var letters = CaptureLetters.Assign(session.Model.Captures.Select(c => c.Id == captureId ? c with { IsSent = false } : c));
        var editor = new EditorSurface(CreateContext(window, window.Region ?? local, source, capture.Annotations,
            capture.SnapshotNote, letters[captureId], regionEditable: false));
        editor.Done += (_, result) =>
        {
            Close();
            if (result.IsChanged)
                session.UpdateCapture(capture with { Annotations = result.Annotations, SnapshotNote = result.SnapshotNote });
        };
        editor.Cancelled += (_, _) =>
        {
            if (editor.IsChanged && MessageBox.Show(window, "Закрыть без сохранения? Изменения будут потеряны.", "Snapkit",
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

    /// <summary>
    /// Монитор и место снимка: там, где его сняли. Если того монитора больше нет —
    /// основной монитор, снимок по центру рабочей области.
    /// </summary>
    private static (MonitorInfo Monitor, RectI Local) PlaceForReopen(RectI screenBounds)
    {
        var monitors = ScreenCapturer.GetMonitors();
        var centerX = screenBounds.X + screenBounds.Width / 2;
        var centerY = screenBounds.Y + screenBounds.Height / 2;
        if (monitors.FirstOrDefault(m => m.Contains(centerX, centerY)) is { } home)
        {
            return (home, new RectI(screenBounds.X - home.Bounds.X, screenBounds.Y - home.Bounds.Y, screenBounds.Width, screenBounds.Height));
        }
        var primary = monitors.First(m => m.IsPrimary);
        var work = primary.WorkArea;
        var x = Math.Max(work.X, work.X + (work.Width - screenBounds.Width) / 2) - primary.Bounds.X;
        var y = Math.Max(work.Y, work.Y + (work.Height - screenBounds.Height) / 2) - primary.Bounds.Y;
        return (primary, new RectI(x, y, screenBounds.Width, screenBounds.Height));
    }

    private static EditorContext CreateContext(CaptureOverlayWindow window, RectI region, BitmapSource? background,
        System.Collections.Immutable.ImmutableList<Annotation> annotations, string snapshotNote, char letter, bool regionEditable)
    {
        var scale = window.Scale;
        var bounds = window.Monitor.Bounds;
        var work = window.Monitor.WorkArea;
        return new EditorContext(
            new Size(bounds.Width / scale, bounds.Height / scale), scale, region,
            new RectI(work.X - bounds.X, work.Y - bounds.Y, work.Width, work.Height),
            background, annotations, snapshotNote, letter, regionEditable);
    }

    /// <summary>Область выделена — в этом окне открывается редактор, остальные мониторы больше не реагируют.</summary>
    private void StartEditor(CaptureOverlayWindow window)
    {
        foreach (var other in _windows.Where(w => w != window)) other.SelectionLocked = true;

        var editor = new EditorSurface(CreateContext(window, window.Region!.Value, background: null, [], "",
            (char)('A' + session.Model.UnsentCount), regionEditable: true));
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
