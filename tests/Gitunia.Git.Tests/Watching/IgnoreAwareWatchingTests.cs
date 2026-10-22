using System.Text;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;
using Xunit;

namespace Gitunia.Git.Tests.Watching;

public class IgnoreAwareWatchingTests
{
    private static readonly GitRunner Runner = IsolatedGit.CreateRunner();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly WorkingTreeDirectoryLister Lister = new(Runner);
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(20);

    private static readonly RepositoryWatcherOptions FastOptions = new(
        TimeSpan.FromMilliseconds(40),
        TimeSpan.FromMilliseconds(400),
        TimeSpan.FromMilliseconds(60));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void GivenFilePaths_WhenParsing_ThenEveryParentDirectoryAppearsOnceAndRootIsExcluded()
    {
        var output = Encoding.UTF8.GetBytes("top.txt\0src/a/one.txt\0src/a/two.txt\0docs/my notes/n.txt\0");

        var directories = WorkingTreeDirectoryParser.Parse(Path.Combine("root"), output);

        Assert.Equal(
            new[] { "docs", Path.Combine("docs", "my notes"), "src", Path.Combine("src", "a") }
                .Select(relative => Path.Combine("root", relative))
                .Order(),
            directories.Order());
    }

    [Fact]
    public void GivenNoOutput_WhenParsing_ThenNoDirectories()
    {
        Assert.Empty(WorkingTreeDirectoryParser.Parse("root", []));
    }

    [Fact]
    public async Task GivenIgnoredDirectory_WhenListing_ThenOnlyDirectoriesWithNonIgnoredFilesAreListed()
    {
        using var testRepository = CreateRepositoryWithIgnoredTree();

        var directories = await Lister.ListAsync(testRepository.WorkingTreePath, Token);

        Assert.NotNull(directories);
        Assert.Equal(
            ExpectedDirectories(testRepository, "src", Path.Combine("src", "a")).Order(),
            directories.Order());
    }

