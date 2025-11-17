using System;

namespace GlobalTextHelper.Domain.Selection;

public sealed class SelectionContext
{
    public SelectionContext(
        string originalText,
        string normalizedText,
        IntPtr sourceWindow,
        DateTime timestamp,
        SelectionRange? selectionRange)
    {
        OriginalText = originalText;
        NormalizedText = normalizedText;
        SourceWindow = sourceWindow;
        Timestamp = timestamp;
        SelectionRange = selectionRange;
    }

    public string OriginalText { get; }
    public string NormalizedText { get; }
    public IntPtr SourceWindow { get; }
    public DateTime Timestamp { get; }
    public SelectionRange? SelectionRange { get; }
}
