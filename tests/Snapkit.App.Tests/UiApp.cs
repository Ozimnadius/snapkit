using System.Windows;
using System.Windows.Threading;

namespace Snapkit.App.Tests;

/// <summary>
/// Элементы интерфейса программы (панель инструментов и т. п.) берут кисти и стили из темы Theme.xaml.
/// WPF допускает одно приложение на процесс, а ресурсы принадлежат его потоку, поэтому тестовое приложение
/// с темой запускается один раз на своём STA-потоке, и тела таких тестов выполняются на нём.
/// Это не App: настоящая программа (трей, перехват клавиш) не запускается.
/// </summary>
public static class UiApp
{
    private static readonly Lazy<Dispatcher> Ui = new(Start);

    public static void Run(Action body) => Ui.Value.Invoke(body);

    private static Dispatcher Start()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Snapkit;component/Theme.xaml"),
            });
            app.Startup += (_, _) =>
            {
                dispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
            };
            app.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return dispatcher!;
    }
}