    [Fact]
    public async Task GivenNotARepository_WhenListing_ThenNull()
    {
        var directory = Directory.CreateTempSubdirectory("gitunia-lister");
        try
        {
            Assert.Null(await Lister.ListAsync(directory.FullName, Token));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GivenIgnoredDirectory_WhenWatching_ThenOnlyNonIgnoredDirectoriesGetWatchers()
    {
        using var testRepository = CreateRepositoryWithIgnoredTree();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var budget = new WatchBudget();
        using var watcher = new RepositoryWatcher(location, budget, FastOptions, _ => { }, directoryLister: Lister);

        watcher.Start();
        await WaitForAsync(() => watcher.WorkingTreeDirectoryCount == 2);

        Assert.False(watcher.IsPolling);
        Assert.Equal(watcher.WatcherCount, budget.InUse);
    }

    [Fact]
    public async Task GivenWatchedSubdirectory_WhenFileChanges_ThenWorkingTreeIsReported()
    {
        using var testRepository = CreateRepositoryWithIgnoredTree();
        var recorder = new ChangeRecorder();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        using var watcher = CreateWatcher(location, recorder.Record);
        watcher.Start();
        await WaitForAsync(() => watcher.WorkingTreeDirectoryCount == 2);

        testRepository.WriteFile(Path.Combine("src", "a", "x.txt"), "changed");

        await recorder.WaitForAsync(RepositoryChangeKinds.WorkingTree, Token);
    }

    [Fact]
    public async Task GivenNewNestedDirectories_WhenFileIsCreatedDeepInside_ThenItIsReported()
    {
        using var testRepository = CreateRepositoryWithIgnoredTree();
        var recorder = new ChangeRecorder();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        using var watcher = CreateWatcher(location, recorder.Record);
        watcher.Start();
        await WaitForAsync(() => watcher.WorkingTreeDirectoryCount == 2);

        Directory.CreateDirectory(Path.Combine(testRepository.WorkingTreePath, "fresh", "deeper"));
        await WaitForAsync(() => watcher.WorkingTreeDirectoryCount == 4);
        await recorder.WaitForAsync(RepositoryChangeKinds.WorkingTree, Token);
        var callsBefore = recorder.CallCount;
        testRepository.WriteFile(Path.Combine("fresh", "deeper", "file.txt"), "x");

        await WaitForAsync(() => recorder.CallCount > callsBefore);
    }

    [Fact]
    public async Task GivenNewIgnoredDirectoryWithFiles_WhenRescanned_ThenItsWatchersAreDropped()
    {
        using var testRepository = CreateRepositoryWithIgnoredTree();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        using var watcher = CreateWatcher(location, _ => { });
        watcher.Start();
        await WaitForAsync(() => watcher.WorkingTreeDirectoryCount == 2);

        testRepository.WriteFile(".gitignore", "ignored/\ntmpcache/\n");
        testRepository.WriteFile(Path.Combine("ignored", "built", "deep", "blob.bin"), "x");
        Directory.Move(
            Path.Combine(testRepository.WorkingTreePath, "ignored", "built"),
            Path.Combine(testRepository.WorkingTreePath, "tmpcache"));
        testRepository.WriteFile(Path.Combine("src", "b", "late.txt"), "x");

        await WaitForAsync(() => watcher.WorkingTreeDirectoryCount == 3);
        await Task.Delay(300, Token);
        Assert.Equal(3, watcher.WorkingTreeDirectoryCount);
    }

    [Fact]
    public async Task GivenWatchedDirectoryRemoved_WhenRescanned_ThenItsWatcherIsReleased()
    {
        using var testRepository = CreateRepositoryWithIgnoredTree();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        using var watcher = CreateWatcher(location, _ => { });
        watcher.Start();
        await WaitForAsync(() => watcher.WorkingTreeDirectoryCount == 2);

        testRepository.Git("rm", "-r", "-q", "-f", "src");

        await WaitForAsync(() => watcher.WorkingTreeDirectoryCount == 0);
    }

    [Fact]
    public async Task GivenMoreDirectoriesThanTheLimit_WhenStarting_ThenTabPollsAndStillReportsChanges()
    {
        using var testRepository = CreateRepositoryWithIgnoredTree();
        var recorder = new ChangeRecorder();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var options = FastOptions with { MaximumWorkingTreeDirectories = 1 };
        var budget = new WatchBudget();
        using var watcher = new RepositoryWatcher(location, budget, options, recorder.Record, directoryLister: Lister);

        watcher.Start();
        await WaitForAsync(() => watcher.IsPolling);

        Assert.Equal(0, watcher.WorkingTreeDirectoryCount);
        Assert.Equal(0, budget.InUse);
        testRepository.WriteFile(Path.Combine("src", "a", "x.txt"), "changed");
        await recorder.WaitForAsync(RepositoryChangeKinds.WorkingTree, Token);
    }

    [Fact]
    public async Task GivenBigNewIgnoredTree_WhenItAppears_ThenTabKeepsWatchingInsteadOfPolling()
    {
        using var testRepository = CreateRepositoryWithIgnoredTree();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var options = FastOptions with { MaximumWorkingTreeDirectories = 6 };
        using var watcher = new RepositoryWatcher(
            location,
            new WatchBudget(),
            options,
            _ => { },
            directoryLister: Lister);
        watcher.Start();
        await WaitForAsync(() => watcher.WorkingTreeDirectoryCount == 2);
        testRepository.WriteFile(".gitignore", "ignored/\nmodules/\n");
        var staging = Path.Combine(testRepository.WorkingTreePath, "ignored", "staging");
        for (var index = 0; index < 40; index++)
        {
            Directory.CreateDirectory(Path.Combine(staging, $"package{index}", "lib"));
            File.WriteAllText(Path.Combine(staging, $"package{index}", "lib", "index.js"), "x");
        }

        Directory.Move(staging, Path.Combine(testRepository.WorkingTreePath, "modules"));

        testRepository.WriteFile(Path.Combine("src", "b", "s.txt"), "x");

        await WaitForAsync(() => watcher.WorkingTreeDirectoryCount == 3 && !watcher.IsPolling);
        Assert.False(watcher.IsPolling);
    }

    [Fact]
    public async Task GivenListingFails_WhenStarting_ThenWorkingTreeIsWatchedRecursively()
    {
        using var testRepository = CreateRepositoryWithIgnoredTree();
        var recorder = new ChangeRecorder();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var budget = new WatchBudget();
        using var watcher = new RepositoryWatcher(
            location,
            budget,
            FastOptions,
            recorder.Record,
            directoryLister: new FailingLister());
        watcher.Start();
        var watchersBefore = watcher.WatcherCount;
        await WaitForAsync(() => watcher.WatcherCount > watchersBefore);

        testRepository.WriteFile(Path.Combine("src", "a", "x.txt"), "changed");

        await recorder.WaitForAsync(RepositoryChangeKinds.WorkingTree, Token);
        Assert.Equal(0, watcher.WorkingTreeDirectoryCount);
    }

    [Fact]
    public async Task GivenListingStillRunning_WhenDisposed_ThenNoDirectoryWatcherIsInstalled()
    {
        using var testRepository = CreateRepositoryWithIgnoredTree();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var lister = new FixedLister(Path.Combine(testRepository.WorkingTreePath, "src"));
        var watcher = new RepositoryWatcher(
            location,
            new WatchBudget(),
            FastOptions,
            _ => { },
            directoryLister: lister);
        watcher.Start();
        await lister.Started.Task.WaitAsync(WaitLimit, Token);

        watcher.Dispose();
        lister.Release.SetResult();
        await Task.Delay(200, Token);

        Assert.Equal(0, watcher.WorkingTreeDirectoryCount);
    }

    [Fact]
    public async Task GivenListingWithDuplicatePaths_WhenApplied_ThenEachDirectoryIsWatchedOnce()
    {
        using var testRepository = CreateRepositoryWithIgnoredTree();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var source = Path.Combine(testRepository.WorkingTreePath, "src");
        var lister = new FixedLister(source, source, Path.Combine(source, "a"));
        using var watcher = new RepositoryWatcher(
            location,
            new WatchBudget(),
            FastOptions,
            _ => { },
            directoryLister: lister);
        watcher.Start();
        await lister.Started.Task.WaitAsync(WaitLimit, Token);

        lister.Release.SetResult();

        await WaitForAsync(() => watcher.WorkingTreeDirectoryCount > 0);
        Assert.Equal(2, watcher.WorkingTreeDirectoryCount);
    }

    private static RepositoryWatcher CreateWatcher(
        RepositoryLocation location,
        Action<RepositoryChangeKinds> onChanged) =>
        new(location, new WatchBudget(), FastOptions, onChanged, directoryLister: Lister);

    private static TestRepository CreateRepositoryWithIgnoredTree()
    {
        var testRepository = TestRepositories.Linear(1);
        testRepository.Commit(
            "tree",
            new TestFile(".gitignore", "ignored/\n"),
            new TestFile(Path.Combine("src", "a", "x.txt"), "x"));
        testRepository.WriteFile(Path.Combine("ignored", "deep", "y.txt"), "y");
        testRepository.WriteFile(Path.Combine("ignored", "other", "z.txt"), "z");

        return testRepository;
    }

    private static IEnumerable<string> ExpectedDirectories(TestRepository testRepository, params string[] relative) =>
        relative.Select(path => Path.Combine(testRepository.WorkingTreePath, path));

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + WaitLimit;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Condition not reached in time.");
            await Task.Delay(20, Token);
        }
    }

    private sealed class FixedLister(params string[] directories) : IWorkingTreeDirectoryLister
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyList<string>?> ListAsync(
            string workingTreePath,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);

            return directories;
        }
    }

    private sealed class FailingLister: IWorkingTreeDirectoryLister
    {
        public Task<IReadOnlyList<string>?> ListAsync(string workingTreePath, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>?>(null);
    }
}
