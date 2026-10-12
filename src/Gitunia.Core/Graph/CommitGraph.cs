using Gitunia.Git;

namespace Gitunia.Core.Graph;

/// <summary>
/// The rows of one tab's graph, appended while the log streams in. Append-only while loading: rows never change or move, so
/// an index stays valid while loading continues; only a refresh replaces them all at once. Not thread-safe; the owner appends from one thread (the UI thread).
/// </summary>
public sealed class CommitGraph
{
    private readonly List<GraphEntry> entries = [];

    /// <summary>Number of rows loaded so far.</summary>
    public int Count => entries.Count;

    /// <summary>The widest row seen so far, in lanes; only grows, so lane columns stay put while scrolling and loading.</summary>
    public int MaximumLaneCount { get; private set; } = 1;

    public GraphEntry this[int index] => entries[index];

    /// <summary>Appends rows in log order.</summary>
    public void AddRange(IReadOnlyList<GraphEntry> batch)
    {
        foreach (var entry in batch)
        {
            entries.Add(entry);
            MaximumLaneCount = Math.Max(MaximumLaneCount, entry.Row.LaneCount);
        }
    }

    /// <summary>Replaces every row (a refreshed log); the lane width starts over so it can shrink again.</summary>
    public void Replace(IReadOnlyList<GraphEntry> replacement)
    {
        entries.Clear();
        MaximumLaneCount = 1;
        AddRange(replacement);
    }

    /// <summary>Index of the commit, or -1 when it is not loaded (yet).</summary>
    public int IndexOf(ObjectId commitId)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index].Id == commitId)
            {
                return index;
            }
        }

        return -1;
    }
}
