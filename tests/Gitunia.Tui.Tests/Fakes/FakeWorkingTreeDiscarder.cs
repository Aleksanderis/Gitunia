using Gitunia.Git.Discard;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Records what would be thrown away instead of running git; can fail or wait on demand.</summary>
public sealed class FakeWorkingTreeDiscarder : IWorkingTreeDiscarder
{
    private readonly TaskCompletionSource _gate = new();

    /// <summary>What the fake reports for names that are not valid UTF-8.</summary>
    public bool CanDeleteInvalidNames { get; set; } = true;

    public List<IReadOnlyList<string>> Restored { get; } = [];

    public List<IReadOnlyList<string>> Deleted { get; } = [];

    public List<string> ReversedPatches { get; } = [];

    /// <summary>Thrown by the next calls when set.</summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global : fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public Exception? Failure { get; set; }

    /// <summary>When true, calls wait until <see cref="Release"/>.</summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global : fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
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
            await _gate.Task.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        }

        if (Failure is { } failure)
        {
            throw failure;
        }
    }
}
