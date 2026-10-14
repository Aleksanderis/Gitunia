using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;
using Xunit;

namespace Gitunia.Git.Tests.Watching;

public class RepositoryWatcherTests
{
    private static readonly GitRunner Runner = new();

    private static readonly RepositoryWatcherOptions FastOptions = new(
        TimeSpan.FromMilliseconds(40),
        TimeSpan.FromMilliseconds(400),
        TimeSpan.FromMilliseconds(60));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenWatchedRepository_WhenExternalCommit_ThenRefsAndIndexAreReported()
    {
        using var repository = TestRepositories.Linear(2);
        var recorder = new ChangeRecorder();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        using var watcher = new RepositoryWatcher(repo.Location, new WatchBudget(), FastOptions, recorder.Record);
        watcher.Start();

        repository.Commit("external", ("later.txt", "x"));

        await recorder.WaitForAsync(RepositoryChangeKinds.Refs | RepositoryChangeKinds.Index, Token);
        Assert.False(watcher.IsPolling);
        Assert.True(watcher.WatcherCount > 0);
    }

    [Fact]
    public async Task GivenWatchedRepository_WhenWorkingTreeFileChanges_ThenWorkingTreeIsReported()
    {
        using var repository = TestRepositories.Linear(1);
        var recorder = new ChangeRecorder();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        using var watcher = new RepositoryWatcher(repo.Location, new WatchBudget(), FastOptions, recorder.Record);
        watcher.Start();

        repository.WriteFile(Path.Combine("sub", "new.txt"), "hello");

        await recorder.WaitForAsync(RepositoryChangeKinds.WorkingTree, Token);
    }

