using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using GlobalTextHelper.Infrastructure.Logging;

namespace GlobalTextHelper.Infrastructure.Selection;

internal sealed class HotkeySelectionService
{
    private readonly ILogger _logger;

    public HotkeySelectionService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<HotkeySelectionResult?> CaptureSelectionAsync()
    {
        var targetWindow = NativeMethods.GetForegroundWindow();
        if (targetWindow == IntPtr.Zero)
        {
            return null;
        }

        uint oldSeq = NativeMethods.GetClipboardSequenceNumber();

        NativeMethods.SetForegroundWindow(targetWindow);
        InputHelper.SendCtrlC();

        bool changed = await WaitForClipboardChangeAsync(oldSeq, 300);
        if (!changed)
        {
            return null;
        }

        try
        {
            if (!Clipboard.ContainsText())
            {
                return null;
            }

            var text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return new HotkeySelectionResult(text, targetWindow);
        }
        catch (Exception ex)
        {
            _logger.LogError("Det gick inte att läsa text från urklippet efter genvägen.", ex);
            return null;
        }
    }

    private static async Task<bool> WaitForClipboardChangeAsync(uint oldSeq, int timeoutMs)
    {
        var start = Environment.TickCount;
        while (Environment.TickCount - start < timeoutMs)
        {
            uint current = NativeMethods.GetClipboardSequenceNumber();
            if (current != oldSeq)
            {
                return true;
            }

            await Task.Delay(10);
        }

        return false;
    }
}

internal readonly record struct HotkeySelectionResult(string Text, IntPtr SourceWindow);
