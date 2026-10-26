namespace Snapkit.Core.Session;

/// <summary>Что видно на экране: развёрнутая лента, капсула (свёрнутая лента) или ничего.</summary>
public enum StripView { Hidden, Strip, Capsule }

/// <summary>
/// Когда показывать ленту и капсулу — по настройке «Показывать ленту» и тому, свёрнута ли лента
/// (описание этапа 3, разделы 2.8 и 2.11).
/// </summary>
public static class StripPresence
{
    public static StripView OnStartup(bool showStrip, bool collapsed)
        => !showStrip ? StripView.Hidden : collapsed ? StripView.Capsule : StripView.Strip;

    /// <summary>
    /// Новый снимок: при включённой ленте капсула остаётся капсулой (растёт счётчик), а закрытая лента
    /// появляется; при выключенной всё остаётся как есть — о снимке скажет уведомление.
    /// </summary>
    public static StripView AfterNewCapture(StripView current, bool showStrip)
        => !showStrip ? current : current == StripView.Capsule ? StripView.Capsule : StripView.Strip;

    /// <summary>«Свернуть»: при включённой ленте — капсула, при выключенной лента просто прячется.</summary>
    public static StripView OnMinimize(bool showStrip) => showStrip ? StripView.Capsule : StripView.Hidden;

    public static StripView OnShowStripChanged(bool showStrip, bool collapsed) => OnStartup(showStrip, collapsed);
}
