using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using GlobalTextHelper.Domain.Selection;
using SelectionRange = GlobalTextHelper.Domain.Selection.SelectionRange;
using GlobalTextHelper.Infrastructure.Clipboard;
using GlobalTextHelper.Infrastructure.Logging;

namespace GlobalTextHelper.Infrastructure.Selection;

public sealed class SelectionWatcher : NativeWindow, IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private const uint EVENT_OBJECT_TEXTSELECTIONCHANGED = 0x8014;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private const int EM_GETSEL = 0x00B0;

    private readonly IClipboardService _clipboardService;
    private readonly ILogger _logger;
    private readonly ISynchronizeInvoke _dispatcher;
    private WinEventDelegate? _winEventDelegate;
    private IntPtr _winEventHook = IntPtr.Zero;
    private bool _disposed;
    private SelectionRange? _lastSelectionRange;
    private IntPtr _lastSelectionWindow;
    private DateTime _lastSelectionTimestamp;

    public SelectionWatcher(IClipboardService clipboardService, ILogger logger, ISynchronizeInvoke dispatcher)
    {
        _clipboardService = clipboardService;
        _logger = logger;
        _dispatcher = dispatcher;
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
            throw new InvalidOperationException("Unable to register clipboard listener.");
        }

        _winEventDelegate = OnWinEvent;
        _winEventHook = SetWinEventHook(
            EVENT_OBJECT_TEXTSELECTIONCHANGED,
            EVENT_OBJECT_TEXTSELECTIONCHANGED,
            IntPtr.Zero,
            _winEventDelegate,
            0,
            0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        if (_winEventHook == IntPtr.Zero)
        {
            throw new InvalidOperationException("Unable to subscribe to text selection events.");
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
                    var selectionRange = TryGetLastSelectionRange(hwnd);
                    SelectionCaptured?.Invoke(
                        this,
                        new SelectionCapturedEventArgs(text, hwnd, SelectionSource.Clipboard, DateTime.UtcNow, selectionRange));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to read clipboard", ex);
        }
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType != EVENT_OBJECT_TEXTSELECTIONCHANGED)
        {
            return;
        }

        if (_clipboardService.IsReplacingSelection)
        {
            return;
        }

        if (_dispatcher.InvokeRequired)
        {
            _dispatcher.BeginInvoke(new MethodInvoker(() => HandleSelectionCapture(hwnd)), Array.Empty<object?>());
        }
        else
        {
            HandleSelectionCapture(hwnd);
        }
    }

    private void HandleSelectionCapture(IntPtr hwnd)
    {
        if (!ShouldCaptureSelection(hwnd, out var selectionRange))
        {
            return;
        }

        RememberSelection(hwnd, selectionRange);

        var task = _clipboardService.CaptureSelectionAsync(hwnd, CancellationToken.None);
        string? text = task.GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        SelectionCaptured?.Invoke(
            this,
            new SelectionCapturedEventArgs(text, hwnd, SelectionSource.TextSelection, DateTime.UtcNow, selectionRange));
    }

    private void RememberSelection(IntPtr hwnd, SelectionRange? selectionRange)
    {
        if (selectionRange is null)
        {
            return;
        }

        _lastSelectionWindow = hwnd;
        _lastSelectionRange = selectionRange;
        _lastSelectionTimestamp = DateTime.UtcNow;
    }

    private SelectionRange? TryGetLastSelectionRange(IntPtr hwnd)
    {
        if (_lastSelectionRange is null || hwnd == IntPtr.Zero)
        {
            return null;
        }

        if (hwnd != _lastSelectionWindow)
        {
            return null;
        }

        if ((DateTime.UtcNow - _lastSelectionTimestamp) > TimeSpan.FromSeconds(5))
        {
            return null;
        }

        return _lastSelectionRange;
    }

    private static bool ShouldCaptureSelection(IntPtr hwnd, out SelectionRange? selectionRange)
    {
        selectionRange = null;
        if (hwnd == IntPtr.Zero)
        {
            return true;
        }

        var className = GetWindowClassName(hwnd);
        if (string.IsNullOrEmpty(className))
        {
            return true;
        }

        if (!IsRichEditClass(className) &&
            !string.Equals(className, "Edit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            SendMessage(hwnd, EM_GETSEL, out int start, out int end);
            if (start == end)
            {
                return false;
            }

            selectionRange = new SelectionRange(start, end);
            return true;
        }
        catch
        {
            return true;
        }
    }

    private static string GetWindowClassName(IntPtr hwnd)
    {
        var builder = new StringBuilder(256);
        return GetClassName(hwnd, builder, builder.Capacity) == 0
            ? string.Empty
            : builder.ToString();
    }

    private static bool IsRichEditClass(string className)
    {
        return className.StartsWith("RICHEDIT", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_winEventHook != IntPtr.Zero)
        {
            UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }

        if (Handle != IntPtr.Zero)
        {
            RemoveClipboardFormatListener(Handle);
            DestroyHandle();
        }
    }

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, out int wParam, out int lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
}
