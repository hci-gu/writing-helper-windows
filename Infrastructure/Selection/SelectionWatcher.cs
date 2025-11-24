using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using GlobalTextHelper.Domain.Selection;
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
    private static readonly HashSet<string> UiaExcludedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "wordpad",
        "write"
    };

    private readonly IClipboardService _clipboardService;
    private readonly ILogger _logger;
    private readonly ISynchronizeInvoke _dispatcher;
    private readonly Func<bool> _isAutoShowEnabled;
    private readonly Func<int> _getMinSelectionLength;
    private WinEventDelegate? _winEventDelegate;
    private IntPtr _winEventHook = IntPtr.Zero;
    private bool _disposed;

    public SelectionWatcher(IClipboardService clipboardService, ILogger logger, ISynchronizeInvoke dispatcher, Func<bool> isAutoShowEnabled, Func<int> getMinSelectionLength)
    {
        _clipboardService = clipboardService;
        _logger = logger;
        _dispatcher = dispatcher;
        _isAutoShowEnabled = isAutoShowEnabled;
        _getMinSelectionLength = getMinSelectionLength;
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
            throw new InvalidOperationException("Det går inte att registrera en urklippslyssnare.");
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
            throw new InvalidOperationException("Det går inte att prenumerera på händelser för textmarkeringar.");
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
            _logger.LogError("Det gick inte att läsa från urklippet", ex);
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

        if (!_isAutoShowEnabled())
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
        if (!HasNonEmptySelection(hwnd))
        {
            return;
        }

        int minSelectionLength = _getMinSelectionLength();
        string? uiaText = null;
        string? processName = null;

        try
        {
            if (ShouldSkipUiaRead(hwnd, out processName))
            {
                _logger.LogInformation($"Skipping UIA selection read for '{processName}' due to unstable provider. Falling back to clipboard.");
            }
            else
            {
                uiaText = TryReadSelectedTextViaUIA(hwnd);
                if (!string.IsNullOrWhiteSpace(uiaText))
                {
                    string trimmed = uiaText.Trim();
                    if (trimmed.Length >= minSelectionLength)
                    {
                        SelectionCaptured?.Invoke(
                            this,
                            new SelectionCapturedEventArgs(trimmed, hwnd, SelectionSource.TextSelection, DateTime.UtcNow));
                    }
                    return;
                }
            }
        }
        catch (AccessViolationException)
        {
            processName ??= TryGetProcessName(hwnd) ?? "unknown";
            _logger.LogInformation($"UIA selection read caused AccessViolation for process '{processName}'. Falling back to clipboard.");
        }
        catch (Exception ex)
        {
            _logger.LogError("UIA selection read failed", ex);
        }

        // Fallback to Clipboard
        var task = _clipboardService.CaptureSelectionAsync(hwnd, CancellationToken.None);
        string? text = task.GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string trimmedText = text.Trim();
        if (trimmedText.Length >= minSelectionLength)
        {
            SelectionCaptured?.Invoke(
                this,
                new SelectionCapturedEventArgs(trimmedText, hwnd, SelectionSource.TextSelection, DateTime.UtcNow));
        }
    }

    [HandleProcessCorruptedStateExceptions]
    [SecurityCritical]
    private string? TryReadSelectedTextViaUIA(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return null;

        try
        {
            var element = AutomationElement.FromHandle(hwnd);
            if (element == null)
                return null;

            if (element.TryGetCurrentPattern(TextPattern.Pattern, out object patternObj))
            {
                var textPattern = (TextPattern)patternObj;
                var ranges = textPattern.GetSelection();
                if (ranges != null && ranges.Length > 0)
                {
                    var sb = new StringBuilder();
                    foreach (var range in ranges)
                    {
                        try
                        {
                            string text = range.GetText(-1);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                if (sb.Length > 0)
                                {
                                    sb.AppendLine();
                                }
                                sb.Append(text.Trim());
                            }
                        }
                        catch (AccessViolationException)
                        {
                            // Ignore memory corruption errors from UIA
                        }
                        catch (Exception)
                        {
                            // Ignore other errors from UIA
                        }
                    }

                    return sb.Length > 0 ? sb.ToString() : null;
                }
            }
        }
        catch (AccessViolationException)
        {
            // Ignore memory corruption errors from UIA
        }
        catch (Exception ex)
        {
            // UIA can be flaky, especially with some applications.
            // We log this as debug/warning rather than error to avoid noise.
            _logger.LogInformation($"UIA selection read warning: {ex.Message}");
        }

        return null;
    }

    private static bool ShouldSkipUiaRead(IntPtr hwnd, out string? processName)
    {
        processName = TryGetProcessName(hwnd);
        return processName is not null && UiaExcludedProcesses.Contains(processName);
    }

    private static bool HasNonEmptySelection(IntPtr hwnd)
    {
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
            return start != end;
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

    private static string? TryGetProcessName(IntPtr hwnd)
    {
        try
        {
            GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == 0)
            {
                return null;
            }

            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
