using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Snapkit.App.Editor;
using Snapkit.App.Rendering;
using Snapkit.Core;
using Snapkit.Core.Editing;

namespace Snapkit.App.Capturing;

/// <summary>
/// Окно на весь монитор с застывшим кадром: здесь выделяют область снимка.
/// Координаты выделения — физические пиксели внутри монитора.
/// </summary>
public partial class CaptureOverlayWindow : Window
{
    private const double HandleSize = 8;
    private const double HandleHitRadius = 8;

    private readonly Rectangle[] _handles = new Rectangle[8];
    private readonly RegionSelection _selection;
    private bool _locked;

    public CaptureOverlayWindow(MonitorInfo monitor, BitmapSource frame)
    {
        InitializeComponent();
        Monitor = monitor;
        Frame = frame;
        FrameImage.Source = frame;
        _selection = new RegionSelection(new RectI(0, 0, monitor.Bounds.Width, monitor.Bounds.Height));

        for (var i = 0; i < _handles.Length; i++)
        {
            _handles[i] = new Rectangle
            {
                Width = HandleSize, Height = HandleSize, Fill = Brushes.White,
                Stroke = Palette.Brush(Palette.Accent), StrokeThickness = 1.5, Visibility = Visibility.Collapsed,
            };
            SelectionLayer.Children.Add(_handles[i]);
        }

        SourceInitialized += (_, _) =>
        {
            // Свой обработчик стоит раньше WPF: при смене масштаба Windows предлагает новый размер окна,
            // а нам нужно, чтобы окно оставалось ровно на границах монитора
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(KeepBoundsOnDpiChange);
            PlaceOnMonitor();
        };
        // Закрыть окно в обход программы (Alt+F4) — значит отменить снимок. Сообщаем об отмене
        // после выхода из Closing: пока окно закрывается, WPF не даёт закрыть его из обработчика
        Closing += (_, e) =>
        {
            if (_closeAllowed) return;
            e.Cancel = true;
            Dispatcher.BeginInvoke(() => Cancelled?.Invoke(this, EventArgs.Empty));
        };
        Loaded += (_, _) => Redraw();

        Root.MouseLeftButtonDown += OnMouseDown;
        Root.MouseMove += OnMouseMove;
        Root.MouseLeftButtonUp += OnMouseUp;
        KeyDown += OnKeyDown;
    }

    public MonitorInfo Monitor { get; }
    public BitmapSource Frame { get; }
    public RectI? Region => _selection.Region;

    public event EventHandler? SelectionStarted;
    public event EventHandler<RectI>? Confirmed;
    public event EventHandler? Cancelled;

    /// <summary>Масштаб монитора: физические пиксели на единицу WPF.</summary>
    public double Scale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    /// <summary>Снять выделение — пользователь начал выделять на другом мониторе.</summary>
    public void ClearSelection()
    {
        _selection.Begin(new PointD(0, 0));
        _selection.End();
        Redraw();
    }

    private bool _closeAllowed;

    /// <summary>Разрешить закрыть окно — так закрывает его сама программа.</summary>
    public void AllowClose() => _closeAllowed = true;

    /// <summary>Поставить рамку на заданное место (повторное открытие снимка).</summary>
    public void PresetRegion(RectI region)
    {
        _selection.Select(region);
        Redraw();
    }

    /// <summary>Выделить весь монитор.</summary>
    public void SelectAll()
    {
        _selection.SelectAll();
        Redraw();
    }

    /// <summary>Запретить менять рамку (например, пока идёт рисование в редакторе).</summary>
    public bool SelectionLocked
    {
        get => _locked;
        set { _locked = value; Redraw(); }
    }

    /// <summary>Рамку отпустили после выделения или изменения границ.</summary>
    public event EventHandler? RegionChanged;

    /// <summary>Редактор, открытый в этом окне (после выделения области).</summary>
    public EditorSurface? Editor { get; private set; }

