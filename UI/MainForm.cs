using System;
using System.Drawing;
using System.Windows.Forms;

namespace GlobalTextHelper.UI;

public sealed class MainForm : Form
{
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _autoShowItem;

    public MainForm()
    {
        Text = "GlobalTextHelper";
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-2000, -2000);
        FormBorderStyle = FormBorderStyle.FixedToolWindow;

        _menu = new ContextMenuStrip();
        _autoShowItem = new ToolStripMenuItem("Visa automatiskt vid markering");
        _autoShowItem.CheckOnClick = true;
        _autoShowItem.Click += (_, __) => AutoShowOnSelectionChanged?.Invoke(this, EventArgs.Empty);
        _menu.Items.Add(_autoShowItem);
        _menu.Items.Add(new ToolStripSeparator());

        _menu.Items.Add("Öppna redigeraren…", null, (_, __) => EditorRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add("Inställningar…", null, (_, __) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Avsluta", null, (_, __) => ExitRequested?.Invoke(this, EventArgs.Empty));

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
    public event EventHandler? EditorRequested;
    public event EventHandler? AutoShowOnSelectionChanged;

    public bool AutoShowOnSelection
    {
        get => _autoShowItem.Checked;
        set => _autoShowItem.Checked = value;
    }

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
