namespace GlobalTextHelper.Domain.Selection;

public readonly struct SelectionRange
{
    public SelectionRange(int start, int end)
    {
        if (end < start)
        {
            (start, end) = (end, start);
        }

        Start = start;
        End = end;
    }

    public int Start { get; }
    public int End { get; }

    public bool IsEmpty => Start >= End;
}
