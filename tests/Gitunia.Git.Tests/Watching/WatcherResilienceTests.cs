using System.Collections.Concurrent;
using System.Text;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;
using Xunit;

namespace Gitunia.Git.Tests.Watching;

/// <summary>Watching that survives a buffer overflow, drops ignored directories and starts without touching the disk (#1317).</summary>
public class WatcherResilienceTests
{
    private static readonly GitRunner Runner = IsolatedGit.CreateRunner();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan QuietWindow = TimeSpan.FromMilliseconds(600);

    private static readonly RepositoryWatcherOptions FastOptions = new(
        TimeSpan.FromMilliseconds(40),
        TimeSpan.FromMilliseconds(400),
        TimeSpan.FromMilliseconds(60),
        WatchDirectoriesIndividually: false,
        WatcherRetryInterval: TimeSpan.FromMilliseconds(100));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenWatcher_WhenStartedAsync_ThenWatchersAreCreatedAndChangesAreReported()
    {
        using var testRepository = TestRepositories.Linear(1);
        var recorder = new ChangeRecorder();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        using var watcher = new RepositoryWatcher(location, new WatchBudget(), FastOptions);
        watcher.Changed += recorder.Record;

        await watcher.StartAsync();

        Assert.True(watcher.WatcherCount > 0);
        Assert.False(watcher.IsPolling);
        testRepository.WriteFile("after-start.txt", "x");
        await recorder.WaitForAsync(RepositoryChangeKinds.WorkingTree, Token);
    }

    [Fact]
    public async Task GivenWatcher_WhenBufferOverflows_ThenEverythingIsReportedOnceAndWatchingContinues()
    {
        using var testRepository = TestRepositories.Linear(1);
        var recorder = new ChangeRecorder();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        using var watcher = new RepositoryWatcher(location, new WatchBudget(), FastOptions);
        watcher.Changed += recorder.Record;
        watcher.Start();
        var watchers = watcher.WatcherCount;

        watcher.ReportWatcherError(new InternalBufferOverflowException(), fromWorkingTree: true);

        await recorder.WaitForAsync(RepositoryChangeKinds.WorkingTree, Token);
        Assert.Equal(RepositoryChangeKinds.WorkingTree, recorder.Seen);
        Assert.False(watcher.IsPolling);
        Assert.Equal(watchers, watcher.WatcherCount);

        testRepository.WriteFile("after-overflow.txt", "x");
        await recorder.WaitForAsync(RepositoryChangeKinds.WorkingTree, Token);
    }

    [Fact]
    public async Task GivenWatcher_WhenGitDirectoryBufferOverflows_ThenGitStateIsReportedAndWatchingContinues()
    {
        using var testRepository = TestRepositories.Linear(1);
        var recorder = new ChangeRecorder();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        using var watcher = new RepositoryWatcher(location, new WatchBudget(), FastOptions);
        watcher.Changed += recorder.Record;
        watcher.Start();

        watcher.ReportWatcherError(new InternalBufferOverflowException());

        await recorder.WaitForAsync(RepositoryChangeKinds.Head | RepositoryChangeKinds.Refs, Token);
        Assert.False(watcher.IsPolling);
    }

    [Fact]
    public async Task GivenWatcher_WhenFatalErrorHappens_ThenItPollsAndWatchesAgainLater()
    {
        using var testRepository = TestRepositories.Linear(1);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var budget = new WatchBudget();
        using var watcher = new RepositoryWatcher(location, budget, FastOptions);
        watcher.Start();
        var watchers = watcher.WatcherCount;

        watcher.ReportWatcherError(new IOException("handle lost"));

        Assert.True(watcher.IsPolling);
        Assert.Equal(0, watcher.WatcherCount);
        await WaitForAsync(() => !watcher.IsPolling && watcher.WatcherCount == watchers);
        Assert.Equal(watchers, watcher.WatcherCount);
        Assert.Equal(watchers, budget.InUse);
    }

    [Fact]
    public async Task GivenIgnoredDirectory_WhenFilesChangeInsideIt_ThenNothingIsReportedButOtherFilesAre()
    {
        using var testRepository = TestRepositories.Linear(1);
        var ignored = Path.Combine(testRepository.WorkingTreePath, "node_modules");
        Directory.CreateDirectory(Path.Combine(ignored, "pkg"));
        var recorder = new ChangeRecorder();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        using var watcher = new RepositoryWatcher(
            location,
            new WatchBudget(),
            FastOptions,
            directoryLister: new IgnoredLister(ignored));
        watcher.Changed += recorder.Record;
        watcher.Start();
        await WaitForAsync(() => watcher.IgnoredDirectoryCount > 0);

        testRepository.WriteFile(Path.Combine("node_modules", "pkg", "index.js"), "x");
        testRepository.WriteFile(Path.Combine("node_modules", "top.js"), "x");
        await Task.Delay(QuietWindow, Token);

        Assert.Equal(0, recorder.CallCount);

        testRepository.WriteFile("tracked.txt", "x");
        await recorder.WaitForAsync(RepositoryChangeKinds.WorkingTree, Token);
    }

