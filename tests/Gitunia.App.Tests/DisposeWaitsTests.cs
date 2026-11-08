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
using Gitunia.Git.Blobs;
using Gitunia.Git.Blobs.Models;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.History;
using Gitunia.Git.History.Models;
using Gitunia.Git.Staging;
using Gitunia.Git.Trees;
using Gitunia.Git.Trees.Models;
using Gitunia.Core.Commits;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Staging;
using Gitunia.App.Loading.Diff;
using Gitunia.App.Worktrees;
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
        var (effects, tabId, location) = await StartAsync(fakes, services);
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

    private static async Task<(EffectRunner<TabSession> Effects, TabId TabId, RepositoryLocation Location)> StartAsync(FakeGitServices fakes, GitServices services)
    {
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

        return (effects, tabId, location);
    }

    [Fact]
    public async Task GivenStoreReadsAndEffectsInFlight_WhenTheRunnerIsDisposed_ThenItWaitsForEachCancelledRead()
    {
        var fakes = new FakeGitServices();
        fakes.CommitLog.AddOnTop("root");
        var diff = new BlockingReader();
        var tree = new BlockingReader();
        var blob = new BlockingReader();
        var history = new BlockingReader();
        var commits = new BlockingReader();
        var index = new BlockingReader();
        var services = fakes.Services with
        {
            Diff = diff,
            CommitTree = tree,
            Blob = blob,
            FileHistory = history,
            CommitWriter = commits,
            IndexWriter = index,
        };
        var (effects, tabId, location) = await StartAsync(fakes, services);
        var session = effects.SessionOf(tabId)!;
        var commit = FakeCommitDetailsReader.Chain(1).Commits[0];
        var entry = new StatusEntry(StatusEntryKind.Ordinary, "a.txt", null, FileChangeKind.Unmodified, FileChangeKind.Modified, null, ConflictKind.None, null);
        Assert.True(ObjectId.TryParse(new string('a', 40), out var blobId));
        session.DiffStore!.Load(commit, null);
        session.WorkingDiffStore!.Show(new WorkingDiffRequest(WorkingDiffSide.Unstaged, entry), 1);
        session.Tree!.Store.Select(commit);
        session.ContentStore!.Load(commit, "a.txt", blobId, null);
        effects.Run(new ReadFileHistoryEffect(tabId, location, "HEAD", "a.txt", 1));
        effects.Run(new ReadHeadMessageEffect(tabId, location, null));
        effects.Run(new RunCommitEffect(tabId, location, new CommitRequest("m", false, false, false), null));
        var historyEntry = new FileHistoryEntry(new string('b', 40), "a", "d", "s", "a.txt", null, FileChangeKind.Modified);
        effects.Run(new ReadFileHistoryDiffEffect(tabId, location, historyEntry, 1));
        effects.Run(new StagePathsEffect(tabId, location, new StagingPlan(["a.txt"], "a.txt", 1, 0, string.Empty), true, false, StagingFollow.None));
        Assert.True(diff.WaitStarted(Limit, 3));
        Assert.True(tree.WaitStarted(Limit));
        Assert.True(blob.WaitStarted(Limit));
        Assert.True(history.WaitStarted(Limit));
        Assert.True(commits.WaitStarted(Limit, 2));
        Assert.True(index.WaitStarted(Limit));

        effects.Dispose();

        Assert.Equal(3, diff.WoundDownCount);
        Assert.Equal(1, tree.WoundDownCount);
        Assert.Equal(1, blob.WoundDownCount);
        Assert.Equal(1, history.WoundDownCount);
        Assert.Equal(2, commits.WoundDownCount);
        Assert.Equal(1, index.WoundDownCount);
    }

    [Fact]
    public async Task GivenAWorktreePassInFlight_WhenThePollerIsDisposed_ThenWaitForStopWaitsForTheCancelledReadToEnd()
    {
        using var repository = TestRepositories.WithWorktree(out _);
        var host = new Dispatcher(AppState.Empty);
        var location = await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, TestContext.Current.CancellationToken);
        host.Dispatch(new OpenRepositoryCommand(location));
        using var session = new FakeGraphSession(host.Workspace.ActiveTabId!.Value);
        await TestGit.Services.RepositoryLoader
            .RunAsync(location, session, host, new StatusReporter(host), InlineUi.Instance, TestContext.Current.CancellationToken);
        var reader = new BlockingReader();
        var poller = new WorktreeOverviewPoller(reader, host);
        poller.Start();
        Assert.True(reader.WaitStarted(Limit));

        poller.Dispose();

        Assert.True(poller.WaitForStop(Limit));
        Assert.Equal(1, reader.WoundDownCount);
    }

    // Plays a read whose process takes a while to die: it blocks until cancelled, then needs time to wind down.
    private sealed class BlockingReader : IStatusReader, IBlameReader, ICommitDetailsReader, IIssueTrackerReader, IDiffReader, ICommitTreeReader, IBlobReader, IFileHistoryReader, ICommitWriter, IIndexWriter, IDisposable
    {
        private readonly SemaphoreSlim _started = new(0);
        private int _woundDown;

        public void Dispose() => _started.Dispose();

        public int WoundDownCount => Volatile.Read(ref _woundDown);

        public bool WaitStarted(TimeSpan timeout, int count = 1)
        {
            for (var index = 0; index < count; index++)
            {
                if (!_started.Wait(timeout))
                {
                    return false;
                }
            }

            return true;
        }

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

        public async IAsyncEnumerable<DiffFile> ReadAsync(
            RepositoryLocation location,
            IReadOnlyList<string> command,
            ICollection<string>? warnings = null,
            int maximumLinesPerFile = int.MaxValue,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await BlockAsync(cancellationToken);
            yield break;
        }

        public async IAsyncEnumerable<DiffFile> ReadCommitAsync(
            RepositoryLocation location,
            ObjectId commit,
            ObjectId? firstParent,
            ICollection<string>? warnings = null,
            int maximumLinesPerFile = int.MaxValue,
            DiffOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await BlockAsync(cancellationToken);
            yield break;
        }

        public async IAsyncEnumerable<DiffFile> ReadWorkingFileAsync(
            RepositoryLocation location,
            WorkingDiffSide side,
            StatusEntry entry,
            ICollection<string>? warnings = null,
            int maximumLinesPerFile = int.MaxValue,
            DiffOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await BlockAsync(cancellationToken);
            yield break;
        }

        public async Task<ObjectId?> ReadFirstParentAsync(RepositoryLocation location, ObjectId commit, CancellationToken cancellationToken)
        {
            await BlockAsync(cancellationToken);

            return null;
        }

        async Task<IReadOnlyList<TreeEntry>> ICommitTreeReader.ReadAsync(RepositoryLocation location, ObjectId commit, ICollection<string>? warnings, CancellationToken cancellationToken)
        {
            await BlockAsync(cancellationToken);

            return [];
        }

        public async Task<BlobContent> ReadAsync(RepositoryLocation location, ObjectId blob, long? knownSize = null, CancellationToken cancellationToken = default)
        {
            await BlockAsync(cancellationToken);

            throw new InvalidOperationException("unreachable");
        }

        public async Task<ParseResult<FileHistoryEntry>> ReadAsync(RepositoryLocation location, string revision, string path, int limit, CancellationToken cancellationToken)
        {
            await BlockAsync(cancellationToken);

            throw new InvalidOperationException("unreachable");
        }

        public async Task<ParseResult<FileHistoryEntry>> ReadRangeAsync(RepositoryLocation location, string revision, string path, LineRange range, int limit, CancellationToken cancellationToken)
        {
            await BlockAsync(cancellationToken);

            throw new InvalidOperationException("unreachable");
        }

        public async Task<CommitResult> CommitAsync(RepositoryLocation location, CommitRequest request, CancellationToken cancellationToken)
        {
            await BlockAsync(cancellationToken);

            throw new InvalidOperationException("unreachable");
        }

        public async Task<string?> ReadHeadMessageAsync(RepositoryLocation location, CancellationToken cancellationToken)
        {
            await BlockAsync(cancellationToken);

            return null;
        }

        public async Task<string?> ReadMergeMessageAsync(RepositoryLocation location, CancellationToken cancellationToken)
        {
            await BlockAsync(cancellationToken);

            return null;
        }

        public async Task StageAsync(RepositoryLocation location, IReadOnlyList<string> paths, CancellationToken cancellationToken) => await BlockAsync(cancellationToken);

        public async Task IntentToAddAsync(RepositoryLocation location, IReadOnlyList<string> paths, CancellationToken cancellationToken) => await BlockAsync(cancellationToken);

        public async Task UnstageAsync(RepositoryLocation location, IReadOnlyList<string> paths, bool isHeadUnborn, CancellationToken cancellationToken) => await BlockAsync(cancellationToken);

        public async Task ApplyPatchAsync(RepositoryLocation location, string patch, PatchDirection direction, CancellationToken cancellationToken) => await BlockAsync(cancellationToken);

        private async Task BlockAsync(CancellationToken cancellationToken)
        {
            _started.Release();
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