    public void ShowEditor(EditorSurface editor)
    {
        Editor = editor;
        Hint.Visibility = Visibility.Collapsed;
        EditorLayer.Children.Add(editor);
        Redraw();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_locked) return;
        var p = Physical(e.GetPosition(Root));
        var handle = _selection.HitHandle(p, HandleHitRadius * Scale);
        // В редакторе рамку можно только растягивать за маркеры: внутри рисуют, снаружи ничего не происходит
        if (Editor is not null && handle is SelectionHandle.None or SelectionHandle.Move) return;
        if (e.ClickCount == 2 && handle == SelectionHandle.Move && _selection.Region is { } region)
        {
            Confirmed?.Invoke(this, region);
            return;
        }
        _selection.Begin(p, handle);
        if (handle == SelectionHandle.None) SelectionStarted?.Invoke(this, EventArgs.Empty);
        Root.CaptureMouse();
        Redraw();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_locked) return;
        var p = Physical(e.GetPosition(Root));
        if (_selection.IsDragging)
        {
            _selection.Drag(p);
            Redraw();
            if (Editor is not null && _selection.Region is { } live) Editor.SetRegion(live);
            return;
        }
        Root.Cursor = _selection.HitHandle(p, HandleHitRadius * Scale) switch
        {
            SelectionHandle.Move => Cursors.SizeAll,
            SelectionHandle.North or SelectionHandle.South => Cursors.SizeNS,
            SelectionHandle.East or SelectionHandle.West => Cursors.SizeWE,
            SelectionHandle.NorthWest or SelectionHandle.SouthEast => Cursors.SizeNWSE,
            SelectionHandle.NorthEast or SelectionHandle.SouthWest => Cursors.SizeNESW,
            _ => Cursors.Cross,
        };
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_selection.IsDragging) return;
        _selection.End();
        Root.ReleaseMouseCapture();
        Redraw();
        RegionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (Editor is not null)
        {
            e.Handled = Editor.HandleKey(e);
            return;
        }
        if (e.Key == Key.Escape)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _selection.Region is { } region && !_locked)
        {
            Confirmed?.Invoke(this, region);
            e.Handled = true;
        }
    }

    private PointD Physical(Point dip) => new(dip.X * Scale, dip.Y * Scale);

    private void Redraw()
    {
        var scale = Scale;
        var full = new Rect(0, 0, Monitor.Bounds.Width / scale, Monitor.Bounds.Height / scale);
        var region = _selection.Region;
        Hint.Visibility = region is null && !_selection.IsDragging && Editor is null ? Visibility.Visible : Visibility.Collapsed;

        if (region is not { } r)
        {
            Shade.Data = new RectangleGeometry(full);
            SelectionBorder.Visibility = Visibility.Collapsed;
            SizeLabel.Visibility = Visibility.Collapsed;
            foreach (var h in _handles) h.Visibility = Visibility.Collapsed;
            return;
        }

        var rect = new Rect(r.X / scale, r.Y / scale, r.Width / scale, r.Height / scale);
        Shade.Data = new GeometryGroup
        {
            FillRule = FillRule.EvenOdd,
            Children = { new RectangleGeometry(full), new RectangleGeometry(rect) },
        };

        SelectionBorder.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionBorder, rect.X - 1);
        Canvas.SetTop(SelectionBorder, rect.Y - 1);
        SelectionBorder.Width = rect.Width + 2;
        SelectionBorder.Height = rect.Height + 2;

        Point[] points =
        [
            rect.TopLeft, new(rect.X + rect.Width / 2, rect.Y), rect.TopRight, new(rect.Right, rect.Y + rect.Height / 2),
            rect.BottomRight, new(rect.X + rect.Width / 2, rect.Bottom), rect.BottomLeft, new(rect.X, rect.Y + rect.Height / 2),
        ];
        for (var i = 0; i < _handles.Length; i++)
        {
            _handles[i].Visibility = _locked ? Visibility.Collapsed : Visibility.Visible;
            Canvas.SetLeft(_handles[i], points[i].X - HandleSize / 2);
            Canvas.SetTop(_handles[i], points[i].Y - HandleSize / 2);
        }

        SizeText.Text = $"{r.Width} × {r.Height}";
        SizeLabel.Visibility = Visibility.Visible;
        SizeLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(SizeLabel, rect.X);
        Canvas.SetTop(SizeLabel, rect.Y >= SizeLabel.DesiredSize.Height + 6 ? rect.Y - SizeLabel.DesiredSize.Height - 6 : rect.Y + 6);
    }

    private void PlaceOnMonitor()
    {
        var b = Monitor.Bounds;
        SetWindowPos(new WindowInteropHelper(this).Handle, HWND_TOPMOST, b.X, b.Y, b.Width, b.Height, SWP_NOACTIVATE);
    }

    private nint KeepBoundsOnDpiChange(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_DPICHANGED)
        {
            var b = Monitor.Bounds;
            Marshal.StructureToPtr(new RECT { Left = b.X, Top = b.Y, Right = b.Right, Bottom = b.Bottom }, lParam, false);
        }
        return 0;
    }

    private const int WM_DPICHANGED = 0x02E0;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private static readonly nint HWND_TOPMOST = -1;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