    [Fact]
    public async Task GivenGitignoreEdit_WhenWatchingRecursively_ThenTheIgnoredSetIsReloadedAndDropsItsEvents()
    {
        using var testRepository = TestRepositories.Linear(1);
        var ignored = Path.Combine(testRepository.WorkingTreePath, "build");
        Directory.CreateDirectory(ignored);
        var recorder = new ChangeRecorder();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var lister = new SwitchableLister();
        using var watcher = new RepositoryWatcher(location, new WatchBudget(), FastOptions, directoryLister: lister);
        watcher.Changed += recorder.Record;
        watcher.Start();
        await WaitForAsync(() => lister.CallCount == 1);
        lister.Ignored = [ignored];

        testRepository.WriteFile(".gitignore", "build/\n");
        await WaitForAsync(() => watcher.IgnoredDirectoryCount == 1);
        await Task.Delay(QuietWindow, Token);
        var before = recorder.CallCount;
        testRepository.WriteFile(Path.Combine("build", "out.bin"), "x");
        await Task.Delay(QuietWindow, Token);

        Assert.Equal(before, recorder.CallCount);
    }

    [Fact]
    public async Task GivenBurstOfGitignoreEdits_WhenAListingIsRunning_ThenTheyCollapseIntoOneMoreListing()
    {
        using var testRepository = TestRepositories.Linear(1);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        using var lister = new SwitchableLister { Blocking = true };
        using var watcher = new RepositoryWatcher(location, new WatchBudget(), FastOptions, directoryLister: lister);
        watcher.Start();
        await WaitForAsync(() => lister.CallCount == 1);

        for (var edit = 0; edit < 5; edit++)
        {
            testRepository.WriteFile(".gitignore", $"edit{edit}\n");
            await Task.Delay(30, Token);
        }

        lister.Release.Set();
        await WaitForAsync(() => lister.CallCount == 2);
        await Task.Delay(QuietWindow, Token);

        Assert.Equal(2, lister.CallCount);
    }

    [Fact]
    public async Task GivenSlowFingerprint_WhenStartFallsBackToPolling_ThenStartDoesNotComputeItOnTheCallingThread()
    {
        using var testRepository = TestRepositories.Linear(1);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        using var reader = new SlowReader();

        using var watcher = new RepositoryWatcher(
            location,
            new WatchBudget(0),
            FastOptions,
            fingerprintReader: reader);

        watcher.Start();

        // Returned while the fingerprint is still blocked, and the lock is free meanwhile.
        Assert.True(watcher.IsPolling);
        await WaitForAsync(() => !reader.CallerThreads.IsEmpty);

        reader.Release.Set();
    }

    [Fact]
    public void GivenDirectoryOutput_WhenParsingIgnored_ThenOnlyDirectoryEntriesAreListed()
    {
        var output = Encoding.UTF8.GetBytes("node_modules/\0src/bin/\0notes.log\0my dir/obj/\0");

        var directories = WorkingTreeDirectoryParser.ParseDirectories("root", output);

        Assert.Equal(
            new[] { "node_modules", Path.Combine("src", "bin"), Path.Combine("my dir", "obj") }
                .Select(relative => Path.Combine("root", relative)),
            directories);
    }

    [Fact]
    public async Task GivenIgnoredTree_WhenListingIgnored_ThenTopLevelIgnoredDirectoriesAreReturned()
    {
        using var testRepository = TestRepositories.Linear(1);
        testRepository.Commit(
            "ignore",
            new TestFile(".gitignore", "node_modules/\nbin/\n"),
            new TestFile(Path.Combine("src", "keep.txt"), "x"));
        testRepository.WriteFile(Path.Combine("node_modules", "pkg", "a.js"), "x");
        testRepository.WriteFile(Path.Combine("src", "bin", "b.dll"), "x");

        var directories = await new WorkingTreeDirectoryLister(Runner).ListIgnoredAsync(testRepository.WorkingTreePath, Token);

        Assert.NotNull(directories);
        Assert.Equal(
            new[] { Path.Combine("node_modules"), Path.Combine("src", "bin") }
                .Select(relative => Path.Combine(testRepository.WorkingTreePath, relative))
                .Order(),
            directories.Order());
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + WaitLimit;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Condition not reached in time.");
            await Task.Delay(20, Token);
        }
    }

    private sealed class IgnoredLister(params string[] ignored) : IWorkingTreeDirectoryLister
    {
        public Task<IReadOnlyList<string>?> ListAsync(string workingTreePath, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>?>(null);

        public Task<IReadOnlyList<string>?> ListIgnoredAsync(string workingTreePath, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>?>(ignored);
    }

    private sealed class SwitchableLister : IWorkingTreeDirectoryLister, IDisposable
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public volatile bool Blocking;

        public IReadOnlyList<string> Ignored { get; set; } = [];

        public ManualResetEventSlim Release { get; } = new();

        public Task<IReadOnlyList<string>?> ListAsync(string workingTreePath, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>?>(null);

        public async Task<IReadOnlyList<string>?> ListIgnoredAsync(
            string workingTreePath,
            CancellationToken cancellationToken)
        {
            var current = Ignored;
            Interlocked.Increment(ref _callCount);
            if (Blocking)
            {
                await Task.Run(() => Release.Wait(cancellationToken), cancellationToken);
            }

            return current;
        }

        public void Dispose() => Release.Dispose();
    }

    private sealed class SlowReader: IRepositoryFingerprintReader, IDisposable
    {
        private readonly DiskFingerprintReader _inner = new();

        public ManualResetEventSlim Release { get; } = new();

        public ConcurrentBag<int> CallerThreads { get; } = [];

        public RepositoryFingerprintGroups Read(RepositoryLocation location)
        {
            CallerThreads.Add(Environment.CurrentManagedThreadId);
            Release.Wait(Token);

            return _inner.Read(location);
        }

        public void Dispose() => Release.Dispose();
    }
}
