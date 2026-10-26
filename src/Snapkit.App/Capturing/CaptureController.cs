using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Snapkit.App.Clipboarding;
using Snapkit.App.Editor;
using Snapkit.App.Imaging;
using Snapkit.App.Interop;
using Snapkit.App.Notifications;
using Snapkit.App.Rendering;
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

    /// <summary>Положить снимок из редактора в буфер — картинку с разметкой и комментарии (Ctrl+Shift+C).</summary>
    public Func<BitmapSource, System.Collections.Immutable.ImmutableList<Annotation>, string, Task>? CopyDraft { get; set; }

    public event EventHandler? Closed;

    public void StartNewCapture()
    {
        if (IsOpen) return;
        if (session.Model.IsFull)
        {
            Toasts.Show($"В ленте максимум {SessionModel.MaxCaptures} снимков. Отправьте или удалите лишние");
            return;
        }

        _previousForeground = Foreground.Current;
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
            window.Cancelled += (_, _) => Cancel();
            _windows.Add(window);
            window.Show();
        }

        var underCursor = ScreenCapturer.MonitorUnderCursor();
        var active = _windows.FirstOrDefault(w => w.Monitor.Handle == underCursor.Handle) ?? _windows[0];
        Foreground.Activate(active);
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
        _previousForeground = Foreground.Current;
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
        window.Cancelled += (_, _) => Cancel();
        _windows.Add(window);
        window.Show();
        window.PresetRegion(local);

        // Буква — та, которую снимок получит, если после правки снова станет неотправленным
        var letters = CaptureLetters.Assign(session.Model.Captures.Select(c => c.Id == captureId ? c with { IsSent = false } : c));
        var editor = new EditorSurface(CreateContext(window, window.Region ?? local, source, capture.Annotations,
            capture.SnapshotNote, letters[captureId], regionEditable: false));
        editor.CopyRequested += (_, _) => CopyFromEditor(editor, source);
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
        Foreground.Activate(window);
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
        editor.CopyRequested += (_, _) => CopyFromEditor(editor, Crop(window.Frame, editor.Region));
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
        Foreground.Activate(window);
    }

    private static BitmapSource Crop(BitmapSource frame, RectI region)
        => ImageCodecs.Normalize(new CroppedBitmap(frame, new Int32Rect(region.X, region.Y, region.Width, region.Height)));

    // Снимок с текущей разметкой и комментариями из редактора — в буфер как пакет из одного снимка
    private async void CopyFromEditor(EditorSurface editor, BitmapSource source)
    {
        if (CopyDraft is null) return;
        try
        {
            await CopyDraft(source, editor.CurrentAnnotations, editor.SnapshotNote);
            Toasts.Show("Снимок скопирован. Нажмите Ctrl+V в чате");
        }
        catch (ClipboardBusyException)
        {
            Toasts.Error("Буфер обмена занят другой программой. Повторите");
        }
    }

    private void CompleteNew(CaptureOverlayWindow window, RectI region, EditorResult result)
    {
        var crop = Crop(window.Frame, region);
        var screenBounds = new RectI(window.Monitor.Bounds.X + region.X, window.Monitor.Bounds.Y + region.Y, region.Width, region.Height);
        Close();
        session.AddCapture(crop, screenBounds, result.Annotations, result.SnapshotNote);
    }

    // Отмена из окна (Esc до выделения, Alt+F4): если открыт редактор — как Esc в нём, с вопросом о правках
    private void Cancel()
    {
        // Отмена от Alt+F4 приходит с задержкой — снимок к этому времени мог уже закрыться
        if (!IsOpen) return;
        if (_editor is { } editor) editor.RequestCancel();
        else Close();
    }

    private void Close()
    {
        foreach (var window in _windows)
        {
            window.AllowClose();
            window.Close();
        }
        _windows.Clear();
        _editor = null;
        restoreChrome();
        Foreground.Restore(_previousForeground);
        Closed?.Invoke(this, EventArgs.Empty);
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmFlush();
}
