namespace Snapkit.App.Shell;

/// <summary>
/// Работает только одна копия программы. Повторный запуск не открывает вторую копию,
/// а просит первую показать ленту. Установщик новой версии просит работающую копию закрыться.
/// </summary>
public static class SingleInstance
{
    private const string MutexName = @"Local\Snapkit.SingleInstance";
    private const string ShowEventName = @"Local\Snapkit.ShowStrip";
    private const string ExitEventName = @"Local\Snapkit.Exit";

    // Держим ссылки, чтобы сборщик мусора не освободил мьютекс и события.
    private static Mutex? _mutex;
    private static EventWaitHandle? _showEvent;
    private static EventWaitHandle? _exitEvent;

    /// <summary>
    /// true — это первая копия; onSecondInstance вызывается с фонового потока при каждом повторном запуске,
    /// onExitRequested — когда установщик новой версии просит закрыться.
    /// false — копия уже работает, ей отправлена просьба показать ленту.
    /// </summary>
    public static bool TryAcquire(Action onSecondInstance, Action onExitRequested)
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            Signal(ShowEventName);
            return false;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName);
        WaitHandle[] events = [_showEvent, _exitEvent];
        var listener = new Thread(() =>
        {
            while (true)
            {
                if (WaitHandle.WaitAny(events) == 0) onSecondInstance();
                else onExitRequested();
            }
        })
        { IsBackground = true, Name = "Snapkit.SingleInstance" };
        listener.Start();
        return true;
    }

    /// <summary>Попросить работающую копию закрыться (лента сохраняется на диске).</summary>
    public static void RequestExit() => Signal(ExitEventName);

    private static void Signal(string name)
    {
        if (!EventWaitHandle.TryOpenExisting(name, out var existing)) return;
        existing.Set();
        existing.Dispose();
    }
}
