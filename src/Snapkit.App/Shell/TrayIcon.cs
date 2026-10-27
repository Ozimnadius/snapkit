using Forms = System.Windows.Forms;

namespace Snapkit.App.Shell;

/// <summary>Значок у часов: клик показывает или прячет ленту, правый клик — меню.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _showStrip;

    public TrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Новый снимок", null, (_, _) => NewCaptureRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Показать ленту", null, (_, _) => ShowStripRequested?.Invoke(this, EventArgs.Empty));
        _showStrip = new Forms.ToolStripMenuItem("Показывать ленту") { CheckOnClick = true };
        _showStrip.Click += (_, _) => ShowStripToggled?.Invoke(this, _showStrip.Checked);
        menu.Items.Add(_showStrip);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Настройки…", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
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
    public event EventHandler? SettingsRequested;

    /// <summary>Галочка «Показывать ленту» в меню значка.</summary>
    public event EventHandler<bool>? ShowStripToggled;

    public bool ShowStripChecked
    {
        get => _showStrip.Checked;
        set => _showStrip.Checked = value;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
