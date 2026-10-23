using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Rebase;

public class InteractiveRebaseWriterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAFourCommitHistory_WhenReadingFromTheRoot_ThenAllCommitsComeOldestFirst()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);

        var range = await writer.ReadRangeAsync(location, RebaseBase.Root, Token);

        Assert.Equal(["One", "Two", "Three", "Four"], range.Select(commit => commit.Subject));
        Assert.Equal(repository.Head, range[^1].Hash);
    }

    [Fact]
    public async Task GivenACommitBase_WhenReadingTheRange_ThenOnlyTheCommitsAfterItAreListed()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);

        var range = await writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~2"), Token);

        Assert.Equal(["Three", "Four"], range.Select(commit => commit.Subject));
    }

    [Fact]
    public async Task GivenTwoIndependentCommits_WhenTheirOrderIsSwapped_ThenHistoryHasTheNewOrderAndTheSameFiles()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var range = await writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~2"), Token);
        var treeBefore = repository.Git("rev-parse", "HEAD^{tree}").Trim();

        var result = await writer.RebaseAsync(
            location,
            RebaseBase.OntoCommit("HEAD~2"),
            [Entry(range[1], RebaseTodoAction.Pick), Entry(range[0], RebaseTodoAction.Pick)],
            Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal(["One", "Two", "Four", "Three"], Subjects(repository));
        Assert.Equal(treeBefore, repository.Git("rev-parse", "HEAD^{tree}").Trim());
        Assert.Equal(repository.Head, result.Head);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenAReword_WhenRebasing_ThenOnlyThatMessageChangesAndItKeepsTheTextAsTyped()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var range = await writer.ReadRangeAsync(location, RebaseBase.Root, Token);
        const string message = "Better 'two'\n\n# not a comment\nBody with \"quotes\" and $HOME\n";

        var result = await writer.RebaseAsync(
            location,
            RebaseBase.Root,
            [
                Entry(range[0], RebaseTodoAction.Pick),
                Entry(range[1], RebaseTodoAction.Reword, message),
                Entry(range[2], RebaseTodoAction.Pick),
                Entry(range[3], RebaseTodoAction.Pick),
            ],
            Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal(["One", "Better 'two'", "Three", "Four"], Subjects(repository));
        var body = repository.Git("log", "--format=%B", "--grep=Better", "-1");
        Assert.Equal(message.TrimEnd(), body.TrimEnd());
        Assert.False(Directory.Exists(Path.Combine(location.GitDirectory, "gitunia-rebase")));
    }

    [Fact]
    public async Task GivenASquash_WhenRebasing_ThenTwoCommitsBecomeOneWithBothMessages()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var range = await writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~2"), Token);

        await writer.RebaseAsync(
            location,
            RebaseBase.OntoCommit("HEAD~2"),
            [Entry(range[0], RebaseTodoAction.Pick), Entry(range[1], RebaseTodoAction.Squash)],
            Token);

        Assert.Equal(["One", "Two", "Three"], Subjects(repository));
        var message = repository.Git("log", "-1", "--format=%B");
        Assert.Contains("Four", message, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "four.txt")));
    }

    [Fact]
    public async Task GivenAFixup_WhenRebasing_ThenTheMessageOfTheEarlierCommitIsKept()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var range = await writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~2"), Token);

        await writer.RebaseAsync(
            location,
            RebaseBase.OntoCommit("HEAD~2"),
            [Entry(range[0], RebaseTodoAction.Pick), Entry(range[1], RebaseTodoAction.Fixup)],
            Token);

        Assert.Equal(["One", "Two", "Three"], Subjects(repository));
        Assert.DoesNotContain("Four", repository.Git("log", "-1", "--format=%B"), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "four.txt")));
    }

    [Fact]
    public async Task GivenARewordFollowedByAFixup_WhenRebasing_ThenTheNewMessageSurvivesTheMeld()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var range = await writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~2"), Token);

        await writer.RebaseAsync(
            location,
            RebaseBase.OntoCommit("HEAD~2"),
            [Entry(range[0], RebaseTodoAction.Reword, "Renamed"), Entry(range[1], RebaseTodoAction.Fixup)],
            Token);

        Assert.Equal(["One", "Two", "Renamed"], Subjects(repository));
    }

    [Fact]
    public async Task GivenADrop_WhenRebasing_ThenTheCommitAndItsChangesAreGone()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var range = await writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~2"), Token);

        await writer.RebaseAsync(
            location,
            RebaseBase.OntoCommit("HEAD~2"),
            [Entry(range[0], RebaseTodoAction.Drop), Entry(range[1], RebaseTodoAction.Pick)],
            Token);

        Assert.Equal(["One", "Two", "Four"], Subjects(repository));
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "three.txt")));
    }

    [Fact]
    public async Task GivenEveryCommitDropped_WhenRebasing_ThenHeadReturnsToTheBase()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var range = await writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~2"), Token);
        var baseCommit = repository.Git("rev-parse", "HEAD~2").Trim();

        await writer.RebaseAsync(
            location,
            RebaseBase.OntoCommit("HEAD~2"),
            [Entry(range[0], RebaseTodoAction.Drop), Entry(range[1], RebaseTodoAction.Drop)],
            Token);

        Assert.Equal(baseCommit, repository.Head);
    }

    [Fact]
    public async Task GivenTheRootCommit_WhenRewordedWithRoot_ThenTheRootMessageChangesAndTheRestIsReplayed()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var range = await writer.ReadRangeAsync(location, RebaseBase.Root, Token);
        var entries = range
            .Select(commit => commit == range[0]
                ? Entry(commit, RebaseTodoAction.Reword, "Initial import")
                : Entry(commit, RebaseTodoAction.Pick))
            .ToList();

        var result = await writer.RebaseAsync(location, RebaseBase.Root, entries, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal(["Initial import", "Two", "Three", "Four"], Subjects(repository));
        var root = repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();
        Assert.Equal("Initial import", repository.Git("log", "-1", "--format=%s", root).Trim());
        Assert.NotEqual(range[0].Hash, root);
    }

    [Fact]
    public async Task GivenTheRootSquashedIntoNothing_WhenRebasing_ThenTheTodoIsRefusedBeforeGitRuns()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var range = await writer.ReadRangeAsync(location, RebaseBase.Root, Token);
        var head = repository.Head;
        var entries = range
            .Select(commit => Entry(commit, commit == range[0] ? RebaseTodoAction.Squash : RebaseTodoAction.Pick))
            .ToList();

        await Assert.ThrowsAsync<RebaseRangeException>(
            () => writer.RebaseAsync(location, RebaseBase.Root, entries, Token));

        Assert.Equal(head, repository.Head);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenAConflictingReorder_WhenRebasing_ThenItStopsAndAbortRestoresTheOriginalHead()
    {
        using var repository = Conflicting();
        var (writer, location) = await CreateAsync(repository);
        var range = await writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~3"), Token);
        var original = repository.Head;

        var result = await writer.RebaseAsync(
            location,
            RebaseBase.OntoCommit("HEAD~3"),
            [
                Entry(range[1], RebaseTodoAction.Pick),
                Entry(range[0], RebaseTodoAction.Pick),
                Entry(range[2], RebaseTodoAction.Pick),
            ],
            Token);

        Assert.Equal(OperationOutcome.Conflicted, result.Outcome);
        Assert.Equal(original, result.OriginalHead);
        Assert.Equal(RepositoryOperation.Rebase, new RepositoryOperationDetector().Detect(location));
        Assert.Contains("f.txt", repository.Git("diff", "--name-only", "--diff-filter=U"), StringComparison.Ordinal);

        var aborted = await CreateOperationWriter(repository)
            .AbortAsync(location, RepositoryOperation.Rebase, Token);

        Assert.Equal(OperationOutcome.Completed, aborted.Outcome);
        Assert.Equal(original, repository.Head);
        Assert.Equal(string.Empty, repository.Git("status", "--porcelain").Trim());
    }

    [Fact]
    public async Task GivenAConflictStopBeforeAReword_WhenResolvedAndContinued_ThenTheRewordStillApplies()
    {
        using var repository = Conflicting();
        var (writer, location) = await CreateAsync(repository);
        var range = await writer.ReadRangeAsync(location, RebaseBase.Root, Token);

        // Two is dropped, so Three conflicts; the reword of Four runs only after the stop is resolved.
        var result = await writer.RebaseAsync(
            location,
            RebaseBase.Root,
            [
                Entry(range[0], RebaseTodoAction.Pick),
                Entry(range[1], RebaseTodoAction.Drop),
                Entry(range[2], RebaseTodoAction.Pick),
                Entry(range[3], RebaseTodoAction.Reword, "Four, reworded"),
            ],
            Token);

        Assert.Equal(OperationOutcome.Conflicted, result.Outcome);
        repository.WriteFile("f.txt", "1\n3\n");
        repository.Git("add", "f.txt");
        var continued = await CreateOperationWriter(repository)
            .ContinueAsync(location, RepositoryOperation.Rebase, Token);

        Assert.Equal(OperationOutcome.Completed, continued.Outcome);
        Assert.Equal(["One", "Three", "Four, reworded"], Subjects(repository));
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenAMergeCommitInTheRange_WhenReadingOrRebasing_ThenItIsRefusedAndNothingChanges()
    {
        using var repository = Merged();
        var (writer, location) = await CreateAsync(repository);
        var head = repository.Head;

        var read = await Assert.ThrowsAsync<RebaseRangeException>(
            () => writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~2"), Token));
        var rebase = await Assert.ThrowsAsync<RebaseRangeException>(
            () => writer.RebaseAsync(location, RebaseBase.Root, [], Token));

        Assert.Contains("merge commit", read.Message, StringComparison.Ordinal);
        Assert.Contains("merge commit", rebase.Message, StringComparison.Ordinal);
        Assert.Equal(head, repository.Head);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenAMergeBelowTheBase_WhenReadingTheRangeAbove_ThenTheRangeIsAccepted()
    {
        using var repository = Merged();
        repository.Commit("After", new TestFile("after.txt", "after\n"));
        var (writer, location) = await CreateAsync(repository);

        var range = await writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~1"), Token);

        Assert.Equal(["After"], range.Select(commit => commit.Subject));
    }

    [Fact]
    public async Task GivenADirtyTree_WhenRebasing_ThenGitRefusesAndNoHelperFilesRemain()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var range = await writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~2"), Token);
        repository.WriteFile("three.txt", "dirty\n");

        await Assert.ThrowsAsync<GitCommandFailedException>(() => writer.RebaseAsync(
            location,
            RebaseBase.OntoCommit("HEAD~2"),
            [Entry(range[1], RebaseTodoAction.Reword, "New"), Entry(range[0], RebaseTodoAction.Pick)],
            Token));

        Assert.False(Directory.Exists(Path.Combine(location.GitDirectory, "gitunia-rebase")));
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenAnEnvironmentSequenceEditor_WhenRebasing_ThenItIsRefusedBecauseItWouldReplaceTheTodo()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository, name => name == "GIT_SEQUENCE_EDITOR" ? "true" : null);
        var range = await writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~1"), Token);

        var exception = await Assert.ThrowsAsync<RebaseEnvironmentException>(() => writer.RebaseAsync(
            location,
            RebaseBase.OntoCommit("HEAD~1"),
            [Entry(range[0], RebaseTodoAction.Drop)],
            Token));

        Assert.Contains("GIT_SEQUENCE_EDITOR", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenARewordFollowedByASquash_WhenRebasing_ThenTheNewSubjectAndTheSquashedMessageAreKept()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var range = await writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~2"), Token);

        await writer.RebaseAsync(
            location,
            RebaseBase.OntoCommit("HEAD~2"),
            [Entry(range[0], RebaseTodoAction.Reword, "Renamed"), Entry(range[1], RebaseTodoAction.Squash)],
            Token);

        Assert.Equal(["One", "Two", "Renamed"], Subjects(repository));
        Assert.Contains("Four", repository.Git("log", "-1", "--format=%B"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAConflictStop_WhenTheCommitIsSkipped_ThenTheRebaseCompletesWithoutIt()
    {
        using var repository = Conflicting();
        var (writer, location) = await CreateAsync(repository);
        var range = await writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~3"), Token);
        await writer.RebaseAsync(
            location,
            RebaseBase.OntoCommit("HEAD~3"),
            [
                Entry(range[1], RebaseTodoAction.Pick),
                Entry(range[0], RebaseTodoAction.Pick),
                Entry(range[2], RebaseTodoAction.Pick),
            ],
            Token);

        var skipped = await CreateOperationWriter(repository)
            .SkipAsync(location, RepositoryOperation.Rebase, Token);

        Assert.Equal(OperationOutcome.Completed, skipped.Outcome);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
        Assert.DoesNotContain("Three", Subjects(repository));
    }

    [Fact]
    public async Task GivenARejectedReword_WhenTheHookIsFixedAndTheRebaseContinued_ThenTheLaterRewordStillApplies()
    {
        using var repository = Linear();
        var hook = Path.Combine(repository.WorkingTreePath, ".git", "hooks", "commit-msg");
        Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        File.WriteAllText(hook, "#!/bin/sh\ngrep -q REJECT \"$1\" && exit 1\nexit 0\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var (writer, location) = await CreateAsync(repository);
        var range = await writer.ReadRangeAsync(location, RebaseBase.Root, Token);

        await Assert.ThrowsAsync<GitCommandFailedException>(() => writer.RebaseAsync(
            location,
            RebaseBase.Root,
            [
                Entry(range[0], RebaseTodoAction.Pick),
                Entry(range[1], RebaseTodoAction.Reword, "REJECT me"),
                Entry(range[2], RebaseTodoAction.Reword, "Three, reworded"),
                Entry(range[3], RebaseTodoAction.Pick),
            ],
            Token));

        Assert.Equal(RepositoryOperation.Rebase, new RepositoryOperationDetector().Detect(location));
        Assert.True(Directory.Exists(Path.Combine(location.GitDirectory, "gitunia-rebase")));
        File.Delete(hook);
        var continued = await CreateOperationWriter(repository)
            .ContinueAsync(location, RepositoryOperation.Rebase, Token);

        Assert.Equal(OperationOutcome.Completed, continued.Outcome);
        Assert.Contains("Three, reworded", Subjects(repository));
    }

    [Fact]
    public async Task GivenAnEditorPathWithSpaceAndApostrophe_WhenRebasing_ThenGitFindsTheEditorAndTheTodoApplies()
    {
        using var repository = Linear();
        var directory = Directory.CreateTempSubdirectory("gitunia it's here ");
        try
        {
            var script = Path.Combine(directory.FullName, "editor.sh");
            File.WriteAllText(script, "#!/bin/sh\ncp \"$1\" \"$2\"\n");
            var (writer, location) = await CreateAsync(repository, command: new SequenceEditorCommand(["sh", script]));
            var range = await writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~2"), Token);

            await writer.RebaseAsync(
                location,
                RebaseBase.OntoCommit("HEAD~2"),
                [Entry(range[1], RebaseTodoAction.Pick), Entry(range[0], RebaseTodoAction.Pick)],
                Token);

            Assert.Equal(["One", "Two", "Four", "Three"], Subjects(repository));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GivenATargetStartingWithADash_WhenReadingTheRange_ThenItIsRejected()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => writer.ReadRangeAsync(location, RebaseBase.OntoCommit("--root"), Token));
    }

    [Fact]
    public async Task GivenACancelledToken_WhenRebasing_ThenNothingRuns()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);
        var range = await writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~1"), Token);
        var head = repository.Head;
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.RebaseAsync(
            location,
            RebaseBase.OntoCommit("HEAD~1"),
            [Entry(range[0], RebaseTodoAction.Drop)],
            source.Token));

        Assert.Equal(head, repository.Head);
    }

    [Fact]
    public async Task GivenAMergeAndKeepMerges_WhenReadingTheRange_ThenTheTodoCarriesLabelResetAndMergeLines()
    {
        using var repository = Branchy();
        var (writer, location) = await CreateAsync(repository);
        var head = repository.Head;

        var range = await writer.ReadRangeAsync(location, RebaseBase.Root.WithMerges(), Token);

        Assert.Contains(range, line => line.Action == RebaseTodoAction.Label && line.Hash == "onto");
        Assert.Contains(range, line => line.Action == RebaseTodoAction.Reset);
        var merge = Assert.Single(range, line => line.Action == RebaseTodoAction.Merge);
        Assert.Equal(head, merge.Hash);
        Assert.Equal("Merge side", merge.Subject);
        Assert.All(range.Where(line => !line.Action.IsStructure()), line => Assert.Equal(40, line.Hash.Length));
        Assert.Equal(head, repository.Head);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
        Assert.Equal(string.Empty, repository.Git("status", "--porcelain").Trim());
    }

    [Fact]
    public async Task GivenARangeWithoutMergesAndKeepMerges_WhenReading_ThenItIsTheUsualCommitList()
    {
        using var repository = Linear();
        var (writer, location) = await CreateAsync(repository);

        var range = await writer.ReadRangeAsync(location, RebaseBase.OntoCommit("HEAD~2").WithMerges(), Token);

        Assert.Equal(["Three", "Four"], range.Select(commit => commit.Subject));
        Assert.All(range, commit => Assert.Equal(RebaseTodoAction.Pick, commit.Action));
    }

    [Fact]
    public async Task GivenTheTodoAsRead_WhenRebasingWithMerges_ThenTheMergeSurvivesWithTheSameTree()
    {
        using var repository = Branchy();
        var (writer, location) = await CreateAsync(repository);
        var rebaseBase = FromBase(repository);
        var range = await writer.ReadRangeAsync(location, rebaseBase, Token);
        var tree = repository.Git("rev-parse", "HEAD^{tree}").Trim();

        var result = await writer.RebaseAsync(location, rebaseBase, Entries(range), Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal("1", repository.Git("rev-list", "--merges", "--count", "HEAD").Trim());
        Assert.Equal(tree, repository.Git("rev-parse", "HEAD^{tree}").Trim());
        Assert.Equal("Merge side", repository.Git("log", "-1", "--format=%s").Trim());
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
        Assert.False(Directory.Exists(Path.Combine(location.GitDirectory, "gitunia-rebase")));
    }

    [Fact]
    public async Task GivenCommitsOnTheMergedBranch_WhenReorderedAndReworded_ThenTheMergeIsKeptOverTheNewOrder()
    {
        using var repository = Branchy();
        var (writer, location) = await CreateAsync(repository);
        var rebaseBase = FromBase(repository);
        var range = await writer.ReadRangeAsync(location, rebaseBase, Token);
        var entries = Swapped(range);
        var first = entries.FindIndex(entry => entry.Action == RebaseTodoAction.Pick);
        entries[first] = entries[first] with { Action = RebaseTodoAction.Reword, Message = "SideB, reworded" };

        var result = await writer.RebaseAsync(location, rebaseBase, entries, Token);

        Assert.Equal(OperationOutcome.Completed, result.Outcome);
        Assert.Equal("1", repository.Git("rev-list", "--merges", "--count", "HEAD").Trim());
        var side = repository.Git("log", "--reverse", "--format=%s", "HEAD^2", "^HEAD^1").Trim();
        Assert.Equal("SideB, reworded\nSideA", side);
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "a.txt")));
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
    }

    [Fact]
    public async Task GivenAMergeAndAConflictingReorder_WhenAborted_ThenTheMergedHistoryIsBack()
    {
        using var repository = ConflictingBranchy();
        var (writer, location) = await CreateAsync(repository);
        var rebaseBase = FromBase(repository);
        var range = await writer.ReadRangeAsync(location, rebaseBase, Token);
        var original = repository.Head;

        var result = await writer.RebaseAsync(location, rebaseBase, Swapped(range), Token);

        Assert.Equal(OperationOutcome.Conflicted, result.Outcome);
        Assert.Equal(RepositoryOperation.Rebase, new RepositoryOperationDetector().Detect(location));
        var aborted = await CreateOperationWriter(repository)
            .AbortAsync(location, RepositoryOperation.Rebase, Token);

        Assert.Equal(OperationOutcome.Completed, aborted.Outcome);
        Assert.Equal(original, repository.Head);
        Assert.Equal("1", repository.Git("rev-list", "--merges", "--count", "HEAD").Trim());
        Assert.Equal(string.Empty, repository.Git("status", "--porcelain").Trim());
    }

    [Fact]
    public async Task GivenAMergeAndAConflictingReorder_WhenResolvedAndContinued_ThenTheMergeIsRecreated()
    {
        using var repository = ConflictingBranchy();
        var (writer, location) = await CreateAsync(repository);
        var rebaseBase = FromBase(repository);
        var range = await writer.ReadRangeAsync(location, rebaseBase, Token);

        var result = await writer.RebaseAsync(location, rebaseBase, Swapped(range), Token);

        Assert.Equal(OperationOutcome.Conflicted, result.Outcome);
        Assert.True(Directory.Exists(Path.Combine(location.GitDirectory, "gitunia-rebase")));
        var outcome = result.Outcome;
        while (outcome == OperationOutcome.Conflicted)
        {
            repository.WriteFile("f.txt", "1\n2\n3\n");
            repository.Git("add", "f.txt");
            var continued = await CreateOperationWriter(repository)
                .ContinueAsync(location, RepositoryOperation.Rebase, Token);
            outcome = continued.Outcome;
        }

        Assert.Equal(OperationOutcome.Completed, outcome);
        Assert.Equal("1", repository.Git("rev-list", "--merges", "--count", "HEAD").Trim());
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenATodoWithoutTheMergeLine_WhenRebasing_ThenItIsRefusedBeforeGitStarts()
    {
        using var repository = Branchy();
        var (writer, location) = await CreateAsync(repository);
        var rebaseBase = RebaseBase.Root.WithMerges();
        var range = await writer.ReadRangeAsync(location, rebaseBase, Token);
        var entries = Entries(range).Where(entry => entry.Action != RebaseTodoAction.Merge).ToList();
        var head = repository.Head;

        await Assert.ThrowsAsync<RebaseRangeException>(
            () => writer.RebaseAsync(location, rebaseBase, entries, Token));

        Assert.Equal(head, repository.Head);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(location));
    }

    private static RebaseTodoEntry Entry(RebaseTodoCommit commit, RebaseTodoAction action, string? message = null) =>
        new(commit.Hash, action, message);

    private static string[] Subjects(TestRepository repository) =>
        repository.Git("log", "--reverse", "--format=%s").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static TestRepository Linear() =>
        TestRepository.Create()
            .Commit("One", new TestFile("one.txt", "1\n"))
            .Commit("Two", new TestFile("two.txt", "2\n"))
            .Commit("Three", new TestFile("three.txt", "3\n"))
            .Commit("Four", new TestFile("four.txt", "4\n"));

    private static TestRepository Conflicting() =>
        TestRepository.Create()
            .Commit("One", new TestFile("f.txt", "1\n"))
            .Commit("Two", new TestFile("f.txt", "1\n2\n"))
            .Commit("Three", new TestFile("f.txt", "1\n2\n3\n"))
            .Commit("Four", new TestFile("g.txt", "4\n"));

    private static RebaseBase FromBase(TestRepository repository) =>
        RebaseBase.OntoCommit(repository.Git("rev-list", "--max-parents=0", "HEAD").Trim()).WithMerges();

    private static List<RebaseTodoEntry> Entries(IReadOnlyList<RebaseTodoCommit> range) =>
        [.. range.Select(line => new RebaseTodoEntry(line.Hash, line.Action, Argument: line.Argument))];

    // Swaps the two commits of the branch that is merged (SideA and SideB).
    private static List<RebaseTodoEntry> Swapped(IReadOnlyList<RebaseTodoCommit> range)
    {
        var entries = Entries(range);
        var picks = Enumerable.Range(0, range.Count)
            .Where(index => range[index].Subject.StartsWith("Side", StringComparison.Ordinal))
            .ToList();
        (entries[picks[0]], entries[picks[1]]) = (entries[picks[1]], entries[picks[0]]);
        return entries;
    }

    private static TestRepository Branchy()
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

    private static TestRepository ConflictingBranchy()
    {
        var repository = TestRepository.Create()
            .Commit("Base", new TestFile("f.txt", "1\n"))
            .CheckoutNewBranch("side")
            .Commit("SideA", new TestFile("f.txt", "1\n2\n"))
            .Commit("SideB", new TestFile("f.txt", "1\n2\n3\n"))
            .Checkout("main")
            .Commit("Main", new TestFile("main.txt", "main\n"));
        repository.Git("merge", "--no-ff", "-m", "Merge side", "side");
        return repository;
    }

    private static TestRepository Merged()
    {
        var repository = TestRepository.Create()
            .Commit("Base", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("side")
            .Commit("Side", new TestFile("side.txt", "side\n"))
            .Checkout("main")
            .Commit("Main", new TestFile("main.txt", "main\n"));
        repository.Git("merge", "--no-ff", "-m", "Merge side", "side");
        return repository;
    }

    private static OperationWriter CreateOperationWriter(TestRepository repository) =>
        new(new OperationRunner(repository.CreateIsolatedRunner(), new RepositoryOperationDetector()));

    private static async Task<Harness> CreateAsync(
        TestRepository repository,
        Func<string, string?>? environment = null,
        SequenceEditorCommand? command = null)
    {
        var runner = repository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);
        var detector = new RepositoryOperationDetector();
        var operations = new OperationRunner(runner, detector);

        // The sequence editor is cp: git calls "cp <prepared todo> <its todo>", the same copy Gitunia's mode makes.
        var writer = new InteractiveRebaseWriter(
            operations,
            runner,
            detector,
            command ?? new SequenceEditorCommand(["cp"]),
            environment ?? (_ => null));
        return new Harness(writer, location);
    }

    private sealed record Harness(InteractiveRebaseWriter Writer, RepositoryLocation Location);
}
