using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GlobalTextHelper.Infrastructure.Clipboard;

public interface IClipboardService
{
    bool IsReadingSelection { get; }
    bool IsReplacingSelection { get; }
    Task<string?> CaptureSelectionAsync(IntPtr sourceWindow, CancellationToken cancellationToken);
    Task ReplaceSelectionAsync(string originalText, string replacementText, IntPtr targetWindow, CancellationToken cancellationToken);
    Task CopyToClipboardAsync(string text, CancellationToken cancellationToken);
}

public sealed class ClipboardService : IClipboardService
{
    private const int ClipboardSuppressionDelayMs = 200;
    private volatile bool _isReadingSelection;
    private volatile bool _isReplacingSelection;
    private int _clipboardUpdateDepth;

    public bool IsReadingSelection => _isReadingSelection;
    public bool IsReplacingSelection => _isReplacingSelection;

    public Task<string?> CaptureSelectionAsync(IntPtr sourceWindow, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CaptureSelectionInternal(sourceWindow));
    }

    public async Task ReplaceSelectionAsync(string originalText, string replacementText, IntPtr targetWindow, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await ReplaceSelectionInternalAsync(originalText, replacementText, targetWindow, cancellationToken);
    }

    public Task CopyToClipboardAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CopyToClipboardInternal(text);
        return Task.CompletedTask;
    }

    private string? CaptureSelectionInternal(IntPtr sourceWindow)
    {
        if (_isReadingSelection)
        {
            return null;
        }

        var snapshot = TryGetClipboardSnapshot();
        if (snapshot is null)
        {
            return null;
        }

        try
        {
            _isReadingSelection = true;

            if (sourceWindow != IntPtr.Zero)
            {
                SetForegroundWindow(sourceWindow);
            }

            SendCtrlShortcut(Keys.C);

            if (System.Windows.Forms.Clipboard.ContainsText())
            {
                var text = System.Windows.Forms.Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }
        catch (ExternalException)
        {
            // Clipboard busy. Allow fallback to clipboard listener.
        }
        finally
        {
            RestoreClipboardSnapshot(snapshot);
            _isReadingSelection = false;
        }

        return null;
    }

    private async Task ReplaceSelectionInternalAsync(string originalText, string replacementText, IntPtr targetWindow, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(replacementText))
        {
            throw new InvalidOperationException("Ersättningstexten får inte vara tom.");
        }

        // 1. Prepare Clipboard
        BeginClipboardUpdate();
        IDataObject? snapshot = null;
        try
        {
            snapshot = System.Windows.Forms.Clipboard.GetDataObject();
        }
        catch { /* Best effort */ }

        try
        {
            System.Windows.Forms.Clipboard.SetText(replacementText);
            // Ensure clipboard update propagates
            await Task.Delay(100, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Det gick inte att skriva till urklippet: " + ex.Message);
        }

        // 2. Try Direct Message Paste (Most Reliable)
        // We attempt to find the specific child control that has focus within the target application
        // and send it a WM_PASTE message directly.
        bool pasteSuccess = false;
        if (targetWindow != IntPtr.Zero)
        {
            pasteSuccess = TryPasteViaMessage(targetWindow);
        }

        // 3. Fallback to Input Simulation (Ctrl+V)
        if (!pasteSuccess)
        {
            if (targetWindow != IntPtr.Zero)
            {
                // Try to bring the window to the foreground
                var rootWindow = GetAncestor(targetWindow, GA_ROOT);
                SetForegroundWindow(rootWindow != IntPtr.Zero ? rootWindow : targetWindow);
                
                // Wait for focus to settle
                await Task.Delay(250, cancellationToken);
            }

            // Send Ctrl+V
            await SendCtrlShortcutAsync(Keys.V, cancellationToken);
            
            // Wait for the application to process the input
            await Task.Delay(500, cancellationToken);
        }
        else
        {
            // If we pasted via message, it's usually synchronous, but give it a tiny moment just in case
            await Task.Delay(50, cancellationToken);
        }

        // 4. Restore Clipboard
        try
        {
            if (snapshot is not null)
            {
                System.Windows.Forms.Clipboard.SetDataObject(snapshot);
            }
            else if (!string.IsNullOrWhiteSpace(originalText))
            {
                System.Windows.Forms.Clipboard.SetText(originalText);
            }
        }
        catch { /* Best effort */ }
        finally
        {
            ScheduleClipboardUpdateRelease();
        }
    }

    private bool TryPasteViaMessage(IntPtr mainHandle)
    {
        try
        {
            // 1. Try to find the actual focused control within that thread
            var targetControl = GetFocusedControlHandle(mainHandle);
            if (targetControl == IntPtr.Zero)
            {
                // Fallback: try the main handle itself (unlikely to work for complex apps like WordPad, but good for simple ones)
                targetControl = mainHandle;
            }

            // 2. Check if it's a valid target for WM_PASTE
            var className = GetWindowClassName(targetControl);
            if (IsPasteSupportedClass(className))
            {
                SendMessage(targetControl, WM_PASTE, 0, 0);
                return true;
            }
        }
        catch
        {
            // Ignore errors and fall back to Ctrl+V
        }
        return false;
    }

    private IntPtr GetFocusedControlHandle(IntPtr windowHandle)
    {
        try
        {
            uint threadId = GetWindowThreadProcessId(windowHandle, out _);
            if (threadId == 0) return IntPtr.Zero;

            var guiInfo = new GUITHREADINFO();
            guiInfo.cbSize = Marshal.SizeOf(guiInfo);

            if (GetGUIThreadInfo(threadId, ref guiInfo))
            {
                // hwndFocus is the window that has keyboard focus
                if (guiInfo.hwndFocus != IntPtr.Zero)
                    return guiInfo.hwndFocus;
            }
        }
        catch { /* Best effort */ }
        
        return IntPtr.Zero;
    }

    private static bool IsPasteSupportedClass(string className)
    {
        if (string.IsNullOrEmpty(className)) return false;
        
        return className.Equals("Edit", StringComparison.OrdinalIgnoreCase) ||
               className.Contains("RichEdit", StringComparison.OrdinalIgnoreCase) ||
               className.Equals("TextBox", StringComparison.OrdinalIgnoreCase); // Some .NET apps
    }

    private static string GetWindowClassName(IntPtr hwnd)
    {
        var builder = new System.Text.StringBuilder(256);
        return GetClassName(hwnd, builder, builder.Capacity) == 0
            ? string.Empty
            : builder.ToString();
    }

    private void CopyToClipboardInternal(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Urklippstexten får inte vara tom.");
        }

        BeginClipboardUpdate();
        try
        {
            System.Windows.Forms.Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Det gick inte att skriva till urklippet: " + ex.Message);
        }
        finally
        {
            ScheduleClipboardUpdateRelease();
        }
    }

    private void BeginClipboardUpdate()
    {
        Interlocked.Increment(ref _clipboardUpdateDepth);
        _isReplacingSelection = true;
    }

    private void ScheduleClipboardUpdateRelease()
    {
        Task.Run(async () =>
        {
            await Task.Delay(ClipboardSuppressionDelayMs).ConfigureAwait(false);
            if (Interlocked.Decrement(ref _clipboardUpdateDepth) <= 0)
            {
                _clipboardUpdateDepth = 0;
                _isReplacingSelection = false;
            }
        });
    }

    private static IDataObject? TryGetClipboardSnapshot()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return System.Windows.Forms.Clipboard.GetDataObject();
            }
            catch (ExternalException)
            {
                Thread.Sleep(10);
            }
        }

        return null;
    }

    private static void RestoreClipboardSnapshot(IDataObject? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        try
        {
            System.Windows.Forms.Clipboard.SetDataObject(snapshot);
        }
        catch (ExternalException)
        {
            // Best effort.
        }
    }

    private static void SendCtrlShortcut(Keys key)
    {
        var inputs = new INPUT[4];
        inputs[0] = CreateKeyInput(Keys.ControlKey, false);
        inputs[1] = CreateKeyInput(key, false);
        inputs[2] = CreateKeyInput(key, true);
        inputs[3] = CreateKeyInput(Keys.ControlKey, true);

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static async Task SendCtrlShortcutAsync(Keys key, CancellationToken cancellationToken)
    {
        // Ctrl Down
        var ctrlDown = new INPUT[] { CreateKeyInput(Keys.ControlKey, false) };
        SendInput(1, ctrlDown, Marshal.SizeOf<INPUT>());
        await Task.Delay(50, cancellationToken);

        // Key Down
        var keyDown = new INPUT[] { CreateKeyInput(key, false) };
        SendInput(1, keyDown, Marshal.SizeOf<INPUT>());
        await Task.Delay(50, cancellationToken);

        // Key Up
        var keyUp = new INPUT[] { CreateKeyInput(key, true) };
        SendInput(1, keyUp, Marshal.SizeOf<INPUT>());
        await Task.Delay(50, cancellationToken);

        // Ctrl Up
        var ctrlUp = new INPUT[] { CreateKeyInput(Keys.ControlKey, true) };
        SendInput(1, ctrlUp, Marshal.SizeOf<INPUT>());
        await Task.Delay(50, cancellationToken);
    }

    private static INPUT CreateKeyInput(Keys key, bool keyUp)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)key,
                    dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    private const uint GA_ROOT = 2;
    private const int WM_PASTE = 0x0302;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public System.Drawing.Rectangle rcCaret;
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
