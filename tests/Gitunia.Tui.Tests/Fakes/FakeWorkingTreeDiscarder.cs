using Gitunia.Git.Discard;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Records what would be thrown away instead of running git; can fail or wait on demand.</summary>
public sealed class FakeWorkingTreeDiscarder : IWorkingTreeDiscarder
{
    private readonly TaskCompletionSource _gate = new();

    public List<IReadOnlyList<string>> Restored { get; } = [];

    public List<IReadOnlyList<string>> Deleted { get; } = [];

    public List<string> ReversedPatches { get; } = [];

    /// <summary>Thrown by the next calls when set.</summary>
    public Exception? Failure { get; set; }

    /// <summary>When true, calls wait until <see cref="Release"/>.</summary>
    public bool IsHeld { get; set; }

    public int CallCount => Restored.Count + Deleted.Count + ReversedPatches.Count;

    public void Release() => _gate.TrySetResult();

    public async Task RestoreAsync(RepositoryLocation location, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        await WaitAsync().ConfigureAwait(false);
        Restored.Add(paths);
    }

    public async Task DeleteUntrackedAsync(
        RepositoryLocation location,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        await WaitAsync().ConfigureAwait(false);
        Deleted.Add(paths);
    }

    public async Task ApplyReversePatchAsync(RepositoryLocation location, string patch, CancellationToken cancellationToken)
    {
        await WaitAsync().ConfigureAwait(false);
        ReversedPatches.Add(patch);
    }

    private async Task WaitAsync()
    {
        if (IsHeld)
        {
            await _gate.Task.ConfigureAwait(false);
        }

        if (Failure is { } failure)
        {
            throw failure;
        }
    }
}
