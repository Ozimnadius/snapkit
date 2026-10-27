using System.Windows;

namespace Snapkit.App.Notifications;

/// <summary>Уведомления программы. Новое уведомление заменяет предыдущее.</summary>
public static class Toasts
{
    private static readonly TimeSpan PlainDuration = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan ActionDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OfferDuration = TimeSpan.FromSeconds(15);
    private static ToastWindow? _window;

    /// <summary>Настройка «Показывать уведомления». Ошибки показываются всегда.</summary>
    public static bool Enabled { get; set; } = true;

    public static void Show(string message, string? actionText = null, Action? onAction = null)
        => Display(message, actionText, onAction, force: false);

    /// <summary>Спрятать уведомление сразу (перед снимком экрана).</summary>
    public static void HideNow() => _window?.Hide();

    /// <summary>Сообщение об ошибке — показывается даже при выключенных уведомлениях.</summary>
    public static void Error(string message) => Display(message, null, null, force: true);

    /// <summary>Предложение с кнопкой (обновление) — показывается и при выключенных уведомлениях, 15 с.</summary>
    public static void Offer(string message, string actionText, Action onAction) => Display(message, actionText, onAction, force: true, OfferDuration);

    private static void Display(string message, string? actionText, Action? onAction, bool force, TimeSpan? duration = null)
    {
        if (!Enabled && !force) return;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => Display(message, actionText, onAction, force, duration));
            return;
        }
        _window ??= new ToastWindow();
        _window.ShowMessage(message, actionText, onAction, duration ?? (actionText is null ? PlainDuration : ActionDuration));
    }
}
