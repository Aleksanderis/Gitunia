namespace Gitunia.Git;

/// <summary>
/// A cap on the number of file system watchers open across all tabs (each one costs inotify watches or handles).
/// A tab that cannot get its share polls instead.
/// </summary>
public sealed class WatchBudget
{
    public const int DefaultMaximum = 64;

    private readonly object _gate = new();
    private int _inUse;

    public WatchBudget(int maximum = DefaultMaximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        Maximum = maximum;
    }

    public int Maximum { get; }

    public int InUse
    {
        get
        {
            lock (_gate)
            {
                return _inUse;
            }
        }
    }

    /// <summary>Takes <paramref name="count"/> slots or none at all.</summary>
    public bool TryAcquire(int count)
    {
        lock (_gate)
        {
            if (_inUse + count > Maximum)
            {
                return false;
            }

            _inUse += count;
            return true;
        }
    }

    public void Release(int count)
    {
        lock (_gate)
        {
            _inUse = Math.Max(0, _inUse - count);
        }
    }
}
