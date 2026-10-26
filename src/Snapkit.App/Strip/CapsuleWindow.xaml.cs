using System.Windows;
using System.Windows.Input;

namespace Snapkit.App.Strip;

/// <summary>
/// Капсула — свёрнутая лента: кнопка нового снимка, счётчик неотправленных и «развернуть».
/// Окно только сообщает о действиях; что делать, решает App.
/// </summary>
public partial class CapsuleWindow : Window
{
    public CapsuleWindow(StripViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public event EventHandler? NewCaptureRequested;
    public event EventHandler? ExpandRequested;
    public event EventHandler? Moved;

    private void Capsule_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        DragMove();
        Moved?.Invoke(this, EventArgs.Empty);
    }

    private void NewCapture_Click(object sender, RoutedEventArgs e) => NewCaptureRequested?.Invoke(this, EventArgs.Empty);

    private void Expand_Click(object sender, RoutedEventArgs e) => ExpandRequested?.Invoke(this, EventArgs.Empty);
}
