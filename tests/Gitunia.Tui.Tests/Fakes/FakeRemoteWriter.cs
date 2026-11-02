using Gitunia.Git.Remotes;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>
/// Records the remote calls. Each call reports <see cref="Reports"/>, then waits for <see cref="Release"/> when
/// <see cref="Blocks"/> is set (honouring the cancellation token like the real writer), then throws
/// <see cref="Failure"/> or answers.
/// </summary>
public sealed class FakeRemoteWriter : IRemoteWriter
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The calls in order, such as <c>fetch|/path|all=False</c>.</summary>
    // ReSharper disable once CollectionNeverQueried.Global : fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public List<string> Calls { get; } = [];

    /// <summary>The progress every call reports first.</summary>
    // ReSharper disable once CollectionNeverUpdated.Global : fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public List<RemoteProgress> Reports { get; } = [];

    /// <summary>Calls wait for <see cref="Release"/> before they finish.</summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global : fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public bool Blocks { get; set; }

    /// <summary>Thrown by every call when set.</summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global : fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public Exception? Failure { get; set; }

    /// <summary>What a pull answers.</summary>
    public PullOutcome Outcome { get; set; } = PullOutcome.FastForwarded;

    /// <summary>Completes once a call has reported and is waiting.</summary>
    public Task Started => _started.Task;

    public void Release() => _release.TrySetResult();

    public Task FetchAsync(
        RepositoryLocation location,
        FetchRequest request,
        IProgress<RemoteProgress>? progress,
        CancellationToken cancellationToken) =>
        RunAsync(
            $"fetch|{location.RootPath}|all={request.AllRemotes}|prune={request.Prune}",
            progress,
            cancellationToken);

    public async Task<PullOutcome> PullAsync(
        RepositoryLocation location,
        IProgress<RemoteProgress>? progress,
        CancellationToken cancellationToken)
    {
        await RunAsync($"pull|{location.RootPath}", progress, cancellationToken);
        return Outcome;
    }

    public Task PushAsync(
        RepositoryLocation location,
        PushRequest request,
        IProgress<RemoteProgress>? progress,
        CancellationToken cancellationToken)
    {
        var remote = request.Remote ?? "(default)";
        var lease = $"{request.Lease?.RemoteBranch}:{request.Lease?.ExpectedHash}";
        var target = $"remote={remote}|branch={request.Branch}|upstream={request.SetUpstream}|lease={lease}";
        return RunAsync($"push|{location.RootPath}|{target}", progress, cancellationToken);
    }

    public Task DeleteRemoteBranchAsync(
        RepositoryLocation location,
        string remote,
        string branch,
        string expectedHash,
        CancellationToken cancellationToken) =>
        RunAsync($"delete|{location.RootPath}|{remote}|{branch}|{expectedHash}", null, cancellationToken);

    private async Task RunAsync(string call, IProgress<RemoteProgress>? progress, CancellationToken cancellationToken)
    {
        Calls.Add(call);
        foreach (var report in Reports)
        {
            progress?.Report(report);
        }

        _started.TrySetResult();
        if (Blocks)
        {
            await _release.Task.WaitAsync(cancellationToken);
        }

        if (Failure is not null)
        {
            throw Failure;
        }
    }
}
