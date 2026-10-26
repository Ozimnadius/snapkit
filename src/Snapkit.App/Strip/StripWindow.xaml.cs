using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Snapkit.App.Shell;
using Snapkit.Core;

namespace Snapkit.App.Strip;

/// <summary>
/// Лента снимков у края экрана. Окно только сообщает о действиях пользователя;
/// что с ними делать, решает App.
/// </summary>
public partial class StripWindow : Window
{
    public StripWindow(StripViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
        SourceInitialized += (_, _) =>
        {
            var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            LogoImage.Source = AppIcon.Frame((int)Math.Ceiling(18 * scale));
        };
    }

    public StripViewModel ViewModel { get; }

    /// <summary>Свой формат перетаскивания: по нему лента узнаёт свою карточку и меняет порядок.</summary>
    public const string CaptureIdFormat = "Snapkit.CaptureId";

    /// <summary>Файл снимка для перетаскивания в другие окна (рисует и пишет App); null — без файла.</summary>
    public Func<Guid, string?>? DragFileFor { get; set; }

    // Где нажали на карточку — перетаскивание начинается, когда мышь отошла дальше системного порога
    private Point? _pressPoint;
    private Guid _pressId;
    // Карточку перетащили — отпускание кнопки не открывает редактор
    private bool _suppressOpen;

    public event EventHandler? NewCaptureRequested;
    public event EventHandler<Guid>? OpenCaptureRequested;
    public event EventHandler<Guid>? DeleteCaptureRequested;
    public event EventHandler<Guid>? CopyCaptureRequested;
    public event EventHandler? ClearRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler? PasteImageRequested;
    public event EventHandler? CopyPackageRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler<bool>? TopmostToggled;
    public event EventHandler? Moved;
    public event EventHandler<(Guid Id, int Slot)>? ReorderRequested;

    /// <summary>Карточку бросили в другое окно и оно её приняло.</summary>
    public event EventHandler<Guid>? DroppedOutside;

