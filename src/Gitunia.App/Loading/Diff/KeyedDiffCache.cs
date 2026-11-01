using Gitunia.Core.Diff;

namespace Gitunia.App.Loading.Diff;

/// <summary>
/// A least-recently-used cache of diffs, bounded twice: by entry count and by the rows held in total (a diff row is
/// roughly 100 to 200 bytes, so the default budget keeps a tab to a few megabytes whatever the graph size, see #199). A diff alone
/// over the row budget is not kept. Not thread-safe: UI thread only. The key says what a diff is of: <see cref="DiffCache"/> keys
/// commits, which never change, so entries never go stale; the Changes view keys files and drops everything when the status changes.
/// </summary>
/// <typeparam name="TKey">What a diff belongs to.</typeparam>
public class KeyedDiffCache<TKey>
    where TKey : notnull
{
    /// <summary>Entries kept by default; the neighbours prefetched around the selection fit well inside it.</summary>
    public const int DefaultCapacity = 12;

    /// <summary>Rows kept by default across all entries.</summary>
    public const int DefaultRowBudget = 16_000;

    private readonly int _capacity;
    private readonly int _rowBudget;
    private readonly Dictionary<TKey, LinkedListNode<KeyedDiffCacheEntry<TKey>>> _nodes = [];
    private readonly LinkedList<KeyedDiffCacheEntry<TKey>> _recentFirst = new();
    private int _rowCount;

    public KeyedDiffCache(int capacity = DefaultCapacity, int rowBudget = DefaultRowBudget)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowBudget, 0);
        _capacity = capacity;
        _rowBudget = rowBudget;
    }

    public int Count => _nodes.Count;

    /// <summary>The rows held by all entries.</summary>
    public int RowCount => _rowCount;

    /// <summary>Looks an entry up and marks it as the most recently used.</summary>
    public bool TryGet(TKey key, out DiffDocument document)
    {
        if (_nodes.TryGetValue(key, out var node))
        {
            _recentFirst.Remove(node);
            _recentFirst.AddFirst(node);
            document = node.Value.Document;
            return true;
        }

        document = DiffDocument.Empty;
        return false;
    }

    /// <summary>Whether a key is cached, without changing its recency.</summary>
    public bool Contains(TKey key) => _nodes.ContainsKey(key);

    /// <summary>Adds (or refreshes) an entry as the most recently used and evicts the least recently used ones beyond the bounds.</summary>
    public void Add(TKey key, DiffDocument document)
    {
        Remove(key);
        if (document.Rows.Count > _rowBudget)
        {
            return;
        }

        _nodes[key] = _recentFirst.AddFirst(new KeyedDiffCacheEntry<TKey>(key, document));
        _rowCount += document.Rows.Count;
        while (_nodes.Count > _capacity || _rowCount > _rowBudget)
        {
            Remove(_recentFirst.Last!.Value.Key);
        }
    }

    public void Clear()
    {
        _nodes.Clear();
        _recentFirst.Clear();
        _rowCount = 0;
    }

    private void Remove(TKey key)
    {
        if (_nodes.Remove(key, out var node))
        {
            _recentFirst.Remove(node);
            _rowCount -= node.Value.Document.Rows.Count;
        }
    }
}
