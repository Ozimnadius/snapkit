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
    public event EventHandler<Guid>? ToggleSentRequested;
    public event EventHandler? ResendLastRequested;
    public event EventHandler? ResendAllRequested;
    public event EventHandler? Moved;
    public event EventHandler<(Guid Id, int Slot)>? ReorderRequested;
    public event EventHandler<Guid>? SaveCaptureRequested;
    public event EventHandler? SavePackageRequested;
    public event EventHandler? ImportFileRequested;
    public event EventHandler<string[]>? FilesDropped;
    public event EventHandler<bool>? ShowStripToggled;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        DragMove();
        Moved?.Invoke(this, EventArgs.Empty);
    }

    private void NewCapture_Click(object sender, RoutedEventArgs e) => NewCaptureRequested?.Invoke(this, EventArgs.Empty);

    private void Clear_Click(object sender, RoutedEventArgs e) => ClearRequested?.Invoke(this, EventArgs.Empty);

    private void Minimize_Click(object sender, RoutedEventArgs e) => Hide();

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
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy | DragDropEffects.Move);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        // Своя карточка — перестановка; файлы из Проводника — импорт картинок
        e.Effects = e.Data.GetDataPresent(CaptureIdFormat) ? DragDropEffects.Move
            : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(CaptureIdFormat) is string text && Guid.TryParse(text, out var id))
        {
            // Координата — от верха списка карточек, прокрутка уже учтена
            var slot = StripLayout.DropSlot(e.GetPosition(CardsList).Y, ViewModel.Cards.Count,
                StripViewModel.CardHeight, StripViewModel.StripeHeight);
            ReorderRequested?.Invoke(this, (id, slot));
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

    private void Badge_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid id }) ToggleSentRequested?.Invoke(this, id);
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
        var resendLast = Item("Повторить последнюю вставку", ResendLastRequested);
        resendLast.IsEnabled = ViewModel.CanResendLast;
        menu.Items.Add(resendLast);
        var resendAll = Item("Вся лента в пачку", ResendAllRequested);
        resendAll.IsEnabled = !ViewModel.IsEmpty;
        menu.Items.Add(resendAll);
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
