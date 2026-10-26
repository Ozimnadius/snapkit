using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Snapkit.App.Shell;

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
        if (sender is FrameworkElement { Tag: Guid id }) OpenCaptureRequested?.Invoke(this, id);
    }

    private void Badge_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid id }) ToggleSentRequested?.Invoke(this, id);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid id }) DeleteCaptureRequested?.Invoke(this, id);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid id }) CopyCaptureRequested?.Invoke(this, id);
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        var topmost = new MenuItem { Header = "Поверх других окон", IsCheckable = true, IsChecked = ViewModel.IsTopmost };
        topmost.Click += (_, _) => TopmostToggled?.Invoke(this, topmost.IsChecked);

        var menu = new ContextMenu { PlacementTarget = MoreButton, Placement = PlacementMode.Bottom };
        menu.Items.Add(topmost);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Вставить изображение из буфера", PasteImageRequested));
        menu.Items.Add(Item("Копировать пакет", CopyPackageRequested));
        var resendLast = Item("Повторить последнюю вставку", ResendLastRequested);
        resendLast.IsEnabled = ViewModel.CanResendLast;
        menu.Items.Add(resendLast);
        var resendAll = Item("Вся лента в пачку", ResendAllRequested);
        resendAll.IsEnabled = !ViewModel.IsEmpty;
        menu.Items.Add(resendAll);
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
