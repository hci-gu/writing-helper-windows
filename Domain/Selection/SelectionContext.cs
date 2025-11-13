using System;

namespace GlobalTextHelper.Domain.Selection;

public sealed class SelectionContext
{
    public SelectionContext(string originalText, string normalizedText, IntPtr sourceWindow, DateTime timestamp)
    {
        OriginalText = originalText;
        NormalizedText = normalizedText;
        SourceWindow = sourceWindow;
        Timestamp = timestamp;
    }

    public string OriginalText { get; }
    public string NormalizedText { get; }
    public IntPtr SourceWindow { get; }
    public DateTime Timestamp { get; }
}
