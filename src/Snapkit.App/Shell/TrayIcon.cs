using Forms = System.Windows.Forms;

namespace Snapkit.App.Shell;

/// <summary>Значок у часов: клик показывает или прячет ленту, правый клик — меню.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public TrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Новый снимок", null, (_, _) => NewCaptureRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Показать ленту", null, (_, _) => ShowStripRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Выйти", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _icon = new Forms.NotifyIcon
        {
            Icon = AppIcon.TrayIcon(Forms.SystemInformation.SmallIconSize),
            Text = "Snapkit",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) ToggleStripRequested?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? NewCaptureRequested;
    public event EventHandler? ShowStripRequested;
    public event EventHandler? ToggleStripRequested;
    public event EventHandler? ExitRequested;

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
