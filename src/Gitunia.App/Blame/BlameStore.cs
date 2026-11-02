namespace Gitunia.App.Blame;

/// <summary>
/// Holds the lines of the one open blame view outside <c>AppState</c>. The handler replaces the data when a view opens
/// and clears it when it closes; the view reads <see cref="Current"/> (a whole snapshot, never half filled).
/// </summary>
public sealed class BlameStore
{
    private BlameData? _current;

    /// <summary>The data of the open view, null while none is open or its file is not read yet.</summary>
    public BlameData? Current => Volatile.Read(ref _current);

    /// <summary>Publishes new data and drops the old; data older than what is published is ignored.</summary>
    /// <param name="data">The data.</param>
    public void Publish(BlameData data)
    {
        if (Current is { } current && current.Generation > data.Generation)
        {
            return;
        }

        Volatile.Write(ref _current, data);
    }

    /// <summary>Drops the data of a read; data of a newer read stays.</summary>
    /// <param name="generation">The read number to drop.</param>
    public void Clear(long generation)
    {
        if (Current is { } data && data.Generation <= generation)
        {
            Volatile.Write(ref _current, null);
        }
    }
}
