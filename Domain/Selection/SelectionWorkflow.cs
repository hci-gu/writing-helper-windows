using System;

namespace GlobalTextHelper.Domain.Selection;

public sealed class SelectionWorkflow
{
    private readonly TimeSpan _dedupeWindow = TimeSpan.FromSeconds(1.5);
    private string? _lastSelectionText;
    private DateTime _lastShownAt;
    private bool _selectionActive;

    public bool TryHandleSelection(SelectionCapturedEventArgs args, out SelectionContext? context)
    {
        context = null;
        if (args is null)
        {
            return false;
        }

        var normalized = Normalize(args.Text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(_lastSelectionText) &&
            string.Equals(normalized, _lastSelectionText, StringComparison.Ordinal) &&
            (now - _lastShownAt) < _dedupeWindow)
        {
            return false;
        }

        _selectionActive = true;
        _lastSelectionText = normalized;
        _lastShownAt = now;
        context = new SelectionContext(args.Text, normalized, args.SourceWindow, now);
        return true;
    }

    public void MarkSelectionHandled()
    {
        _selectionActive = false;
    }

    private static string Normalize(string text)
    {
        return text?.Trim() ?? string.Empty;
    }
}
