namespace Gitunia.Core.Diff;

/// <summary>A stretch of a line's text that differs from the paired line.</summary>
/// <param name="Start">Index of the first changed UTF-16 character.</param>
/// <param name="Length">Number of changed characters.</param>
public readonly record struct ChangedSpan(int Start, int Length)
{
    /// <summary>Index after the last changed character.</summary>
    public int End => Start + Length;

    /// <summary>The stretches of both lists as one ascending list without overlaps.</summary>
    public static IReadOnlyList<ChangedSpan> Union(IReadOnlyList<ChangedSpan> first, IReadOnlyList<ChangedSpan> second)
    {
        if (second.Count == 0)
        {
            return first;
        }

        if (first.Count == 0)
        {
            return second;
        }

        var all = new List<ChangedSpan>(first.Count + second.Count);
        all.AddRange(first);
        all.AddRange(second);
        all.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        var merged = new List<ChangedSpan>(all.Count);
        foreach (var span in all)
        {
            if (merged.Count > 0 && span.Start <= merged[^1].End)
            {
                var last = merged[^1];
                merged[^1] = new ChangedSpan(last.Start, Math.Max(last.End, span.End) - last.Start);
            }
            else
            {
                merged.Add(span);
            }
        }

        return merged;
    }
}
