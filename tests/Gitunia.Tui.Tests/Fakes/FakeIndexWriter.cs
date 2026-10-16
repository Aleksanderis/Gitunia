using Gitunia.Git.Diff.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Staging;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Records the stage and unstage calls instead of running git; can fail or wait on demand.</summary>
public sealed class FakeIndexWriter : IIndexWriter
{
    private readonly TaskCompletionSource _gate = new();

    public List<IReadOnlyList<string>> Staged { get; } = [];

    public List<IReadOnlyList<string>> Unstaged { get; } = [];

    public List<bool> UnbornFlags { get; } = [];

    public List<AppliedPatch> Patches { get; } = [];

    /// <summary>Thrown by the next calls when set.</summary>
    public Exception? Failure { get; set; }

    /// <summary>When true, calls wait until <see cref="Release"/>.</summary>
    public bool IsHeld { get; set; }

    public void Release() => _gate.TrySetResult();

    public async Task StageAsync(RepositoryLocation location, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        await WaitAsync().ConfigureAwait(false);
        Staged.Add(paths);
    }

    public async Task IntentToAddAsync(RepositoryLocation location, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        await WaitAsync().ConfigureAwait(false);
    }

    public async Task UnstageAsync(RepositoryLocation location, IReadOnlyList<string> paths, bool isHeadUnborn, CancellationToken cancellationToken)
    {
        await WaitAsync().ConfigureAwait(false);
        Unstaged.Add(paths);
        UnbornFlags.Add(isHeadUnborn);
    }

    public async Task ApplyPatchAsync(
        RepositoryLocation location,
        string patch,
        PatchDirection direction,
        CancellationToken cancellationToken)
    {
        await WaitAsync().ConfigureAwait(false);
        Patches.Add(new AppliedPatch(patch, direction));
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
