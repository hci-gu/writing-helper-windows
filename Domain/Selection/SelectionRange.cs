using System;

namespace GlobalTextHelper.Domain.Selection;

public readonly struct SelectionRange : IEquatable<SelectionRange>
{
    public SelectionRange(int start, int end)
    {
        if (end < start)
        {
            (start, end) = (end, start);
        }

        Start = Math.Max(0, start);
        End = Math.Max(Start, end);
    }

    public int Start { get; }
    public int End { get; }
    public int Length => Math.Max(0, End - Start);

    public bool Equals(SelectionRange other) => Start == other.Start && End == other.End;

    public override bool Equals(object? obj) => obj is SelectionRange other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Start, End);

    public override string ToString() => $"[{Start}, {End})";
}
