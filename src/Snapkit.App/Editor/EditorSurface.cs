using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Snapkit.App.Imaging;
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
    // Маркер размера ловится в 8 DIP; штрих карандаша хранит точки не чаще 2 px снимка
    private const double HandleHitDip = 8;
    private const double HandleSizeDip = 9;
    private const double StrokeStep = 2;
    private const double ToolbarGap = 10;
    private const double ScreenMargin = 8;
    private const double PanelGap = 12;
    private const double NoteGap = 8;

    // Последние выбранные инструмент, варианты групп, цвет и толщина запоминаются между снимками
    private static EditorTool _lastTool = EditorTool.Rectangle;
    private static readonly Dictionary<ToolGroup, EditorTool> _lastVariant = [];
    private static FillMode _lastFill = FillMode.None;
    private static string? _lastFillColor;
    private static LineStyle _lastLineStyle = LineStyle.Solid;
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

    private FillMode _fill = _lastFill;
    private string? _fillColor = _lastFillColor;
    private LineStyle _lineStyle = _lastLineStyle;

    private enum DragMode { None, Draw, Move, Handle, Pin, Crop }

    private PointD? _dragStart;
    private DragMode _drag;
    private int _dragHandle;
    private ImmutableList<Annotation>? _dragOriginal;
    private TextBox? _textBox;
    private PointD _textAnchor;
    private NoteEditorPopup? _notePopup;
    private ShortcutsPopup? _shortcuts;
    private ColorPickerPopup? _colorPicker;
    // Ждём щелчка пипеткой: чей цвет он задаст
    private ColorTarget? _pipette;

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
        _canvas.Source = context.Source;
        _canvas.SourceOrigin = context.SourceOrigin;
        _canvas.ShowSource = context.ShowSource;
        _canvas.HandleSize = HandleSizeDip * context.Scale;
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
        _toolbar.ColorPickerRequested += (_, target) => OpenColorPicker(target);
        _toolbar.FillSelected += (_, fill) => SetFill(fill, _fillColor);
        _toolbar.FillColorReset += (_, _) => SetFill(_fill, null);
        _toolbar.ThicknessSelected += (_, thickness) =>
        {
            _thickness = _lastThickness = thickness;
            ApplyToSelected(a => a with { Thickness = thickness });
        };
        _toolbar.LineStyleSelected += (_, style) =>
        {
            _lineStyle = _lastLineStyle = style;
            ApplyToSelected(a => HasLineStyle(a.Kind) ? a with { LineStyle = style } : a);
        };
        _toolbar.UndoClicked += (_, _) => Undo();
        _toolbar.RedoClicked += (_, _) => Redo();
        _toolbar.CopyClicked += (_, _) => CopyRequested?.Invoke(this, EventArgs.Empty);
        _toolbar.DoneClicked += (_, _) => Finish();
        _toolbar.HelpClicked += (_, _) => ToggleShortcuts();

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

    /// <summary>Инструментом «Обрезать» выделили новую область снимка (в координатах монитора).</summary>
    public event EventHandler<RectI>? CropRequested;

    /// <summary>Свои цвета из настроек и их сохранение (подключает CaptureController).</summary>
    public Func<string>? CustomColors { get; set; }
    public Action<string>? SaveCustomColors { get; set; }

    public RectI Region => _region;
    public char Letter => _context.Letter;
    public string SnapshotNote => _snapshotNote;
    public bool IsChanged => _history.IsChanged || _snapshotNote != _context.SnapshotNote;

    private RectI Origin => new(0, 0, _region.Width, _region.Height);
    private double Scale => _context.Scale;

    /// <summary>
    /// Разметка в координатах снимка — то, что уйдёт в ленту. Кружки, оставшиеся за краем после
    /// обрезки или уменьшения рамки, сдвигаются внутрь.
    /// </summary>
    public ImmutableList<Annotation> CurrentAnnotations
        => AnnotationGeometry.KeepPinsInside(AnnotationGeometry.ShiftForRegionChange(_history.Current, Origin, _region),
            _region.Width, _region.Height);

    /// <summary>Пользователь поменял границы снимка маркерами.</summary>
    public void SetRegion(RectI region)
    {
        _region = region;
        Layout();
        Refresh();
    }

    /// <summary>Отменить снимок так же, как по Esc (с вопросом, если есть правки).</summary>
    public void RequestCancel() => Cancelled?.Invoke(this, EventArgs.Empty);

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

        // Открытые подсказка, выбор цвета и пипетка закрываются по Esc и не отменяют снимок
        if (key == Key.Escape && (_shortcuts is not null || _colorPicker is not null || _pipette is not null))
        {
            if (_shortcuts is not null) ToggleShortcuts();
            CloseColorPicker();
            _pipette = null;
            Refresh();
            return true;
        }
        if (key == Key.F1 || (shift && key == Key.OemQuestion))
        {
            ToggleShortcuts();
            return true;
        }

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
        if (EditorKeys.ToolFor(key, _lastVariant) is not { } tool) return false;
        SetTool(tool);
        return true;
    }

    private void SetTool(EditorTool tool)
    {
        CommitText();
        CloseNote(save: true);
        _tool = _lastTool = tool;
        if (EditorKeys.GroupOf(tool) is var group and not ToolGroup.None) _lastVariant[group] = tool;
        if (tool != EditorTool.Select) _canvas.SelectedId = null;
        Refresh();
    }

    // Подсказка со всеми клавишами — по центру монитора
    private void ToggleShortcuts()
    {
        if (_shortcuts is { } shown)
        {
            _shortcuts = null;
            _layer.Children.Remove(shown);
            return;
        }
        var popup = new ShortcutsPopup();
        popup.Closed += (_, _) => ToggleShortcuts();
        _shortcuts = popup;
        _layer.Children.Add(popup);
        popup.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(popup, Math.Max(ScreenMargin, (Width - popup.DesiredSize.Width) / 2));
        Canvas.SetTop(popup, Math.Max(ScreenMargin, (Height - popup.DesiredSize.Height) / 2));
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
        CloseColorPicker();
        if (_pipette is not null)
        {
            PickColor(p);
            return;
        }

        switch (_tool)
        {
            case EditorTool.Select when e.ClickCount == 2:
                // Двойной клик по фигуре или её кружку — правка заметки
                if (AnnotationGeometry.TopmostHit(_history.Current, p, SelectToleranceDip * Scale) is { } noted) OpenNote(noted);
                break;
            case EditorTool.Select:
                BeginSelectDrag(p);
                break;
            case EditorTool.Text:
                OpenTextBox(p);
                break;
            case EditorTool.Pin:
                var pin = NewShape(AnnotationKind.Pin, p);
                _history.Push(_history.Current.Add(pin));
                OpenNote(pin.Id);
                break;
            case EditorTool.Eraser:
                if (AnnotationGeometry.TopmostHit(_history.Current, p, SelectToleranceDip * Scale) is { } erased)
                {
                    _history.Push(_history.Current.RemoveAll(a => a.Id == erased));
                    if (_canvas.SelectedId == erased) _canvas.SelectedId = null;
                }
                break;
            case EditorTool.Crop:
                _dragStart = p;
                _drag = DragMode.Crop;
                _canvas.CropPreview = new Rect(p.X, p.Y, 0, 0);
                _canvas.CaptureMouse();
                break;
            default:
                if (KindOf(_tool) is not { } kind) break;
                _dragStart = p;
                _drag = DragMode.Draw;
                _canvas.Preview = kind == AnnotationKind.CurvedArrow ? NewShape(kind, p, p, p) : NewShape(kind, p, p);
                _canvas.CaptureMouse();
                break;
        }
        Refresh();
    }

    /// <summary>Выбор: что хватает щелчок, решает AnnotationHandles.PickForSelect.</summary>
    private void BeginSelectDrag(PointD p)
    {
        var target = AnnotationHandles.PickForSelect(_history.Current, _canvas.SelectedId, p, HandleHitDip * Scale, SelectToleranceDip * Scale);
        _canvas.SelectedId = target?.Id;
        switch (target?.Kind)
        {
            case SelectTargetKind.Handle:
                StartDrag(p, DragMode.Handle, target.Value.Handle);
                break;
            case SelectTargetKind.Pin:
                StartDrag(p, DragMode.Pin, 0);
                break;
            case SelectTargetKind.Shape:
                StartDrag(p, DragMode.Move, 0);
                break;
        }
    }

    private void StartDrag(PointD p, DragMode mode, int handle)
    {
        _dragStart = p;
        _drag = mode;
        _dragHandle = handle;
        _dragOriginal = _history.Current;
        _canvas.CaptureMouse();
    }

    private void OnCanvasMove(object sender, MouseEventArgs e)
    {
        var p = ToMonitor(e.GetPosition(_canvas));
        if (_dragStart is not { } start)
        {
            _canvas.Cursor = HoverCursor(p);
            return;
        }

        if (_drag == DragMode.Crop)
        {
            _canvas.CropPreview = new Rect(new Point(start.X, start.Y), new Point(p.X, p.Y));
        }
        else if (_drag != DragMode.Draw && _dragOriginal is { } original && _canvas.SelectedId is { } id)
        {
            _canvas.Items = original.Select(a => a.Id != id ? a : _drag switch
            {
                DragMode.Handle => AnnotationHandles.Move(a, _dragHandle, p),
                DragMode.Pin => AnnotationHandles.WithPinAt(a, Offset(AnnotationGeometry.PinCenter(a), p.X - start.X, p.Y - start.Y)),
                _ => AnnotationGeometry.Translate(a, p.X - start.X, p.Y - start.Y),
            }).ToList();
        }
        else if (_canvas.Preview is { } preview)
        {
            _canvas.Preview = preview.Kind switch
            {
                AnnotationKind.Pencil or AnnotationKind.Marker => preview with { Points = preview.Points.Add(p) },
                AnnotationKind.CurvedArrow => preview with { Points = [start, AnnotationGeometry.DefaultBend(start, p), p] },
                _ => preview with { Points = [start, p] },
            };
        }
        _canvas.InvalidateVisual();
    }

    private void OnCanvasUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is not { } start) return;
        var p = ToMonitor(e.GetPosition(_canvas));
        _canvas.ReleaseMouseCapture();

        if (_drag == DragMode.Crop)
        {
            _canvas.CropPreview = null;
            // Новая область — внутри текущей; слишком маленькая не принимается
            var left = (int)Math.Round(Math.Max(Math.Min(start.X, p.X), _region.X));
            var top = (int)Math.Round(Math.Max(Math.Min(start.Y, p.Y), _region.Y));
            var right = (int)Math.Round(Math.Min(Math.Max(start.X, p.X), _region.Right));
            var bottom = (int)Math.Round(Math.Min(Math.Max(start.Y, p.Y), _region.Bottom));
            if (right - left >= MinShapeSize && bottom - top >= MinShapeSize)
                CropRequested?.Invoke(this, new RectI(left, top, right - left, bottom - top));
        }
        else if (_drag != DragMode.Draw && _dragOriginal is not null)
        {
            // Каждое законченное перетаскивание — один шаг истории
            if (p != start) _history.Push(_canvas.Items.ToImmutableList());
        }
        else if (_canvas.Preview is { } preview)
        {
            var shape = preview.Kind is AnnotationKind.Pencil or AnnotationKind.Marker
                ? preview with { Points = StrokeSimplifier.Simplify(preview.Points, StrokeStep) }
                : preview;
            if (AnnotationGeometry.IsBigEnough(shape, MinShapeSize))
            {
                _history.Push(_history.Current.Add(shape));
                _dragStart = null;
                _canvas.Preview = null;
                // Сразу под новой фигурой — поле для комментария
                OpenNote(shape.Id);
            }
        }

        _dragStart = null;
        _dragOriginal = null;
        _drag = DragMode.None;
        _canvas.Preview = null;
        Refresh();
    }

    private Cursor HoverCursor(PointD p)
    {
        if (_pipette is not null) return Cursors.Cross;
        var tolerance = SelectToleranceDip * Scale;
        switch (_tool)
        {
            case EditorTool.Select:
                if (_canvas.SelectedId is { } id && _history.Current.FirstOrDefault(a => a.Id == id) is { } selected
                    && AnnotationHandles.Hit(selected, p, HandleHitDip * Scale) is not null)
                    return Cursors.Hand;
                return AnnotationGeometry.TopmostHit(_history.Current, p, tolerance) is null ? Cursors.Arrow : Cursors.SizeAll;
            case EditorTool.Eraser:
                return AnnotationGeometry.TopmostHit(_history.Current, p, tolerance) is null ? Cursors.No : Cursors.Hand;
            case EditorTool.Text:
                return Cursors.IBeam;
            default:
                return Cursors.Cross;
        }
    }

    /// <summary>Вид фигуры, которую рисует инструмент перетаскиванием; у остальных инструментов — null.</summary>
    private static AnnotationKind? KindOf(EditorTool tool) => tool switch
    {
        EditorTool.Rectangle => AnnotationKind.Rectangle,
        EditorTool.RoundedRectangle => AnnotationKind.RoundedRectangle,
        EditorTool.Ellipse => AnnotationKind.Ellipse,
        EditorTool.Arrow => AnnotationKind.Arrow,
        EditorTool.CurvedArrow => AnnotationKind.CurvedArrow,
        EditorTool.WideArrow => AnnotationKind.WideArrow,
        EditorTool.Line => AnnotationKind.Line,
        EditorTool.Pencil => AnnotationKind.Pencil,
        EditorTool.Marker => AnnotationKind.Marker,
        EditorTool.Blur => AnnotationKind.Blur,
        _ => null,
    };

    private static PointD Offset(PointD p, double dx, double dy) => new(p.X + dx, p.Y + dy);

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

    // Новая фигура получает текущие цвет, толщину, стиль линии и — у рамок и овала — заливку
    private Annotation NewShape(AnnotationKind kind, params PointD[] points)
    {
        var fillable = kind is AnnotationKind.Rectangle or AnnotationKind.RoundedRectangle or AnnotationKind.Ellipse;
        return new Annotation(Guid.NewGuid(), kind, [.. points], _color, _thickness, null, null,
            fillable ? _fill : FillMode.None, fillable ? _fillColor : null, _lineStyle);
    }

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

    /// <summary>Новые цвет, заливка, толщина или стиль применяются и к выделенной фигуре — одним шагом истории.</summary>
    private void ApplyToSelected(Func<Annotation, Annotation> change)
    {
        if (_canvas.SelectedId is { } id && _history.Current.FirstOrDefault(a => a.Id == id) is { } selected
            && change(selected) is var changed && changed != selected)
        {
            _history.Push(_history.Current.Replace(selected, changed));
        }
        Refresh();
    }

    private static bool IsFillable(AnnotationKind kind)
        => kind is AnnotationKind.Rectangle or AnnotationKind.RoundedRectangle or AnnotationKind.Ellipse;

    // Стиль линии есть у контуров и штрихов; у текста, кружка, маркера и размытия — нет
    private static bool HasLineStyle(AnnotationKind kind)
        => kind is not (AnnotationKind.Text or AnnotationKind.Pin or AnnotationKind.Marker or AnnotationKind.Blur or AnnotationKind.WideArrow);

    private void SetFill(FillMode fill, string? fillColor)
    {
        _fill = _lastFill = fill;
        _fillColor = _lastFillColor = fillColor;
        ApplyToSelected(a => IsFillable(a.Kind) ? a with { Fill = fill, FillColor = fillColor } : a);
    }

    private void OpenColorPicker(ColorTarget target)
    {
        CloseColorPicker();
        var current = target == ColorTarget.Stroke ? _color : _fillColor ?? _color;
        var picker = new ColorPickerPopup(target, current, HexColor.Parse(CustomColors?.Invoke() ?? ""));
        picker.ColorChosen += (_, chosen) =>
        {
            CloseColorPicker();
            ApplyColor(target, chosen.Hex, chosen.IsCustom);
        };
        picker.PipetteRequested += (_, _) =>
        {
            CloseColorPicker();
            _pipette = target;
            _canvas.Cursor = Cursors.Cross;
        };
        picker.Closed += (_, _) => CloseColorPicker();
        _colorPicker = picker;
        _layer.Children.Add(picker);

        // Над панелью инструментов, а если там нет места — под ней
        picker.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var barLeft = Canvas.GetLeft(_toolbar);
        var barTop = Canvas.GetTop(_toolbar);
        var top = barTop - picker.DesiredSize.Height - NoteGap;
        if (top < ScreenMargin) top = barTop + _toolbar.ActualHeight + NoteGap;
        // Окно встаёт над кнопкой цвета, а не над началом панели
        Canvas.SetLeft(picker, Math.Clamp(barLeft + _toolbar.ColorButtonLeft, ScreenMargin, Math.Max(ScreenMargin, Width - picker.DesiredSize.Width - ScreenMargin)));
        Canvas.SetTop(picker, Math.Max(ScreenMargin, top));
    }

    private void CloseColorPicker()
    {
        if (_colorPicker is not { } picker) return;
        _colorPicker = null;
        _layer.Children.Remove(picker);
        Window.GetWindow(this)?.Focus();
    }

    private void ApplyColor(ColorTarget target, string hex, bool isCustom)
    {
        if (isCustom && CustomColors is { } read && SaveCustomColors is { } save) save(HexColor.AddRecent(read(), hex));
        if (target == ColorTarget.Stroke)
        {
            _color = _lastColor = hex;
            ApplyToSelected(a => a with { Color = hex });
        }
        else
        {
            // Выбрали цвет заливки — значит, заливка нужна: без неё включаем сплошную
            SetFill(_fill == FillMode.None ? FillMode.Solid : _fill, hex);
        }
    }

    /// <summary>Пипетка: цвет пикселя снимка под щелчком.</summary>
    private void PickColor(PointD p)
    {
        var target = _pipette!.Value;
        _pipette = null;
        if (PixelSampler.HexAt(_context.Source, _context.SourceOrigin, p) is { } hex) ApplyColor(target, hex, isCustom: true);
        else Refresh();
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
        var selected = _canvas.SelectedId is { } id ? _history.Current.FirstOrDefault(a => a.Id == id) : null;
        var fillApplicable = EditorKeys.GroupOf(_tool) == ToolGroup.Shape || (selected is not null && IsFillable(selected.Kind));
        _toolbar.Show(_tool, _lastVariant, _color, _thickness, _fill, _fillColor, _lineStyle, fillApplicable,
            _history.CanUndo, _history.CanRedo);

        var number = 0;
        _comments.SetItems(_history.Current
            .Where(a => a.HasNote)
            .Select(a => new CommentItem(a.Id, NoteNumbers.Label(_context.Letter, ++number), a.Note!))
            .ToList());
    }

    /// <summary>
    /// Верх панели инструментов (в единицах WPF): под снимком; нет места — над ним;
    /// и там нет — внутри снимка у нижнего края. Панель всегда остаётся в рабочей области:
    /// у картинки больше экрана нижний край снимка за пределами монитора.
    /// </summary>
    internal static double ToolbarTop(double regionTop, double regionBottom, double barHeight, double workTop, double workBottom)
    {
        var barTop = regionBottom + ToolbarGap;
        if (barTop + barHeight > workBottom - ScreenMargin) barTop = regionTop - barHeight - ToolbarGap;
        if (barTop < ScreenMargin) barTop = regionBottom - barHeight - ToolbarGap;
        var lowest = Math.Max(workTop + ScreenMargin, workBottom - barHeight - ScreenMargin);
        return Math.Clamp(barTop, workTop + ScreenMargin, lowest);
    }

    /// <summary>
    /// Левый край панели инструментов (в единицах WPF): по левому краю снимка, в пределах экрана
    /// и не под панелью комментариев — она во всю высоту экрана и закрыла бы кнопки инструментов.
    /// </summary>
    internal static double ToolbarLeft(double regionLeft, double barWidth, double screenWidth, double panelLeft, double panelWidth)
    {
        var left = Math.Clamp(regionLeft, ScreenMargin, Math.Max(ScreenMargin, screenWidth - barWidth - ScreenMargin));
        var panelRight = panelLeft + panelWidth;
        if (left < panelRight + PanelGap && left + barWidth > panelLeft - PanelGap)
            left = panelLeft < screenWidth / 2 ? panelRight + PanelGap : panelLeft - PanelGap - barWidth;
        return left;
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

        // Панель комментариев — у края рабочей области, с той стороны, где меньше снимка
        var workLeft = _context.WorkArea.X / Scale;
        var workTop = _context.WorkArea.Y / Scale;
        var workRight = (_context.WorkArea.X + _context.WorkArea.Width) / Scale;
        var regionCenter = left + _region.Width / Scale / 2;
        var panelOnRight = regionCenter < (workLeft + workRight) / 2;
        var panelLeft = panelOnRight ? workRight - _comments.Width - PanelGap : workLeft + PanelGap;
        _comments.Height = Math.Max(200, WorkBottom - workTop - 2 * PanelGap);
        Canvas.SetLeft(_comments, panelLeft);
        Canvas.SetTop(_comments, workTop + PanelGap);

        _toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _toolbar.DesiredSize;
        var bottom = (_region.Y + _region.Height) / Scale;
        Canvas.SetLeft(_toolbar, ToolbarLeft(left, size.Width, Width, panelLeft, _comments.Width));
        Canvas.SetTop(_toolbar, ToolbarTop(top, bottom, size.Height, workTop, WorkBottom));
    }
}
