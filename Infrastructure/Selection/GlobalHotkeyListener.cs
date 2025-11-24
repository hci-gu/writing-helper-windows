using System;
using System.Windows.Forms;
using GlobalTextHelper.Infrastructure.Logging;

namespace GlobalTextHelper.Infrastructure.Selection;

internal sealed class GlobalHotkeyListener : NativeWindow, IDisposable
{
    private const int HOTKEY_ID = 1;
    private const NativeMethods.Modifiers HOTKEY_MODS = NativeMethods.Modifiers.Control | NativeMethods.Modifiers.Shift | NativeMethods.Modifiers.NoRepeat;
    private const uint HOTKEY_VK = 0x20; // VK_SPACE

    private readonly ILogger _logger;
    private bool _disposed;

    public GlobalHotkeyListener(ILogger logger)
    {
        _logger = logger;
        CreateHandle(new CreateParams
        {
            Caption = "GlobalHotkeyListener",
            X = -10000,
            Y = -10000,
            Width = 0,
            Height = 0
        });

        if (!NativeMethods.RegisterHotKey(Handle, HOTKEY_ID, HOTKEY_MODS, HOTKEY_VK))
        {
            _logger.LogError("Det gick inte att registrera kortkommandot Ctrl+Shift+Space.");
        }
    }

    public event EventHandler? HotkeyPressed;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        NativeMethods.UnregisterHotKey(Handle, HOTKEY_ID);
        DestroyHandle();
    }
}
