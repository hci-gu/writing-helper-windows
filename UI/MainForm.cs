using System;
using System.Drawing;
using System.Windows.Forms;

namespace GlobalTextHelper.UI;

public sealed class MainForm : Form
{
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _menu;

    public MainForm()
    {
        Text = "GlobalTextHelper";
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-2000, -2000);
        FormBorderStyle = FormBorderStyle.FixedToolWindow;

        _menu = new ContextMenuStrip();
        _menu.Items.Add("Settings…", null, (_, __) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit", null, (_, __) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "GlobalTextHelper",
            ContextMenuStrip = _menu
        };
    }

    public event EventHandler? ExitRequested;
    public event EventHandler? SettingsRequested;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Hide();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Dispose();
            _menu.Dispose();
        }

        base.Dispose(disposing);
    }
}