    // Бросили на саму ленту (перестановка), а не в другое окно
    private bool _droppedOnStrip;
    public event EventHandler<Guid>? SaveCaptureRequested;
    public event EventHandler? SavePackageRequested;
    public event EventHandler? ImportFileRequested;
    public event EventHandler<string[]>? FilesDropped;
    public event EventHandler<bool>? ShowStripToggled;
    public event EventHandler? MinimizeRequested;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        DragMove();
        Moved?.Invoke(this, EventArgs.Empty);
    }

    private void NewCapture_Click(object sender, RoutedEventArgs e) => NewCaptureRequested?.Invoke(this, EventArgs.Empty);

    private void Clear_Click(object sender, RoutedEventArgs e) => ClearRequested?.Invoke(this, EventArgs.Empty);

    private void Minimize_Click(object sender, RoutedEventArgs e) => MinimizeRequested?.Invoke(this, EventArgs.Empty);

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _pressPoint = null;
        if (_suppressOpen)
        {
            _suppressOpen = false;
            return;
        }
        if (sender is FrameworkElement { Tag: Guid id }) OpenCaptureRequested?.Invoke(this, id);
    }

    private void Card_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _suppressOpen = false;
        _pressPoint = null;
        // Нажатие на кнопке карточки (значок, «Копировать», «Удалить») — не перетаскивание
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        if (sender is not FrameworkElement { Tag: Guid id }) return;
        _pressPoint = e.GetPosition(this);
        _pressId = id;
    }

    private void Card_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressPoint is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
        var now = e.GetPosition(this);
        if (!StripLayout.IsDragStarted(now.X - start.X, now.Y - start.Y,
                SystemParameters.MinimumHorizontalDragDistance, SystemParameters.MinimumVerticalDragDistance))
            return;
        _pressPoint = null;
        _suppressOpen = true;
        // Внутри ленты бросили — новый порядок (свой формат), в другое окно — файл снимка
        var data = new DataObject();
        data.SetData(CaptureIdFormat, _pressId.ToString());
        if (DragFileFor?.Invoke(_pressId) is { } path) data.SetData(DataFormats.FileDrop, new[] { path });
        _droppedOnStrip = false;
        var id = _pressId;
        var result = DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy | DragDropEffects.Move);
        if (result != DragDropEffects.None && !_droppedOnStrip) DroppedOutside?.Invoke(this, id);
    }

    private void CardsList_PreviewMouseMove(object sender, MouseEventArgs e)
        => Expand(StripLayout.HoverIndex(e.GetPosition(CardsList).Y, ViewModel.Cards.Count,
            StripViewModel.CardHeight, StripViewModel.StripeHeight));

    private void CardsList_MouseLeave(object sender, MouseEventArgs e) => Expand(-1);

    // Поднять над стопкой карточку index (-1 — ни одну); остальные — на своих местах
    private void Expand(int index)
    {
        for (var i = 0; i < CardsList.Items.Count; i++)
        {
            if (CardsList.ItemContainerGenerator.ContainerFromIndex(i) is UIElement container)
                Panel.SetZIndex(container, i == index ? 100 : 0);
        }
    }

    // Место броска своей карточки — только над списком; координаты от верха списка, прокрутка учтена
    private int? OwnDropSlot(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(CaptureIdFormat)) return null;
        var point = e.GetPosition(CardsList);
        return StripLayout.DropSlotInside(point.X, point.Y, CardsList.ActualWidth, CardsList.ActualHeight,
            ViewModel.Cards.Count, StripViewModel.CardHeight, StripViewModel.StripeHeight);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        // Своя карточка — перестановка, но только над списком; файлы из Проводника — импорт картинок
        var slot = OwnDropSlot(e);
        e.Effects = slot is not null ? DragDropEffects.Move
            : !e.Data.GetDataPresent(CaptureIdFormat) && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy
            : DragDropEffects.None;
        if (slot is { } s)
        {
            var top = StripLayout.SlotTop(s, ViewModel.Cards.Count, StripViewModel.CardHeight, StripViewModel.StripeHeight);
            DropLine.Margin = new Thickness(0, Math.Max(0, top - 1.5), 0, 0);
            DropLine.Visibility = Visibility.Visible;
        }
        else
        {
            DropLine.Visibility = Visibility.Collapsed;
        }
        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e) => DropLine.Visibility = Visibility.Collapsed;

    private void Window_Drop(object sender, DragEventArgs e)
    {
        DropLine.Visibility = Visibility.Collapsed;
        _droppedOnStrip = true;
        if (e.Data.GetData(CaptureIdFormat) is string text && Guid.TryParse(text, out var id))
        {
            // Бросок мимо списка (заголовок, кнопки ленты) порядок не меняет
            if (OwnDropSlot(e) is { } slot) ReorderRequested?.Invoke(this, (id, slot));
        }
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            FilesDropped?.Invoke(this, files);
        }
        e.Handled = true;
    }

    private static bool IsInsideButton(DependencyObject? element)
    {
        for (var e = element; e is not null; e = e is Visual or Visual3D ? VisualTreeHelper.GetParent(e) : LogicalTreeHelper.GetParent(e))
        {
            if (e is ButtonBase) return true;
        }
        return false;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid id }) DeleteCaptureRequested?.Invoke(this, id);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid id }) SaveCaptureRequested?.Invoke(this, id);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid id }) CopyCaptureRequested?.Invoke(this, id);
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        var topmost = new MenuItem { Header = "Поверх других окон", IsCheckable = true, IsChecked = ViewModel.IsTopmost };
        topmost.Click += (_, _) => TopmostToggled?.Invoke(this, topmost.IsChecked);

        var showStrip = new MenuItem { Header = "Показывать ленту", IsCheckable = true, IsChecked = ViewModel.ShowStripSetting };
        showStrip.Click += (_, _) => ShowStripToggled?.Invoke(this, showStrip.IsChecked);

        var menu = new ContextMenu { PlacementTarget = MoreButton, Placement = PlacementMode.Bottom };
        menu.Items.Add(topmost);
        menu.Items.Add(showStrip);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Импортировать файл…", ImportFileRequested));
        menu.Items.Add(Item("Вставить изображение из буфера", PasteImageRequested));
        menu.Items.Add(Item("Копировать пакет", CopyPackageRequested));
        menu.Items.Add(Item("Сохранить пакет…", SavePackageRequested));
        menu.Items.Add(Item("Очистить ленту", ClearRequested));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Выйти", ExitRequested));
        menu.IsOpen = true;
    }

    private MenuItem Item(string header, EventHandler? handler)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => handler?.Invoke(this, EventArgs.Empty);
        return item;
    }
}
