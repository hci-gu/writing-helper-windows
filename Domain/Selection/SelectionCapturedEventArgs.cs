using System;

namespace GlobalTextHelper.Domain.Selection;

public sealed class SelectionCapturedEventArgs : EventArgs
{
    public SelectionCapturedEventArgs(string text, IntPtr sourceWindow, SelectionSource source, DateTime timestamp)
    {
        Text = text;
        SourceWindow = sourceWindow;
        Source = source;
        Timestamp = timestamp;
    }

    public string Text { get; }
    public IntPtr SourceWindow { get; }
    public SelectionSource Source { get; }
    public DateTime Timestamp { get; }
}