    [Fact]
    public async Task GivenBurstOfWrites_WhenDebounced_ThenFewerCallbacksThanWrites()
    {
        using var repository = TestRepositories.Linear(1);
        var recorder = new ChangeRecorder();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        var options = new RepositoryWatcherOptions(TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        using var watcher = new RepositoryWatcher(repo.Location, new WatchBudget(), options, recorder.Record);
        watcher.Start();

        for (var index = 0; index < 20; index++)
        {
            repository.WriteFile("burst.txt", index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        await recorder.WaitForAsync(RepositoryChangeKinds.WorkingTree, Token);
        Assert.True(recorder.CallCount < 20);
    }

    [Fact]
    public async Task GivenBackgroundTab_WhenChangeHappens_ThenItIsStaleAndReportsOnActivation()
    {
        using var repository = TestRepositories.Linear(1);
        var recorder = new ChangeRecorder();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        using var watcher = new RepositoryWatcher(repo.Location, new WatchBudget(), FastOptions, recorder.Record);
        var stale = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.BecameStale += () => stale.TrySetResult();
        watcher.Start();
        watcher.SetActive(false);

        repository.Commit("while hidden", ("hidden.txt", "x"));

        await stale.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        Assert.True(watcher.IsStale);
        Assert.Equal(0, recorder.CallCount);

        watcher.SetActive(true);

        Assert.Equal(1, recorder.CallCount);
        Assert.True(recorder.Seen.HasFlag(RepositoryChangeKinds.Refs));
        Assert.False(watcher.IsStale);
    }

    [Fact]
    public async Task GivenBackgroundTab_WhenSeveralChangesArrive_ThenBecameStaleIsRaisedOnceAndRearmsAfterActivation()
    {
        using var repository = TestRepositories.Linear(1);
        var recorder = new ChangeRecorder();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        var queued = new List<Action>();
        using var watcher = new RepositoryWatcher(repo.Location, new WatchBudget(), FastOptions, recorder.Record, queued.Add);
        var raised = 0;
        watcher.BecameStale += () => raised++;

        watcher.SetActive(false);
        watcher.ReportChange(RepositoryChangeKinds.Refs);
        watcher.ReportChange(RepositoryChangeKinds.Index);
        watcher.ReportChange(RepositoryChangeKinds.Refs);

        Assert.Single(queued);
        RunQueued(queued);
        Assert.Equal(1, raised);

        watcher.SetActive(true);
        watcher.SetActive(false);
        Assert.Empty(queued);

        watcher.ReportChange(RepositoryChangeKinds.Refs);
        watcher.ReportChange(RepositoryChangeKinds.Refs);

        Assert.Single(queued);
        RunQueued(queued);
        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task GivenActiveTabWithPendingChange_WhenHidden_ThenBecameStaleIsRaisedOnce()
    {
        using var repository = TestRepositories.Linear(1);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        var queued = new List<Action>();
        var slowOptions = new RepositoryWatcherOptions(TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(1));
        using var watcher = new RepositoryWatcher(repo.Location, new WatchBudget(), slowOptions, _ => { }, queued.Add);

        watcher.ReportChange(RepositoryChangeKinds.Refs);
        Assert.Empty(queued);

        watcher.SetActive(false);
        watcher.SetActive(false);

        Assert.Single(queued);
    }

    [Fact]
    public async Task GivenBackgroundTab_WhenNothingChanged_ThenActivationReportsNothing()
    {
        using var repository = TestRepositories.Linear(1);
        var recorder = new ChangeRecorder();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        using var watcher = new RepositoryWatcher(repo.Location, new WatchBudget(), FastOptions, recorder.Record);
        watcher.Start();

        watcher.SetActive(false);
        watcher.SetActive(true);

        Assert.Equal(0, recorder.CallCount);
        Assert.False(watcher.IsStale);
    }

    [Fact]
    public async Task GivenBudgetTooSmall_WhenStarting_ThenTabPollsAndStillSeesCommits()
    {
        using var repository = TestRepositories.Linear(1);
        var recorder = new ChangeRecorder();
        var budget = new WatchBudget(0);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        using var watcher = new RepositoryWatcher(repo.Location, budget, FastOptions, recorder.Record);
        watcher.Start();

        Assert.True(watcher.IsPolling);
        Assert.Equal(0, watcher.WatcherCount);
        Assert.Equal(0, budget.InUse);

        repository.Commit("polled", ("polled.txt", "x"));

        await recorder.WaitForAsync(RepositoryChangeKinds.Head | RepositoryChangeKinds.Refs, Token);
    }

    [Fact]
    public async Task GivenSharedBudget_WhenManyTabsStart_ThenWatchersStayWithinCapAndOthersPoll()
    {
        using var first = TestRepositories.Linear(1);
        using var second = TestRepositories.Linear(1);
        var firstRepo = await Repo.OpenAsync(Runner, first.Path, Token);
        var secondRepo = await Repo.OpenAsync(Runner, second.Path, Token);
        var budget = new WatchBudget(5);
        using var firstWatcher = new RepositoryWatcher(firstRepo.Location, budget, FastOptions, _ => { });
        using var secondWatcher = new RepositoryWatcher(secondRepo.Location, budget, FastOptions, _ => { });

        firstWatcher.Start();
        secondWatcher.Start();

        Assert.True(budget.InUse <= budget.Maximum);
        Assert.False(firstWatcher.IsPolling);
        Assert.True(secondWatcher.IsPolling);
        Assert.Equal(firstWatcher.WatcherCount, budget.InUse);
    }

    [Fact]
    public async Task GivenWatcherDisposed_WhenDisposing_ThenBudgetIsReleased()
    {
        using var repository = TestRepositories.Linear(1);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        var budget = new WatchBudget();
        var watcher = new RepositoryWatcher(repo.Location, budget, FastOptions, _ => { });
        watcher.Start();
        Assert.True(budget.InUse > 0);

        watcher.Dispose();

        Assert.Equal(0, budget.InUse);
    }

    [Fact]
    public async Task GivenWatchedRepositoryWithoutWorktrees_WhenWorktreeIsAdded_ThenWorktreesAreReported()
    {
        using var repository = TestRepositories.Linear(1);
        repository.Branch("topic");
        var recorder = new ChangeRecorder();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        using var watcher = new RepositoryWatcher(repo.Location, new WatchBudget(), FastOptions, recorder.Record);
        watcher.Start();

        repository.AddWorktree("late", "topic");

        await recorder.WaitForAsync(RepositoryChangeKinds.Worktrees, Token);
    }

    [Fact]
    public async Task GivenLinkedWorktreeTab_WhenCommittingThere_ThenTabSeesRefsAndIndex()
    {
        using var repository = TestRepositories.WithWorktree(out var worktreePath);
        var recorder = new ChangeRecorder();
        var repo = await Repo.OpenAsync(Runner, worktreePath, Token);
        using var watcher = new RepositoryWatcher(repo.Location, new WatchBudget(), FastOptions, recorder.Record);
        watcher.Start();

        File.WriteAllText(Path.Combine(worktreePath, "wt.txt"), "x");
        await RunGitAsync(worktreePath, "add", "wt.txt");
        await RunGitAsync(worktreePath, "-c", "user.name=T", "-c", "user.email=t@example.com", "commit", "-q", "-m", "in worktree");

        await recorder.WaitForAsync(RepositoryChangeKinds.Refs | RepositoryChangeKinds.Index, Token);
    }

    [Fact]
    public async Task GivenBareRepository_WhenBranchIsCreated_ThenRefsAreReported()
    {
        using var origin = TestRepository.Create(bare: true);
        var recorder = new ChangeRecorder();
        var repo = await Repo.OpenAsync(Runner, origin.Path, Token);
        using var watcher = new RepositoryWatcher(repo.Location, new WatchBudget(), FastOptions, recorder.Record);
        watcher.Start();

        using var clone = TestRepository.Create();
        clone.Commit("c", ("a.txt", "a"));
        clone.Git("push", "-q", origin.Path, "main");

        await recorder.WaitForAsync(RepositoryChangeKinds.Refs, Token);
    }

    private static void RunQueued(List<Action> queued)
    {
        foreach (var work in queued.ToArray())
        {
            work();
        }

        queued.Clear();
    }

    private static async Task RunGitAsync(string path, params string[] arguments)
    {
        await Runner.RunAsync(path, arguments, null, Token);
    }
}
