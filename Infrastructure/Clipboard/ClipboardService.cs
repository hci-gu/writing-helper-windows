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

    public Task ReplaceSelectionAsync(string originalText, string replacementText, IntPtr targetWindow, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReplaceSelectionInternal(originalText, replacementText, targetWindow);
        return Task.CompletedTask;
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

    private void ReplaceSelectionInternal(string originalText, string replacementText, IntPtr targetWindow)
    {
        if (string.IsNullOrWhiteSpace(replacementText))
        {
            throw new InvalidOperationException("Replacement text cannot be empty.");
        }

        BeginClipboardUpdate();

        IDataObject? snapshot = null;
        try
        {
            snapshot = System.Windows.Forms.Clipboard.GetDataObject();
        }
        catch (Exception)
        {
            // Best effort snapshot.
        }

        try
        {
            System.Windows.Forms.Clipboard.SetText(replacementText);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Unable to set clipboard text: " + ex.Message);
        }

        FocusTargetWindow(targetWindow);
        SendCtrlShortcut(Keys.V);

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
        catch (Exception)
        {
            // Allow the system clipboard to recover naturally.
        }
        finally
        {
            ScheduleClipboardUpdateRelease();
        }
    }

    private void CopyToClipboardInternal(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Clipboard text cannot be empty.");
        }

        BeginClipboardUpdate();
        try
        {
            System.Windows.Forms.Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Unable to set clipboard text: " + ex.Message);
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

    private void FocusTargetWindow(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero)
        {
            return;
        }

        IntPtr root = GetAncestor(targetWindow, GA_ROOT);
        if (root == IntPtr.Zero)
        {
            root = targetWindow;
        }

        uint currentThread = GetCurrentThreadId();
        uint targetThread = GetWindowThreadProcessId(targetWindow, out _);
        bool attached = false;

        try
        {
            if (targetThread != 0 && currentThread != targetThread)
            {
                attached = AttachThreadInput(currentThread, targetThread, true);
            }

            SetForegroundWindow(root);
            SetFocus(targetWindow);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(currentThread, targetThread, false);
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint GA_ROOT = 2;

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
