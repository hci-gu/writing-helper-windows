using System;

namespace GlobalTextHelper.Domain.Selection;

public sealed class TextEditContext
{
    public IntPtr TargetWindow { get; }
    public string OriginalText { get; }

    public TextEditContext(IntPtr targetWindow, string originalText)
    {
        TargetWindow = targetWindow;
        OriginalText = originalText;
    }
}
