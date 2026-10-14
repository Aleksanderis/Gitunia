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

    private readonly int _capacity;
    private readonly int _rowBudget;
    private readonly Dictionary<ObjectId, LinkedListNode<DiffCacheEntry>> _nodes = [];
    private readonly LinkedList<DiffCacheEntry> _recentFirst = new();
    private int _rowCount;

    public DiffCache(int capacity = DefaultCapacity, int rowBudget = DefaultRowBudget)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowBudget, 0);
        _capacity = capacity;
        _rowBudget = rowBudget;
    }

    public int Count => _nodes.Count;

    /// <summary>The rows held by all entries.</summary>
    public int RowCount => _rowCount;

    /// <summary>Looks a commit up and marks it as the most recently used.</summary>
    public bool TryGet(ObjectId commit, out DiffDocument document)
    {
        if (_nodes.TryGetValue(commit, out var node))
        {
            _recentFirst.Remove(node);
            _recentFirst.AddFirst(node);
            document = node.Value.Document;
            return true;
        }

        document = DiffDocument.Empty;
        return false;
    }

    /// <summary>Whether a commit is cached, without changing its recency.</summary>
    public bool Contains(ObjectId commit) => _nodes.ContainsKey(commit);

    /// <summary>Adds (or refreshes) an entry as the most recently used and evicts the least recently used ones beyond the bounds.</summary>
    public void Add(ObjectId commit, DiffDocument document)
    {
        Remove(commit);
        if (document.Rows.Count > _rowBudget)
        {
            return;
        }

        _nodes[commit] = _recentFirst.AddFirst(new DiffCacheEntry(commit, document));
        _rowCount += document.Rows.Count;
        while (_nodes.Count > _capacity || _rowCount > _rowBudget)
        {
            Remove(_recentFirst.Last!.Value.Commit);
        }
    }

    public void Clear()
    {
        _nodes.Clear();
        _recentFirst.Clear();
        _rowCount = 0;
    }

    private void Remove(ObjectId commit)
    {
        if (_nodes.Remove(commit, out var node))
        {
            _recentFirst.Remove(node);
            _rowCount -= node.Value.Document.Rows.Count;
        }
    }
}
