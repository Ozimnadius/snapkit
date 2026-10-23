using System.Runtime.ExceptionServices;

namespace Snapkit.App.Tests;

/// <summary>
/// WPF требует однопоточный апартамент (STA), а xUnit запускает тесты в MTA.
/// Выполняет тело теста в отдельном STA-потоке и пробрасывает исключение наружу.
/// </summary>
public static class StaRunner
{
    public static void Run(Action body)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
