using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GlobalTextHelper.Domain.Selection;
using GlobalTextHelper.Infrastructure.Clipboard;
using GlobalTextHelper.Infrastructure.Logging;

namespace GlobalTextHelper.Infrastructure.Selection;

/// <summary>
/// Listens for clipboard updates and emits captured text. Selection-based UIA hooks were removed due to instability.
/// </summary>
public sealed class SelectionWatcher : NativeWindow, IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    private readonly IClipboardService _clipboardService;
    private readonly ILogger _logger;
    private bool _disposed;

    public SelectionWatcher(IClipboardService clipboardService, ILogger logger)
    {
        _clipboardService = clipboardService;
        _logger = logger;

        CreateHandle(new CreateParams
        {
            Caption = "SelectionWatcher",
            X = -10000,
            Y = -10000,
            Width = 0,
            Height = 0
        });

        if (!AddClipboardFormatListener(Handle))
        {
            throw new InvalidOperationException("Det gǾr inte att registrera en urklippslyssnare.");
        }
    }

    public event EventHandler<SelectionCapturedEventArgs>? SelectionCaptured;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE)
        {
            HandleClipboardUpdate();
        }

        base.WndProc(ref m);
    }

    private void HandleClipboardUpdate()
    {
        if (_clipboardService.IsReplacingSelection || _clipboardService.IsReadingSelection)
        {
            return;
        }

        try
        {
            if (System.Windows.Forms.Clipboard.ContainsText())
            {
                string text = System.Windows.Forms.Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var hwnd = GetForegroundWindow();
                    SelectionCaptured?.Invoke(
                        this,
                        new SelectionCapturedEventArgs(text, hwnd, SelectionSource.Clipboard, DateTime.UtcNow));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Det gick inte att l��sa frǾn urklippet", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (Handle != IntPtr.Zero)
        {
            RemoveClipboardFormatListener(Handle);
            DestroyHandle();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
