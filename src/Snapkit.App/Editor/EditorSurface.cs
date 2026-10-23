using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Snapkit.App.Rendering;
using Snapkit.Core;
using Snapkit.Core.Editing;
using Snapkit.Core.Model;

namespace Snapkit.App.Editor;

/// <summary>
/// Редактор разметки поверх области снимка: холст, панель инструментов, ввод текста.
/// Внутри фигуры хранятся в координатах монитора, поэтому изменение границ области
/// не трогает историю правок; в координаты снимка они переводятся при «Готово».
/// </summary>
public sealed class EditorSurface : UserControl
{
    private const double SelectToleranceDip = 6;
    private const double EdgeGripDip = 8;
    private const double MinShapeSize = 4;
    private const double ToolbarGap = 10;
    private const double ScreenMargin = 8;
    private const double PanelGap = 12;
    private const double NoteGap = 8;

    // Последние выбранные инструмент, цвет и толщина запоминаются между снимками
    private static EditorTool _lastTool = EditorTool.Rectangle;
    private static string _lastColor = Palette.Markup[0];
    private static double _lastThickness = Palette.DefaultThickness;

    private readonly EditorContext _context;
    private readonly Canvas _layer = new();
    private readonly EditorCanvas _canvas = new();
    private readonly Canvas _textLayer = new();
    private readonly EditorToolbar _toolbar = new();
    private readonly CommentsPanel _comments = new();
    private readonly AnnotationHistory _history;

    private RectI _region;
    private EditorTool _tool = _lastTool;
    private string _color = _lastColor;
    private double _thickness = _lastThickness;
    private string _snapshotNote;

    private PointD? _dragStart;
    private ImmutableList<Annotation>? _dragOriginal;
    private TextBox? _textBox;
    private PointD _textAnchor;
    private NoteEditorPopup? _notePopup;

    public EditorSurface(EditorContext context)
    {
        _context = context;
        _region = context.Region;
        _snapshotNote = context.SnapshotNote;
        // На входе разметка в координатах снимка — переводим в координаты монитора
        _history = new AnnotationHistory(AnnotationGeometry.ShiftForRegionChange(context.Annotations, context.Region, Origin));

        Width = context.MonitorSize.Width;
        Height = context.MonitorSize.Height;
        Focusable = false;

        var toPixels = new ScaleTransform(1 / context.Scale, 1 / context.Scale);
        _canvas.RenderTransform = toPixels;
        _canvas.Background = context.Background;
        _canvas.Letter = context.Letter;
        _textLayer.RenderTransform = toPixels;
        _layer.Children.Add(_canvas);
        _layer.Children.Add(_textLayer);
        _layer.Children.Add(_toolbar);
        _layer.Children.Add(_comments);
        Content = _layer;
        // Клики по панелям не должны доходить до холста и рамки снимка
        _toolbar.MouseLeftButtonDown += (_, e) => e.Handled = true;

        _canvas.MouseLeftButtonDown += OnCanvasDown;
        _canvas.MouseMove += OnCanvasMove;
        _canvas.MouseLeftButtonUp += OnCanvasUp;

        _toolbar.ToolSelected += (_, tool) => SetTool(tool);
        _toolbar.ColorSelected += (_, color) => { _color = _lastColor = color; RecolorSelected(); Refresh(); };
        _toolbar.ThicknessSelected += (_, thickness) => { _thickness = _lastThickness = thickness; Refresh(); };
        _toolbar.UndoClicked += (_, _) => Undo();
        _toolbar.RedoClicked += (_, _) => Redo();
        _toolbar.CopyClicked += (_, _) => CopyRequested?.Invoke(this, EventArgs.Empty);
        _toolbar.DoneClicked += (_, _) => Finish();

        _comments.SetSnapshotNote(context.SnapshotNote);
        _comments.SnapshotNoteChanged += (_, note) => _snapshotNote = note;
        _comments.ItemClicked += (_, id) =>
        {
            CloseNote(save: true);
            _tool = _lastTool = EditorTool.Select;
            _canvas.SelectedId = id;
            Refresh();
        };
        _comments.ItemRemoved += (_, id) => RemoveNote(id);

        Loaded += (_, _) => Layout();
        Refresh();
    }

