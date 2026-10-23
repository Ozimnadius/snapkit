using System.Windows;

namespace Snapkit.App.Notifications;

/// <summary>Уведомления программы. Новое уведомление заменяет предыдущее.</summary>
public static class Toasts
{
    private static readonly TimeSpan PlainDuration = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan ActionDuration = TimeSpan.FromSeconds(5);
    private static ToastWindow? _window;

    /// <summary>Настройка «Показывать уведомления». Ошибки показываются всегда.</summary>
    public static bool Enabled { get; set; } = true;

    public static void Show(string message, string? actionText = null, Action? onAction = null)
        => Display(message, actionText, onAction, force: false);

    /// <summary>Сообщение об ошибке — показывается даже при выключенных уведомлениях.</summary>
    public static void Error(string message) => Display(message, null, null, force: true);

    private static void Display(string message, string? actionText, Action? onAction, bool force)
    {
        if (!Enabled && !force) return;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => Display(message, actionText, onAction, force));
            return;
        }
        _window ??= new ToastWindow();
        _window.ShowMessage(message, actionText, onAction, actionText is null ? PlainDuration : ActionDuration);
    }
}
