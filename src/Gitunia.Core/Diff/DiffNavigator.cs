namespace Gitunia.Core.Diff;

/// <summary>Pure jumps between hunks and files of a <see cref="DiffDocument"/>.</summary>
public static class DiffNavigator
{
    /// <summary>
    /// The row <paramref name="count"/> anchors after (or before) <paramref name="row"/>, stopping at the last one available;
    /// null when there is no anchor in that direction.
    /// </summary>
    /// <param name="anchors">Ascending anchor rows, such as <see cref="DiffDocument.HunkRows"/>.</param>
    /// <param name="row">The current row.</param>
    /// <param name="forward">Jump towards the end.</param>
    /// <param name="count">How many anchors to skip; at least one.</param>
    public static int? Step(IReadOnlyList<int> anchors, int row, bool forward, int count)
    {
        var steps = Math.Max(1, count);
        int? target = null;
        var current = row;
        for (var step = 0; step < steps; step++)
        {
            var next = forward ? NextAfter(anchors, current) : PreviousBefore(anchors, current);
            if (next is null)
            {
                break;
            }

            target = next;
            current = next.Value;
        }

        return target;
    }

    private static int? NextAfter(IReadOnlyList<int> anchors, int row)
    {
        foreach (var anchor in anchors)
        {
            if (anchor > row)
            {
                return anchor;
            }
        }

        return null;
    }

    private static int? PreviousBefore(IReadOnlyList<int> anchors, int row)
    {
        for (var index = anchors.Count - 1; index >= 0; index--)
        {
            if (anchors[index] < row)
            {
                return anchors[index];
            }
        }

        return null;
    }
}