    public event EventHandler<EditorResult>? Done;
    public event EventHandler? Cancelled;
    public event EventHandler? CopyRequested;

    public RectI Region => _region;
    public char Letter => _context.Letter;
    public bool IsChanged => _history.IsChanged || _snapshotNote != _context.SnapshotNote;

    private RectI Origin => new(0, 0, _region.Width, _region.Height);
    private double Scale => _context.Scale;

    /// <summary>Разметка в координатах снимка — то, что уйдёт в ленту.</summary>
    public ImmutableList<Annotation> CurrentAnnotations
        => AnnotationGeometry.ShiftForRegionChange(_history.Current, Origin, _region);

    /// <summary>Пользователь поменял границы снимка маркерами.</summary>
    public void SetRegion(RectI region)
    {
        _region = region;
        Layout();
        Refresh();
    }

    public void Finish()
    {
        CommitText();
        CloseNote(save: true);
        Done?.Invoke(this, new EditorResult(CurrentAnnotations, _snapshotNote, IsChanged));
    }

    /// <summary>Клавиши редактора. true — клавиша обработана.</summary>
    public bool HandleKey(KeyEventArgs e)
    {
        // Пока пишут текст, заметку или общий комментарий, клавиши принадлежат полю ввода
        if (_textBox is not null || Keyboard.FocusedElement is TextBox) return false;
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        switch (key)
        {
            case Key.Z when ctrl && shift: Redo(); return true;
            case Key.Z when ctrl: Undo(); return true;
            case Key.Y when ctrl: Redo(); return true;
            case Key.C when ctrl && shift: CopyRequested?.Invoke(this, EventArgs.Empty); return true;
            case Key.Delete or Key.Back: DeleteSelected(); return true;
            case Key.Enter: Finish(); return true;
            case Key.Escape: Cancelled?.Invoke(this, EventArgs.Empty); return true;
        }
        if (ctrl || shift) return false;
        EditorTool? tool = key switch
        {
            Key.V => EditorTool.Select,
            Key.R => EditorTool.Rectangle,
            Key.A => EditorTool.Arrow,
            Key.T => EditorTool.Text,
            Key.C => EditorTool.Pin,
            _ => null,
        };
        if (tool is null) return false;
        SetTool(tool.Value);
        return true;
    }

    private void SetTool(EditorTool tool)
    {
        CommitText();
        CloseNote(save: true);
        _tool = _lastTool = tool;
        if (tool != EditorTool.Select) _canvas.SelectedId = null;
        Refresh();
    }

    private void OnCanvasDown(object sender, MouseButtonEventArgs e)
    {
        // В режиме «Выбор» края области отдаём окну — там маркеры изменения границ снимка
        if (_tool == EditorTool.Select && _context.RegionEditable && NearRegionEdge(e.GetPosition(_layer))) return;

        e.Handled = true;
        CommitText();
        // Начали рисовать дальше — непустая заметка сохраняется, пустая просто закрывается
        CloseNote(save: true);
        var p = ToMonitor(e.GetPosition(_canvas));

        switch (_tool)
        {
            case EditorTool.Select when e.ClickCount == 2:
                // Двойной клик по фигуре или её кружку — правка заметки
                if (AnnotationGeometry.TopmostHit(_history.Current, p, SelectToleranceDip * Scale) is { } noted) OpenNote(noted);
                break;
            case EditorTool.Select:
                _canvas.SelectedId = AnnotationGeometry.TopmostHit(_history.Current, p, SelectToleranceDip * Scale);
                if (_canvas.SelectedId is not null)
                {
                    _dragStart = p;
                    _dragOriginal = _history.Current;
                    _canvas.CaptureMouse();
                }
                break;
            case EditorTool.Rectangle or EditorTool.Arrow:
                _dragStart = p;
                _canvas.Preview = NewShape(_tool == EditorTool.Rectangle ? AnnotationKind.Rectangle : AnnotationKind.Arrow, p, p);
                _canvas.CaptureMouse();
                break;
            case EditorTool.Text:
                OpenTextBox(p);
                break;
            case EditorTool.Pin:
                var pin = NewShape(AnnotationKind.Pin, p);
                _history.Push(_history.Current.Add(pin));
                OpenNote(pin.Id);
                break;
        }
        Refresh();
    }

