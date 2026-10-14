using Gitunia.Core.Diff;
using Gitunia.Git;

namespace Gitunia.Tui.Loading.Diff;

/// <summary>
/// A least-recently-used cache of commit diffs, bounded twice: by entry count and by the rows held in total (a diff row is
/// roughly 100 to 200 bytes, so the default budget keeps a tab to a few megabytes whatever the graph size, see #199). The diff of a
/// commit never changes, so entries never go stale. A diff alone over the row budget is not kept. Not thread-safe: UI thread only.
/// </summary>
public sealed class DiffCache
{
    /// <summary>Entries kept by default; the neighbours prefetched around the selection fit well inside it.</summary>
    public const int DefaultCapacity = 12;

    /// <summary>Rows kept by default across all entries.</summary>
    public const int DefaultRowBudget = 16_000;

    private readonly int capacity;
    private readonly int rowBudget;
    private readonly Dictionary<ObjectId, LinkedListNode<DiffCacheEntry>> nodes = [];
    private readonly LinkedList<DiffCacheEntry> recentFirst = new();
    private int rowCount;

    public DiffCache(int capacity = DefaultCapacity, int rowBudget = DefaultRowBudget)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowBudget, 0);
        this.capacity = capacity;
        this.rowBudget = rowBudget;
    }

    public int Count => nodes.Count;

    /// <summary>The rows held by all entries.</summary>
    public int RowCount => rowCount;

    /// <summary>Looks a commit up and marks it as the most recently used.</summary>
    public bool TryGet(ObjectId commit, out DiffDocument document)
    {
        if (nodes.TryGetValue(commit, out var node))
        {
            recentFirst.Remove(node);
            recentFirst.AddFirst(node);
            document = node.Value.Document;
            return true;
        }

        document = DiffDocument.Empty;
        return false;
    }

    /// <summary>Whether a commit is cached, without changing its recency.</summary>
    public bool Contains(ObjectId commit) => nodes.ContainsKey(commit);

    /// <summary>Adds (or refreshes) an entry as the most recently used and evicts the least recently used ones beyond the bounds.</summary>
    public void Add(ObjectId commit, DiffDocument document)
    {
        Remove(commit);
        if (document.Rows.Count > rowBudget)
        {
            return;
        }

        nodes[commit] = recentFirst.AddFirst(new DiffCacheEntry(commit, document));
        rowCount += document.Rows.Count;
        while (nodes.Count > capacity || rowCount > rowBudget)
        {
            Remove(recentFirst.Last!.Value.Commit);
        }
    }

    public void Clear()
    {
        nodes.Clear();
        recentFirst.Clear();
        rowCount = 0;
    }

    private void Remove(ObjectId commit)
    {
        if (nodes.Remove(commit, out var node))
        {
            recentFirst.Remove(node);
            rowCount -= node.Value.Document.Rows.Count;
        }
    }
}
