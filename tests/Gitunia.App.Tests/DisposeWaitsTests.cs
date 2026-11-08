using System.Runtime.CompilerServices;
using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.App.Tests.Fakes;
using Gitunia.Core;
using Gitunia.Core.Blame;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Git.Blame;
using Gitunia.Git.Blame.Models;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.IssueTracker;
using Gitunia.Git.IssueTracker.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Xunit;

namespace Gitunia.App.Tests;

// Background work that is not a refresh (#1260): the shutdown must wait for each kind until its process is gone.
public class DisposeWaitsTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    [Fact]
    public void GivenAStatusPassInFlight_WhenThePollerIsDisposed_ThenWaitForStopWaitsForTheCancelledReadToEnd()
    {
        var reader = new BlockingReader();
        var active = FakeStatusReader.LocationOf("active");
        var background = FakeStatusReader.LocationOf("background");
        var workspace = Workspace.Empty;
        foreach (var location in new[] { active, background })
        {
            workspace = WorkspaceReducer.Reduce(workspace, new OpenRepositoryCommand(location)).Workspace;
        }

        var host = new Dispatcher(AppState.Empty);
        host.Restore(new WorkspaceUpdate(workspace with { ActiveTabId = workspace.Tabs[0].Id }, []));
        var poller = new BackgroundStatusPoller(reader, host, InlineUi.Instance);
        poller.Start();
        Assert.True(reader.WaitStarted(Limit));

        poller.Dispose();

        Assert.True(poller.WaitForStop(Limit));
        Assert.Equal(1, reader.WoundDownCount);
    }

    [Fact]
    public async Task GivenBlameCommitDetailsAndIssueLinksInFlight_WhenTheRunnerIsDisposed_ThenItWaitsForEachCancelledRead()
    {
        var fakes = new FakeGitServices();
        fakes.CommitLog.AddOnTop("root");
        var issues = new BlockingReader();
        var blame = new BlockingReader();
        var details = new BlockingReader();
        var services = fakes.Services with { IssueTracker = issues, Blame = blame, CommitDetails = details };
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var location = FakeStatusReader.LocationOf("first");
        var scratch = WorkspaceReducer.Reduce(Workspace.Empty, new OpenRepositoryCommand(location)).Workspace;
        var tabId = scratch.Tabs[0].Id;
        var host = new Dispatcher(AppState.Empty);
        var effects = new EffectRunner<TabSession>(
            services,
            host,
            pump,
            new TabSessionFactory(),
            new EffectRunnerOptions<TabSession>
            {
                Statuses = new StatusReporter(host),
                Watcher = TestGit.QuietWatcherOptions,
            });
        host.Restore(new WorkspaceUpdate(scratch with { ActiveTabId = tabId }, [new LoadRepositoryEffect(tabId, scratch.Tabs[0].CommandPath)]));
        await pump.RunUntilAsync(() => !effects.SessionOf(tabId)!.IsLoading);
        effects.Run(new StartBlameEffect(tabId, location, "a.txt", null, 1));
        effects.SessionOf(tabId)!.Details.Select(FakeCommitDetailsReader.Chain(1).Commits[0]);
        Assert.True(issues.WaitStarted(Limit));
        Assert.True(blame.WaitStarted(Limit));
        Assert.True(details.WaitStarted(Limit));

        effects.Dispose();

        Assert.Equal(1, issues.WoundDownCount);
        Assert.Equal(1, blame.WoundDownCount);
        Assert.Equal(1, details.WoundDownCount);
    }

    // Plays a read whose process takes a while to die: it blocks until cancelled, then needs time to wind down.
    private sealed class BlockingReader : IStatusReader, IBlameReader, ICommitDetailsReader, IIssueTrackerReader
    {
        private readonly TaskCompletionSource _started = new();
        private int _woundDown;

        public int WoundDownCount => Volatile.Read(ref _woundDown);

        public bool WaitStarted(TimeSpan timeout) => _started.Task.Wait(timeout);

        public async Task<StatusSnapshot> ReadStatusAsync(RepositoryLocation location, bool includeIgnored, UntrackedFilesMode untrackedFiles, CancellationToken cancellationToken)
        {
            await BlockAsync(cancellationToken);

            throw new InvalidOperationException("unreachable");
        }

        public async Task<BlameContent> ReadContentAsync(RepositoryLocation location, string path, string? revision, CancellationToken cancellationToken)
        {
            await BlockAsync(cancellationToken);

            throw new InvalidOperationException("unreachable");
        }

        public async IAsyncEnumerable<BlameChunk> BlameAsync(
            RepositoryLocation location,
            string path,
            string? revision,
            BlameOptions? options = null,
            ICollection<string>? warnings = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await BlockAsync(cancellationToken);
            yield break;
        }

        public async Task<string?> ReadIgnoreRevsFileAsync(RepositoryLocation location, CancellationToken cancellationToken)
        {
            await BlockAsync(cancellationToken);

            return null;
        }

        public async Task<CommitDetails?> ReadAsync(RepositoryLocation location, ObjectId id, ICollection<string>? warnings = null, CancellationToken cancellationToken = default)
        {
            await BlockAsync(cancellationToken);

            return null;
        }

        public async Task<IReadOnlyList<CommitFileChange>> ReadFilesAsync(
            RepositoryLocation location,
            ObjectId id,
            ObjectId? parent,
            ICollection<string>? warnings = null,
            CancellationToken cancellationToken = default)
        {
            await BlockAsync(cancellationToken);

            return [];
        }

        public async Task<IReadOnlyList<IssueTrackerRule>> ReadAsync(RepositoryLocation location, CancellationToken cancellationToken)
        {
            await BlockAsync(cancellationToken);

            return [];
        }

        private async Task BlockAsync(CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300), CancellationToken.None);
                Interlocked.Increment(ref _woundDown);

                throw;
            }
        }
    }
}
