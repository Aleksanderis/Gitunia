using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Rebase;

public class QuickRebaseWriterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAMiddleCommit_WhenDropped_ThenItsChangesAreGoneAndTheOthersKeepTheirOrder()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var target = repository.Git("rev-parse", "HEAD~2").Trim();

        var plan = await writer.PlanAsync(location, QuickRebaseKind.Drop, target, Token);
        var result = await writer.RunAsync(location, plan, null, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal(["One", "Three", "Four"], Subjects(repository));
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "two.txt")));
        Assert.Equal(2, plan.LaterCommits);
    }

    [Fact]
    public async Task GivenTheRootCommit_WhenDropped_ThenTheNextCommitBecomesTheRoot()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var root = repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();

        var plan = await writer.PlanAsync(location, QuickRebaseKind.Drop, root, Token);
        var result = await writer.RunAsync(location, plan, null, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal(["Two", "Three", "Four"], Subjects(repository));
    }

    [Fact]
    public async Task GivenACommit_WhenFixedUp_ThenItsChangesJoinTheCommitBeforeWithThatMessage()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var target = repository.Git("rev-parse", "HEAD~1").Trim();
        var treeBefore = repository.Git("rev-parse", "HEAD^{tree}").Trim();

        var plan = await writer.PlanAsync(location, QuickRebaseKind.Fixup, target, Token);
        var result = await writer.RunAsync(location, plan, null, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal("Two", plan.IntoCommit?.Subject);
        Assert.Equal(["One", "Two", "Four"], Subjects(repository));
        Assert.Equal(treeBefore, repository.Git("rev-parse", "HEAD^{tree}").Trim());
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "three.txt")));
    }

    [Fact]
    public async Task GivenTheSecondCommit_WhenFixedUp_ThenItMeldsIntoTheRootCommit()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var target = repository.Git("rev-parse", "HEAD~2").Trim();

        var plan = await writer.PlanAsync(location, QuickRebaseKind.Fixup, target, Token);
        await writer.RunAsync(location, plan, null, Token);

        Assert.Equal(["One", "Three", "Four"], Subjects(repository));
    }

    [Fact]
    public async Task GivenTheRootCommit_WhenPlanningAFixup_ThenItIsRefused()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var root = repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();

        await Assert.ThrowsAsync<RebaseRangeException>(
            () => writer.PlanAsync(location, QuickRebaseKind.Fixup, root, Token));
    }

    [Fact]
    public async Task GivenAMultiLineMessage_WhenReworded_ThenOnlyTheSubjectChanges()
    {
        using var repository = TestRepository.Create()
            .Commit("One", new TestFile("one.txt", "1\n"))
            .Commit("Two\n\nBody line\nMore body", new TestFile("two.txt", "2\n"))
            .Commit("Three", new TestFile("three.txt", "3\n"));
        var (writer, location) = await CreateAsync(repository);
        var target = repository.Git("rev-parse", "HEAD~1").Trim();

        var plan = await writer.PlanAsync(location, QuickRebaseKind.Reword, target, Token);
        var result = await writer.RunAsync(location, plan, "Second", Token);

        Assert.Equal("Two", plan.Subject);
        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal(["One", "Second", "Three"], Subjects(repository));
        Assert.Equal("Second\n\nBody line\nMore body", repository.Git("log", "-1", "--format=%B", "HEAD~1").TrimEnd());
    }

    [Fact]
    public async Task GivenABlankSubject_WhenReworded_ThenNothingChanges()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var head = repository.Head;
        var plan = await writer.PlanAsync(location, QuickRebaseKind.Reword, head, Token);

        await Assert.ThrowsAsync<RebaseRangeException>(() => writer.RunAsync(location, plan, "  ", Token));

        Assert.Equal(head, repository.Head);
    }

    [Fact]
    public async Task GivenTheTipCommit_WhenDropped_ThenTheBranchMovesBackOneCommit()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var plan = await writer.PlanAsync(location, QuickRebaseKind.Drop, repository.Head, Token);

        var result = await writer.RunAsync(location, plan, null, Token);

        Assert.Equal(repository.Git("rev-parse", "HEAD").Trim(), result.Head);
        Assert.Equal(["One", "Two", "Three"], Subjects(repository));
        Assert.Equal(0, plan.LaterCommits);
    }

    [Fact]
    public async Task GivenTheOnlyCommit_WhenPlanningADrop_ThenItIsRefused()
    {
        using var repository = TestRepository.Create().Commit("One", new TestFile("one.txt", "1\n"));
        var (writer, location) = await CreateAsync(repository);

        await Assert.ThrowsAsync<RebaseRangeException>(
            () => writer.PlanAsync(location, QuickRebaseKind.Drop, repository.Head, Token));
    }

    [Fact]
    public async Task GivenAMergeCommit_WhenPlanning_ThenItIsRefused()
    {
        using var repository = TestRepository.Create()
            .Commit("Base", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("side")
            .Commit("Side", new TestFile("side.txt", "side\n"))
            .Checkout("main")
            .Commit("Main", new TestFile("main.txt", "main\n"));
        repository.Git("merge", "--no-ff", "-m", "Merge side", "side");
        var (writer, location) = await CreateAsync(repository);

        await Assert.ThrowsAsync<RebaseRangeException>(
            () => writer.PlanAsync(location, QuickRebaseKind.Drop, repository.Head, Token));
    }

    [Fact]
    public async Task GivenACommitOnAnotherBranch_WhenPlanning_ThenItIsRefused()
    {
        using var repository = Linear();
        repository.CheckoutNewBranch("side").Commit("Side", new TestFile("side.txt", "s\n")).Checkout("main");
        var (writer, location) = await CreateAsync(repository);
        var side = repository.Git("rev-parse", "side").Trim();

        await Assert.ThrowsAsync<RebaseRangeException>(
            () => writer.PlanAsync(location, QuickRebaseKind.Drop, side, Token));
    }

    [Fact]
    public async Task GivenTheHistoryMovedAfterPlanning_WhenRunning_ThenNothingIsRebased()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var target = repository.Git("rev-parse", "HEAD~1").Trim();
        var plan = await writer.PlanAsync(location, QuickRebaseKind.Drop, target, Token);
        repository.Commit("Five", new TestFile("five.txt", "5\n"));
        var head = repository.Head;

        await Assert.ThrowsAsync<RebaseRangeException>(() => writer.RunAsync(location, plan, null, Token));

        Assert.Equal(head, repository.Head);
    }

    [Fact]
    public async Task GivenADropThatConflicts_WhenRunning_ThenTheRebaseStopsAndCanBeAborted()
    {
        using var repository = TestRepository.Create()
            .Commit("One", new TestFile("f.txt", "1\n"))
            .Commit("Two", new TestFile("f.txt", "1\n2\n"))
            .Commit("Three", new TestFile("f.txt", "1\n2\n3\n"));
        var (writer, location) = await CreateAsync(repository);
        var head = repository.Head;
        var target = repository.Git("rev-parse", "HEAD~1").Trim();
        var plan = await writer.PlanAsync(location, QuickRebaseKind.Drop, target, Token);

        var result = await writer.RunAsync(location, plan, null, Token);

        Assert.Equal(OperationOutcome.Conflicted, result.Outcome);
        Assert.Equal(head, result.OriginalHead);
        Assert.Equal(RepositoryOperation.Rebase, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenAMergeAboveTheCommit_WhenReworded_ThenTheMergeSurvivesAndTheMessageChanges()
    {
        using var repository = TestRepository.Create()
            .Commit("Base", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("side")
            .Commit("SideA", new TestFile("a.txt", "a\n"))
            .Checkout("main")
            .Commit("Main", new TestFile("main.txt", "main\n"));
        repository.Git("merge", "--no-ff", "-m", "Merge side", "side");
        var (writer, location) = await CreateAsync(repository);
        var target = repository.Git("rev-parse", "HEAD^2").Trim();
        var tree = repository.Git("rev-parse", "HEAD^{tree}").Trim();

        var plan = await writer.PlanAsync(location, QuickRebaseKind.Reword, target, Token);
        var result = await writer.RunAsync(location, plan, "Renamed", Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal("1", repository.Git("rev-list", "--merges", "--count", "HEAD").Trim());
        Assert.Equal("Renamed", repository.Git("log", "-1", "--format=%s", "HEAD^2").Trim());
        Assert.Equal(tree, repository.Git("rev-parse", "HEAD^{tree}").Trim());
        Assert.Equal(2, plan.LaterCommits);
    }

    [Fact]
    public async Task GivenAMergeAboveTheCommit_WhenDropped_ThenTheMergeSurvivesWithoutTheCommit()
    {
        using var repository = MergedSide();
        var (writer, location) = await CreateAsync(repository);
        var target = repository.Git("rev-parse", "HEAD^2").Trim();

        var plan = await writer.PlanAsync(location, QuickRebaseKind.Drop, target, Token);
        var result = await writer.RunAsync(location, plan, null, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal("1", repository.Git("rev-list", "--merges", "--count", "HEAD").Trim());
        Assert.Equal("SideA", repository.Git("log", "-1", "--format=%s", "HEAD^2").Trim());
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "a.txt")));
    }

    [Fact]
    public async Task GivenAMergeAboveTheCommit_WhenFixedUp_ThenTheMergeSurvivesWithTheTwoCommitsAsOne()
    {
        using var repository = MergedSide();
        var (writer, location) = await CreateAsync(repository);
        var target = repository.Git("rev-parse", "HEAD^2").Trim();
        var tree = repository.Git("rev-parse", "HEAD^{tree}").Trim();

        var plan = await writer.PlanAsync(location, QuickRebaseKind.Fixup, target, Token);
        var result = await writer.RunAsync(location, plan, null, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal("1", repository.Git("rev-list", "--merges", "--count", "HEAD").Trim());
        Assert.Equal("SideA", repository.Git("log", "-1", "--format=%s", "HEAD^2").Trim());
        Assert.Equal("1", repository.Git("rev-list", "--count", "HEAD^2", "^HEAD^1").Trim());
        Assert.Equal(tree, repository.Git("rev-parse", "HEAD^{tree}").Trim());
    }

    [Fact]
    public async Task GivenAFixupWhoseCommitStartsALineAfterAReset_WhenPlanning_ThenItIsRefusedBeforeAnyConfirm()
    {
        using var repository = TestRepository.Create()
            .Commit("Base", new TestFile("base.txt", "base\n"))
            .Commit("Parent", new TestFile("parent.txt", "p\n"))
            .CheckoutNewBranch("side")
            .Commit("SideA", new TestFile("a.txt", "a\n"))
            .Checkout("main")
            .Commit("Main", new TestFile("main.txt", "main\n"));
        repository.Git("merge", "--no-ff", "-m", "Merge side", "side");
        var (writer, location) = await CreateAsync(repository);
        var target = repository.Git("rev-parse", "HEAD^1").Trim();
        var head = repository.Head;

        var exception = await Assert.ThrowsAsync<RebaseRangeException>(
            () => writer.PlanAsync(location, QuickRebaseKind.Fixup, target, Token));

        Assert.Contains("reset", exception.Message, StringComparison.Ordinal);
        Assert.Equal(head, repository.Head);
    }

    private static string[] Subjects(TestRepository repository) =>
        repository.Git("log", "--reverse", "--format=%s").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    // Main and a side branch of SideA and SideB, merged with a merge commit.
    private static TestRepository MergedSide()
    {
        var repository = TestRepository.Create()
            .Commit("Base", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("side")
            .Commit("SideA", new TestFile("a.txt", "a\n"))
            .Commit("SideB", new TestFile("b.txt", "b\n"))
            .Checkout("main")
            .Commit("Main", new TestFile("main.txt", "main\n"));
        repository.Git("merge", "--no-ff", "-m", "Merge side", "side");
        return repository;
    }

    private static TestRepository Linear() =>
        TestRepository.Create()
            .Commit("One", new TestFile("one.txt", "1\n"))
            .Commit("Two", new TestFile("two.txt", "2\n"))
            .Commit("Three", new TestFile("three.txt", "3\n"))
            .Commit("Four", new TestFile("four.txt", "4\n"));

    private static async Task<(QuickRebaseWriter Writer, RepositoryLocation Location)> CreateAsync(
        TestRepository repository)
    {
        var runner = repository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);
        var detector = new RepositoryOperationDetector();
        var interactive = new InteractiveRebaseWriter(
            new OperationRunner(runner, detector),
            runner,
            detector,
            new SequenceEditorCommand(["cp"]),
            _ => null);
        return (new QuickRebaseWriter(runner, interactive), location);
    }
}
