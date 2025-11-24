using System;
using System.Drawing;
using System.Windows.Forms;
using GlobalTextHelper.Infrastructure.App;

namespace GlobalTextHelper.UI;

public sealed class MainForm : Form
{
    private const int HOTKEY_ID = 1;
    private const NativeMethods.Modifiers HOTKEY_MODS =
        NativeMethods.Modifiers.Control | NativeMethods.Modifiers.Shift | NativeMethods.Modifiers.NoRepeat;
    private const uint HOTKEY_VK = 0x20; // VK_SPACE

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
    public event EventHandler? GlobalHotkeyPressed;

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

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (!NativeMethods.RegisterHotKey(Handle, HOTKEY_ID, HOTKEY_MODS, HOTKEY_VK))
        {
            // Hotkey registration failed; continue without the global shortcut.
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        NativeMethods.UnregisterHotKey(Handle, HOTKEY_ID);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            GlobalHotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        base.WndProc(ref m);
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
