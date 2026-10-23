using System.Windows;
using Forms = System.Windows.Forms;

namespace Snapkit.App;

public partial class App : Application
{
    private Forms.NotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Выйти", null, (_, _) => Shutdown());

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "Snapkit",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.OnExit(e);
    }

    // Значок программы лежит в ресурсах сборки (Assets/snapkit.ico).
    private static System.Drawing.Icon LoadAppIcon()
    {
        var info = GetResourceStream(new Uri("pack://application:,,,/Assets/snapkit.ico"))
            ?? throw new InvalidOperationException("Не найден значок программы в ресурсах.");
        using var stream = info.Stream;
        return new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }
}
