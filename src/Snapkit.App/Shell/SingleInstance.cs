namespace Snapkit.App.Shell;

/// <summary>
/// Работает только одна копия программы. Повторный запуск не открывает вторую копию,
/// а просит первую показать ленту.
/// </summary>
public static class SingleInstance
{
    private const string MutexName = @"Local\Snapkit.SingleInstance";
    private const string ShowEventName = @"Local\Snapkit.ShowStrip";

    // Держим ссылки, чтобы сборщик мусора не освободил мьютекс и событие.
    private static Mutex? _mutex;
    private static EventWaitHandle? _showEvent;

    /// <summary>
    /// true — это первая копия; onSecondInstance вызывается с фонового потока при каждом повторном запуске.
    /// false — копия уже работает, ей отправлена просьба показать ленту.
    /// </summary>
    public static bool TryAcquire(Action onSecondInstance)
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
            {
                existing.Set();
                existing.Dispose();
            }
            return false;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = new Thread(() =>
        {
            while (_showEvent.WaitOne()) onSecondInstance();
        })
        { IsBackground = true, Name = "Snapkit.SingleInstance" };
        listener.Start();
        return true;
    }
}