    private void OnCanvasMove(object sender, MouseEventArgs e)
    {
        var p = ToMonitor(e.GetPosition(_canvas));
        if (_dragStart is not { } start)
        {
            _canvas.Cursor = _tool switch
            {
                EditorTool.Select => AnnotationGeometry.TopmostHit(_history.Current, p, SelectToleranceDip * Scale) is null ? Cursors.Arrow : Cursors.SizeAll,
                EditorTool.Text => Cursors.IBeam,
                _ => Cursors.Cross,
            };
            return;
        }

        if (_tool == EditorTool.Select && _dragOriginal is { } original && _canvas.SelectedId is { } id)
        {
            _canvas.Items = original.Select(a => a.Id == id ? AnnotationGeometry.Translate(a, p.X - start.X, p.Y - start.Y) : a).ToList();
        }
        else if (_canvas.Preview is { } preview)
        {
            _canvas.Preview = preview with { Points = [start, p] };
        }
        _canvas.InvalidateVisual();
    }

    private void OnCanvasUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is not { } start) return;
        var p = ToMonitor(e.GetPosition(_canvas));
        _canvas.ReleaseMouseCapture();

        if (_tool == EditorTool.Select && _dragOriginal is not null)
        {
            if (p != start) _history.Push(_canvas.Items.ToImmutableList());
        }
        else if (_canvas.Preview is { } shape && IsBigEnough(shape))
        {
            _history.Push(_history.Current.Add(shape));
            _dragStart = null;
            _canvas.Preview = null;
            // Сразу под новой фигурой — поле для комментария
            OpenNote(shape.Id);
        }

        _dragStart = null;
        _dragOriginal = null;
        _canvas.Preview = null;
        Refresh();
    }

    /// <summary>Открыть поле заметки под фигурой (для кружка — под ним).</summary>
    private void OpenNote(Guid id)
    {
        if (_history.Current.FirstOrDefault(a => a.Id == id) is not { } annotation) return;
        CloseNote(save: true);
        var popup = new NoteEditorPopup(id, annotation.Note ?? "");
        popup.Committed += (_, _) => CloseNote(save: true);
        popup.Dismissed += (_, _) => CloseNote(save: false);
        _notePopup = popup;
        if (annotation.Kind == AnnotationKind.Pin && !annotation.HasNote) _canvas.PendingPinId = id;

        _layer.Children.Add(popup);
        popup.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var (x, y, w, h) = annotation.Kind == AnnotationKind.Pin
            ? (annotation.Points[0].X - AnnotationGeometry.PinDiameter / 2, annotation.Points[0].Y - AnnotationGeometry.PinDiameter / 2,
               AnnotationGeometry.PinDiameter, AnnotationGeometry.PinDiameter)
            : AnnotationGeometry.Bounds(annotation);
        var size = popup.DesiredSize;
        var top = (y + h) / Scale + NoteGap;
        if (top + size.Height > WorkBottom - ScreenMargin) top = y / Scale - size.Height - NoteGap;
        var left = Math.Clamp(x / Scale, ScreenMargin, Math.Max(ScreenMargin, Width - size.Width - ScreenMargin));
        Canvas.SetLeft(popup, left);
        Canvas.SetTop(popup, Math.Max(ScreenMargin, top));
    }

    /// <summary>
    /// Закрыть поле заметки. save — записать текст (пустой текст убирает заметку).
    /// Кружок, оставшийся без заметки, удаляется.
    /// </summary>
    private void CloseNote(bool save)
    {
        if (_notePopup is not { } popup) return;
        _notePopup = null;
        _canvas.PendingPinId = null;
        _layer.Children.Remove(popup);

        if (_history.Current.FirstOrDefault(a => a.Id == popup.AnnotationId) is { } annotation)
        {
            var text = popup.Text.Trim();
            var note = text.Length == 0 ? null : text;
            if (annotation.Kind == AnnotationKind.Pin && (save ? note is null : !annotation.HasNote))
                _history.Push(_history.Current.Remove(annotation));
            else if (save && note != annotation.Note)
                _history.Push(_history.Current.Replace(annotation, annotation with { Note = note }));
        }
        Window.GetWindow(this)?.Focus();
        Refresh();
    }

    /// <summary>Удалить заметку из панели: у кружка — вместе с ним, у фигуры — только текст.</summary>
    private void RemoveNote(Guid id)
    {
        CloseNote(save: true);
        if (_history.Current.FirstOrDefault(a => a.Id == id) is not { } annotation) return;
        _history.Push(annotation.Kind == AnnotationKind.Pin
            ? _history.Current.Remove(annotation)
            : _history.Current.Replace(annotation, annotation with { Note = null }));
        Refresh();
    }

    private static bool IsBigEnough(Annotation shape)
    {
        var (a, b) = (shape.Points[0], shape.Points[1]);
        var (w, h) = (Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
        return shape.Kind == AnnotationKind.Rectangle
            ? w >= MinShapeSize && h >= MinShapeSize
            : Math.Sqrt(w * w + h * h) >= MinShapeSize;
    }

    private Annotation NewShape(AnnotationKind kind, params PointD[] points)
        => new(Guid.NewGuid(), kind, [.. points], _color, _thickness, null, null);

    private void OpenTextBox(PointD p)
    {
        _textAnchor = p;
        _textBox = new TextBox
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.SemiBold,
            FontSize = AnnotationGeometry.TextFontSize(_thickness),
            Foreground = Palette.Brush(_color),
            CaretBrush = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)),
            BorderBrush = Palette.Brush(Palette.Accent),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(0),
            MinWidth = 40,
            AcceptsReturn = true,
            // Поле стоит так, чтобы текст оказался ровно там, где потом нарисуется
            Margin = new Thickness(-3, -2, 0, 0),
        };
        _textBox.PreviewKeyDown += OnTextKeyDown;
        _textBox.LostKeyboardFocus += (_, _) => CommitText();
        Canvas.SetLeft(_textBox, p.X - _region.X);
        Canvas.SetTop(_textBox, p.Y - _region.Y);
        _textLayer.Children.Add(_textBox);
        Dispatcher.BeginInvoke(() => _textBox?.Focus());
    }

    private void OnTextKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            CommitText();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseTextBox();
            e.Handled = true;
        }
    }

    private void CommitText()
    {
        if (_textBox is null) return;
        var text = _textBox.Text.TrimEnd();
        CloseTextBox();
        if (string.IsNullOrWhiteSpace(text)) return;
        _history.Push(_history.Current.Add(new Annotation(Guid.NewGuid(), AnnotationKind.Text, [_textAnchor],
            _color, _thickness, text, null)));
        Refresh();
    }

    private void CloseTextBox()
    {
        if (_textBox is null) return;
        var box = _textBox;
        _textBox = null;
        _textLayer.Children.Remove(box);
        Window.GetWindow(this)?.Focus();
    }

    private void Undo()
    {
        CommitText();
        CloseNote(save: false);
        _history.Undo();
        _canvas.SelectedId = null;
        Refresh();
    }

    private void Redo()
    {
        CloseNote(save: false);
        _history.Redo();
        _canvas.SelectedId = null;
        Refresh();
    }

    private void DeleteSelected()
    {
        if (_canvas.SelectedId is not { } id) return;
        _history.Push(_history.Current.RemoveAll(a => a.Id == id));
        _canvas.SelectedId = null;
        Refresh();
    }

    // Новый цвет из панели применяется и к выделенной фигуре
    private void RecolorSelected()
    {
        if (_canvas.SelectedId is not { } id) return;
        _history.Push(_history.Current.Select(a => a.Id == id ? a with { Color = _color } : a).ToImmutableList());
    }

    private PointD ToMonitor(Point canvasPoint)
        => new(Math.Clamp(canvasPoint.X, 0, _region.Width) + _region.X, Math.Clamp(canvasPoint.Y, 0, _region.Height) + _region.Y);

    private bool NearRegionEdge(Point layerPoint)
    {
        var r = new Rect(_region.X / Scale, _region.Y / Scale, _region.Width / Scale, _region.Height / Scale);
        return layerPoint.X - r.Left <= EdgeGripDip || r.Right - layerPoint.X <= EdgeGripDip
            || layerPoint.Y - r.Top <= EdgeGripDip || r.Bottom - layerPoint.Y <= EdgeGripDip;
    }

    private double WorkBottom => (_context.WorkArea.Y + _context.WorkArea.Height) / Scale;

    private void Refresh()
    {
        _canvas.Items = _history.Current;
        _canvas.InvalidateVisual();
        _toolbar.Show(_tool, _color, _thickness, _history.CanUndo, _history.CanRedo);

        var number = 0;
        _comments.SetItems(_history.Current
            .Where(a => a.HasNote)
            .Select(a => new CommentItem(a.Id, NoteNumbers.Label(_context.Letter, ++number), a.Note!))
            .ToList());
    }

    /// <summary>Холст — ровно на области снимка, панель инструментов — под ней (или над ней, если места нет).</summary>
    private void Layout()
    {
        _canvas.PixelWidth = _region.Width;
        _canvas.PixelHeight = _region.Height;
        _canvas.Offset = new PointD(_region.X, _region.Y);
        _canvas.InvalidateMeasure();
        var left = _region.X / Scale;
        var top = _region.Y / Scale;
        Canvas.SetLeft(_canvas, left);
        Canvas.SetTop(_canvas, top);
        Canvas.SetLeft(_textLayer, left);
        Canvas.SetTop(_textLayer, top);

        _toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _toolbar.DesiredSize;
        var bottom = (_region.Y + _region.Height) / Scale;
        var barTop = bottom + ToolbarGap;
        if (barTop + size.Height > WorkBottom - ScreenMargin) barTop = top - size.Height - ToolbarGap;
        if (barTop < ScreenMargin) barTop = bottom - size.Height - ToolbarGap;
        var barLeft = Math.Clamp(left, ScreenMargin, Math.Max(ScreenMargin, Width - size.Width - ScreenMargin));
        Canvas.SetLeft(_toolbar, barLeft);
        Canvas.SetTop(_toolbar, barTop);

        // Панель комментариев — у края рабочей области, с той стороны, где меньше снимка
        var workLeft = _context.WorkArea.X / Scale;
        var workTop = _context.WorkArea.Y / Scale;
        var workRight = (_context.WorkArea.X + _context.WorkArea.Width) / Scale;
        var regionCenter = left + _region.Width / Scale / 2;
        var panelOnRight = regionCenter < (workLeft + workRight) / 2;
        _comments.Height = Math.Max(200, WorkBottom - workTop - 2 * PanelGap);
        Canvas.SetLeft(_comments, panelOnRight ? workRight - _comments.Width - PanelGap : workLeft + PanelGap);
        Canvas.SetTop(_comments, workTop + PanelGap);
    }
}
