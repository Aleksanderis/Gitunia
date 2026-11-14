using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Gitunia.App.Creation;
using Gitunia.App.Picker;
using Gitunia.App.Settings;
using Gitunia.App.Status;
using Gitunia.App.Tests.Tags;
using Gitunia.Core.Config;
using Gitunia.Core.Diff;
using Gitunia.Core.Creation;
using Gitunia.Core.Keymap;
using Gitunia.Core.Picker;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Sync;
using Gitunia.Core.Discard;
using Gitunia.Git.Discard;
using Gitunia.Git.Remotes;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories;
using Gitunia.App.Tabs;
using Gitunia.App.Tabs.Models;
using Gitunia.Core.Worktrees;
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
using Gitunia.App.Loading;
using Gitunia.App.Loading.Diff;
using Gitunia.App.Worktrees;
using Gitunia.Git.IssueTracker;
using Gitunia.Git.IssueTracker.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Watching.Models;
using Xunit;

namespace Gitunia.App.Tests;

// Background work that is not a refresh (#1260): the shutdown must wait for each kind until its process is gone.
public class DisposeWaitsTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan WindDown = TimeSpan.FromMilliseconds(300);

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
        var (effects, tabId, location) = await StartAsync(services);
        effects.Run(new StartBlameEffect(tabId, location, "a.txt", null, 1));
        effects.SessionOf(tabId)!.Details.Select(FakeCommitDetailsReader.Chain(1).Commits[0]);
        Assert.True(issues.WaitStarted(Limit));
        Assert.True(blame.WaitStarted(Limit));
        Assert.True(details.WaitStarted(Limit));

        var started = Stopwatch.GetTimestamp();
        effects.Dispose();
        var elapsed = Stopwatch.GetElapsedTime(started);

        // A shutdown that hit its bound is told apart from a read it did not wait for (#1269).
        Assert.True(elapsed < ShutdownTasks.DefaultWait, $"The shutdown ran into its bound: {elapsed}");
        Assert.Equal(1, issues.WoundDownCount);
        Assert.Equal(1, blame.WoundDownCount);
        Assert.Equal(1, details.WoundDownCount);
    }

    private static async Task<(EffectRunner<TabSession> Effects, TabId TabId, RepositoryLocation Location)> StartAsync(
        GitServices services,
        EffectRunnerOptions<TabSession>? options = null)
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
            (options ?? new EffectRunnerOptions<TabSession>()) with
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
        var (effects, tabId, location) = await StartAsync(services);
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

    [Fact]
    public async Task GivenSyncInitAndDiscardInFlight_WhenTheRunnerIsDisposed_ThenItWaitsForEachCancelledCall()
    {
        var fakes = new FakeGitServices();
        fakes.CommitLog.AddOnTop("root");
        var blocking = new BlockingReader();
        var services = fakes.Services with { Remote = blocking, Creator = blocking, Discarder = blocking };
        var (effects, tabId, location) = await StartAsync(services);
        var entry = new StatusEntry(StatusEntryKind.Ordinary, "a.txt", null, FileChangeKind.Unmodified, FileChangeKind.Modified, null, ConflictKind.None, null);
        effects.Run(new FetchEffect(tabId, location, false));
        effects.Run(new InitRepositoryEffect("somewhere"));
        effects.Run(new DiscardFilesEffect(tabId, location, [entry], 0));
        Assert.True(blocking.WaitStarted(Limit, 3));

        effects.Dispose();

        Assert.Equal(3, blocking.WoundDownCount);
    }

    [Fact]
    public void GivenAPrefetchInFlight_WhenTheStoreIsDisposed_ThenTheSharedTasksWaitForTheCancelledRead()
    {
        var reader = new BlockingReader();
        var tasks = new ShutdownTasks();
        var cache = new DiffCache();
        Assert.True(ObjectId.TryParse(new string('a', 40), out var shown));
        Assert.True(ObjectId.TryParse(new string('b', 40), out var next));
        cache.Add(shown, DiffDocument.Empty);
        var location = FakeStatusReader.LocationOf("prefetch");
        var store = new DiffStore(reader, location, InlineUi.Instance, new DiffStoreOptions(Cache: cache, Tasks: tasks));
        store.Load(shown, null, [next]);
        Assert.True(reader.WaitStarted(Limit));

        store.Dispose();

        Assert.True(tasks.Wait(Limit));
        Assert.Equal(1, reader.WoundDownCount);
    }

    // The work below cannot be cancelled (a file system call): a gate holds it, so what the shutdown waits for is seen.
    [Fact]
    public void GivenSettingsPickerScanAndCloneParentLookupsRunning_WhenTheTasksAreWaitedFor_ThenTheyWaitUntilEachEnds()
    {
        using var gate = new GateDirectories();
        var tasks = new ShutdownTasks();
        var dispatcher = new RecordingDispatcher();
        var token = TestContext.Current.CancellationToken;
        var picker = new PickerEffectHandler(TestGit.Services.Discovery, gate, dispatcher, PickerTimings.Default, tasks);
        var creation = new CreationEffectHandler(new BlockingReader(), gate, dispatcher, tasks);
        var settings = new SettingsEffectHandler(
            new SettingsServices(
                new ConfigWriter(Path.GetTempPath()),
                new SettingsEnvironment(Path.GetTempPath(), gate.Check),
                SettingsRegistry.All,
                DefaultKeymap.Create()),
            dispatcher,
            tasks);
        var commit = new SettingsCommit(SettingsRegistry.All[0], SettingValue.OfFlag(true));
        picker.Handle(new ScanRepositoriesEffect(null, "scan-home"), token);
        creation.Handle(new ResolveCloneParentEffect("source", null, false, null, "parent-home"), token);
        settings.Handle(new CheckSettingsFolderEffect(new SettingsFolderCheck("folder", "folder", commit)), token);
        Assert.True(gate.WaitEntered(Limit, 3));

        Assert.False(tasks.Wait(TimeSpan.Zero));
        gate.Release();

        Assert.True(tasks.Wait(Limit));
    }

    // The fingerprint read is plain file reading that cannot be cancelled; a gate holds it so the wait is seen.
    [Fact]
    public void GivenFingerprintReadsInFlight_WhenThePollerIsDisposed_ThenTheSharedTasksWaitUntilTheReadsEnd()
    {
        using var gate = new GateFingerprint();
        var tasks = new ShutdownTasks();
        var poller = new TabPoller(
            FakeStatusReader.LocationOf("poll"),
            PollOptions.Default,
            InlineUi.Instance,
            new IdleTarget(),
            tasks: tasks,
            fingerprint: gate.Read);
        poller.Start();
        poller.SetActive(true);
        Assert.True(gate.WaitEntered(Limit, 2));

        poller.Dispose();

        Assert.False(tasks.Wait(TimeSpan.Zero));
        gate.Release();
        Assert.True(tasks.Wait(Limit));
    }

    [Fact]
    public void GivenALocateAndADiscoverInFlight_WhenTheShutdownCancelsThem_ThenTheSharedTasksWaitForEachToWindDown()
    {
        using var discovery = new BlockingDiscovery();
        var tasks = new ShutdownTasks();
        using var shutdown = new CancellationTokenSource();
        var worktrees = new WorktreeEffectHandler(TestGit.Services.Worktrees, discovery, new RecordingDispatcher(), tasks);
        var picker = new PickerEffectHandler(discovery, new SlowDirectories(), new RecordingDispatcher(), PickerTimings.Default, tasks);
        worktrees.Locate(new LocateWorktreeEffect(new TabId(1), "somewhere", false), shutdown.Token);
        picker.Handle(new DiscoverRepositoryEffect("elsewhere", 1, null), shutdown.Token);
        Assert.True(discovery.WaitStarted(Limit, 2));

        shutdown.Cancel();

        Assert.True(tasks.Wait(Limit));
        Assert.Equal(2, discovery.WoundDownCount);
    }

    [Fact]
    public void GivenAListingBlockedInTheFileSystem_WhenTheShutdownCancelsIt_ThenTheSharedTasksStillWaitForTheCall()
    {
        using var gate = new GateDirectories();
        var tasks = new ShutdownTasks();
        using var shutdown = new CancellationTokenSource();
        var picker = new PickerEffectHandler(TestGit.Services.Discovery, gate, new RecordingDispatcher(), PickerTimings.Default, tasks);
        var query = Path.GetTempPath();
        picker.Handle(new ListPickerDirectoriesEffect(query, query, query), shutdown.Token);
        Assert.True(gate.WaitEntered(Limit, 1));

        shutdown.Cancel();

        Assert.False(tasks.Wait(TimeSpan.Zero));
        gate.Release();
        Assert.True(tasks.Wait(Limit));
    }

    [Fact]
    public async Task GivenPickerSettingsAndCloneParentLookupsAndAClone_WhenTheRunnerIsDisposed_ThenItWaitsForEachWithinTheSharedBound()
    {
        using var slow = new SlowDirectories();
        var creator = new BlockingReader();
        var fakes = new FakeGitServices();
        var (effects, _, _) = await StartAsync(
            fakes.Services with { Creator = creator },
            new EffectRunnerOptions<TabSession>
            {
                Picker = new PickerServices(slow, PickerTimings.Default),
                Settings = new SettingsServices(
                    new ConfigWriter(Path.GetTempPath()),
                    new SettingsEnvironment(Path.GetTempPath(), slow.DirectoryExists),
                    SettingsRegistry.All,
                    DefaultKeymap.Create()),
            });
        var commit = new SettingsCommit(SettingsRegistry.All[0], SettingValue.OfFlag(true));
        effects.Run(new ScanRepositoriesEffect(null, "scan-home"));
        effects.Run(new ResolveCloneParentEffect("source", null, false, null, "parent-home"));
        effects.Run(new CheckSettingsFolderEffect(new SettingsFolderCheck("folder", "folder", commit)));
        effects.Run(new CloneRepositoryEffect(new CloneRequest("source", "target")));
        Assert.True(slow.WaitEntered(Limit, 3));
        Assert.True(creator.WaitStarted(Limit));

        var started = Stopwatch.GetTimestamp();
        effects.Dispose();
        var elapsed = Stopwatch.GetElapsedTime(started);

        Assert.True(elapsed < ShutdownTasks.DefaultWait, $"The shutdown ran into its bound: {elapsed}");
        Assert.Equal(["folder", "parent-home", "scan-home"], slow.FinishedMarkers);
        Assert.Equal(1, creator.WoundDownCount);
    }

    [Fact]
    public void GivenARunningClone_WhenCancelAndWaitHasNoTimeLeft_ThenItReturnsFalseWithoutWaitingAndLaterWaitsForTheCleanUp()
    {
        var creator = new BlockingReader();
        using var files = new SlowDirectories();
        var handler = new CreationEffectHandler(creator, files, new RecordingDispatcher());
        using var shutdown = new CancellationTokenSource();
        handler.Handle(new CloneRepositoryEffect(new CloneRequest("source", "target")), shutdown.Token);
        Assert.True(creator.WaitStarted(Limit));

        Assert.False(handler.CancelAndWait(TimeSpan.Zero));
        Assert.True(handler.CancelAndWait(Limit));

        Assert.Equal(1, creator.WoundDownCount);
    }

    private sealed class IdleTarget : IPollTarget
    {
        public bool CanPoll => true;

        public void Request(RepositoryChangeKinds kinds)
        {
        }
    }

    private sealed class GateFingerprint : IDisposable
    {
        private readonly SemaphoreSlim _entered = new(0);
        private readonly ManualResetEventSlim _open = new(false);

        public void Dispose() => _open.Set();

        public bool WaitEntered(TimeSpan timeout, int count)
        {
            for (var index = 0; index < count; index++)
            {
                if (!_entered.Wait(timeout))
                {
                    return false;
                }
            }

            return true;
        }

        public void Release() => _open.Set();

        public RepositoryFingerprintGroups Read(RepositoryLocation location)
        {
            _entered.Release();
            _open.Wait(Limit);

            return default;
        }
    }

    // A file system call that takes a while and cannot be cancelled: it counts how many calls ran to the end.
    private sealed class SlowDirectories : IDirectoryLister, IDisposable
    {
        private static readonly string[] Markers = ["scan-home", "parent-home", "folder"];

        private readonly SemaphoreSlim _entered = new(0);
        private readonly ConcurrentDictionary<string, bool> _finished = new();

        public string[] FinishedMarkers => [.. Markers.Where(_finished.ContainsKey).Order()];

        public void Dispose() => _entered.Dispose();

        public bool WaitEntered(TimeSpan timeout, int count)
        {
            for (var index = 0; index < count; index++)
            {
                if (!_entered.Wait(timeout))
                {
                    return false;
                }
            }

            return true;
        }

        // Only the first call of each lookup (named by a marker in its path) is slow, so each one counts once.
        public bool DirectoryExists(string directory)
        {
            var marker = Array.Find(Markers, directory.Contains);
            if (marker is null || !_finished.TryAdd(marker + ":started", true))
            {
                return false;
            }

            _entered.Release();
            Thread.Sleep(WindDown);
            _finished.TryAdd(marker, true);

            return false;
        }

        public IReadOnlyList<string> ListDirectories(string directory) => [];

        public bool IsRepository(string directory) => false;
    }

    // Plays a discovery whose git process takes a while to die after the cancel.
    private sealed class BlockingDiscovery : IRepositoryDiscovery, IDisposable
    {
        private readonly SemaphoreSlim _started = new(0);
        private int _woundDown;

        public int WoundDownCount => Volatile.Read(ref _woundDown);

        public void Dispose() => _started.Dispose();

        public bool WaitStarted(TimeSpan timeout, int count)
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

        public async Task<RepositoryLocation> DiscoverAsync(string path, CancellationToken cancellationToken)
        {
            _started.Release();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // On a thread of its own, not a pool timer: the shutdown blocks pool threads while it waits (#1269).
                await Task.Factory.StartNew(
                    () => Thread.Sleep(WindDown),
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
                Interlocked.Increment(ref _woundDown);
                throw;
            }

            throw new InvalidOperationException("unreachable");
        }
    }

    private sealed class GateDirectories : IDirectoryLister, IDisposable
    {
        private readonly SemaphoreSlim _entered = new(0);
        private readonly ManualResetEventSlim _open = new(false);

        public void Dispose()
        {
            _open.Set();
            _entered.Dispose();
            _open.Dispose();
        }

        public bool WaitEntered(TimeSpan timeout, int count)
        {
            for (var index = 0; index < count; index++)
            {
                if (!_entered.Wait(timeout))
                {
                    return false;
                }
            }

            return true;
        }

        public void Release() => _open.Set();

        public bool Check(string directory) => DirectoryExists(directory);

        public bool DirectoryExists(string directory)
        {
            _entered.Release();
            _open.Wait(Limit);

            return false;
        }

        public IReadOnlyList<string> ListDirectories(string directory)
        {
            _entered.Release();
            _open.Wait(Limit);

            return [];
        }

        public bool IsRepository(string directory) => false;
    }

    // Plays a read whose process takes a while to die: it blocks until cancelled, then needs time to wind down.
    private sealed class BlockingReader : IStatusReader, IBlameReader, ICommitDetailsReader, IIssueTrackerReader, IDiffReader, ICommitTreeReader, IBlobReader, IFileHistoryReader, ICommitWriter, IIndexWriter, IRemoteWriter, IRepositoryCreator, IWorkingTreeDiscarder, IDisposable
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

        public async IAsyncEnumerable<DiffFile> ReadCommitAgainstFirstParentAsync(
            RepositoryLocation location,
            ObjectId commit,
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

        public Task FetchAsync(RepositoryLocation location, FetchRequest request, IProgress<RemoteProgress>? progress, CancellationToken cancellationToken) => BlockAsync(cancellationToken);

        public async Task<PullOutcome> PullAsync(RepositoryLocation location, IProgress<RemoteProgress>? progress, CancellationToken cancellationToken)
        {
            await BlockAsync(cancellationToken);

            return PullOutcome.UpToDate;
        }

        public Task PushAsync(RepositoryLocation location, PushRequest request, IProgress<RemoteProgress>? progress, CancellationToken cancellationToken) => BlockAsync(cancellationToken);

        public Task DeleteRemoteBranchAsync(RepositoryLocation location, string remote, string branch, string expectedHash, CancellationToken cancellationToken) => BlockAsync(cancellationToken);

        public async Task<RepositoryLocation> CloneAsync(CloneRequest request, IProgress<RemoteProgress>? progress, CancellationToken cancellationToken)
        {
            await BlockAsync(cancellationToken);

            throw new InvalidOperationException("unreachable");
        }

        public async Task<InitResult> InitAsync(string directory, CancellationToken cancellationToken)
        {
            await BlockAsync(cancellationToken);

            throw new InvalidOperationException("unreachable");
        }

        public bool CanDeleteInvalidNames => true;

        public Task RestoreAsync(RepositoryLocation location, IReadOnlyList<string> paths, CancellationToken cancellationToken) => BlockAsync(cancellationToken);

        public Task DeleteUntrackedAsync(RepositoryLocation location, IReadOnlyList<string> paths, CancellationToken cancellationToken) => BlockAsync(cancellationToken);

        public Task ApplyReversePatchAsync(RepositoryLocation location, string patch, CancellationToken cancellationToken) => BlockAsync(cancellationToken);

        // The wind-down runs on its own thread, not on a timer and the pool: the shutdown blocks pool threads while it
        // waits, and a starved pool (a small Windows runner) may delay a pool timer past the shutdown bound (#1269,
        // suspected, not reproduced). The sleep only plays a slow process death; the asserts wait on Dispose returning.
        private static Task WindDownAsync()
        {
            var finished = new TaskCompletionSource();
            var thread = new Thread(() =>
            {
                Thread.Sleep(WindDown);
                finished.SetResult();
            })
            {
                IsBackground = true,
            };
            thread.Start();

            return finished.Task;
        }

        private async Task BlockAsync(CancellationToken cancellationToken)
        {
            _started.Release();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await WindDownAsync();
                Interlocked.Increment(ref _woundDown);

                throw;
            }
        }
    }
}
