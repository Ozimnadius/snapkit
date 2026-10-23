using System.Windows;
using System.Windows.Threading;
using Snapkit.App.Interop;

namespace Snapkit.App.Notifications;

/// <summary>
/// Короткое уведомление у правого нижнего угла экрана. Не забирает фокус —
/// это важно во время вставки в чат.
/// </summary>
public partial class ToastWindow : Window
{
    private const double EdgeMargin = 16;
    private readonly DispatcherTimer _timer = new();
    private Action? _action;

    public ToastWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => this.MakeNoActivate();
        SizeChanged += (_, _) => PlaceInCorner();
        _timer.Tick += (_, _) => { _timer.Stop(); Hide(); };
    }

    public void ShowMessage(string message, string? actionText, Action? action, TimeSpan duration)
    {
        MessageText.Text = message;
        _action = action;
        ActionButton.Content = actionText;
        ActionButton.Visibility = actionText is null ? Visibility.Collapsed : Visibility.Visible;

        if (!IsVisible) Show();
        PlaceInCorner();
        _timer.Stop();
        _timer.Interval = duration;
        _timer.Start();
    }

    private void PlaceInCorner()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - EdgeMargin;
        Top = area.Bottom - ActualHeight - EdgeMargin;
    }

    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        Hide();
        _action?.Invoke();
    }
}
