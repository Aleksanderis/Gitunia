using Gitunia.Git.Commits.Models;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Staging;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Staging;

public class IndexWriterTests
{
    private static readonly GitRunner Runner = new();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly StatusReader Status = new(Runner);
    private static readonly IndexWriter Writer = new(Runner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

    private static async Task<IReadOnlyList<StatusEntry>> ReadAsync(RepositoryLocation location)
    {
        var snapshot = await Status.ReadStatusAsync(location, false, UntrackedFilesMode.All, Token);
        return snapshot.Entries;
    }

    [Fact]
    public async Task GivenModifiedAndUntrackedFiles_WhenStaging_ThenTheyMoveToTheIndexAndOthersStay()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("a.txt", "a\n"), new TestFile("b.txt", "b\n"));
        repository.WriteFile("a.txt", "a changed\n").WriteFile("b.txt", "b changed\n").WriteFile("new.txt", "new\n");
        var location = await LocateAsync(repository);

        await Writer.StageAsync(location, ["a.txt", "new.txt"], Token);

        var entries = await ReadAsync(location);
        Assert.Equal(FileChangeKind.Modified, Entry(entries, "a.txt").IndexChange);
        Assert.Equal(FileChangeKind.Unmodified, Entry(entries, "a.txt").WorktreeChange);
        Assert.Equal(FileChangeKind.Added, Entry(entries, "new.txt").IndexChange);
        Assert.Equal(FileChangeKind.Unmodified, Entry(entries, "b.txt").IndexChange);
    }

    [Fact]
    public async Task GivenAMergeConflict_WhenStagingTheConflictedFile_ThenItIsResolvedAndTheMarkersStayInTheFile()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("c.txt", "base\n"));
        repository.CheckoutNewBranch("other").Commit("Other", new TestFile("c.txt", "other\n"));
        repository.Checkout("main").Commit("Main", new TestFile("c.txt", "main\n")).MergeExpectingConflict("other");
        var location = await LocateAsync(repository);
        Assert.Equal(StatusEntryKind.Unmerged, Entry(await ReadAsync(location), "c.txt").Kind);

        await Writer.StageAsync(location, ["c.txt"], Token);

        Assert.DoesNotContain(await ReadAsync(location), entry => entry.Kind == StatusEntryKind.Unmerged);
        Assert.Contains("<<<<<<<", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "c.txt")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenDeletedFile_WhenStaging_ThenTheDeletionIsStaged()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("gone.txt", "x\n"));
        repository.Delete("gone.txt");
        var location = await LocateAsync(repository);

        await Writer.StageAsync(location, ["gone.txt"], Token);

        Assert.Equal(FileChangeKind.Deleted, Entry(await ReadAsync(location), "gone.txt").IndexChange);
    }

    [Fact]
    public async Task GivenBinaryFile_WhenStaging_ThenItIsStagedWhole()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        File.WriteAllBytes(Path.Combine(repository.WorkingTreePath, "image.bin"), [0, 1, 2, 0, 255]);
        var location = await LocateAsync(repository);

        await Writer.StageAsync(location, ["image.bin"], Token);

        Assert.Equal(FileChangeKind.Added, Entry(await ReadAsync(location), "image.bin").IndexChange);
    }

    [Fact]
    public async Task GivenNamesWithGlobCharactersAndColon_WhenStaging_ThenOnlyTheNamedFilesAreStaged()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("star*.txt", "1\n").WriteFile("starry.txt", "2\n").WriteFile(":colon.txt", "3\n");
        var location = await LocateAsync(repository);

        await Writer.StageAsync(location, ["star*.txt", ":colon.txt"], Token);

        var entries = await ReadAsync(location);
        Assert.Equal(FileChangeKind.Added, Entry(entries, "star*.txt").IndexChange);
        Assert.Equal(FileChangeKind.Added, Entry(entries, ":colon.txt").IndexChange);
        Assert.Equal(StatusEntryKind.Untracked, Entry(entries, "starry.txt").Kind);
    }

    [Fact]
    public async Task GivenNoPaths_WhenStagingOrUnstaging_ThenNothingRuns()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        var location = await LocateAsync(repository);

        await Writer.StageAsync(location, [], Token);
        await Writer.UnstageAsync(location, [], false, Token);

        Assert.Empty(await ReadAsync(location));
    }

    [Fact]
    public async Task GivenStagedChanges_WhenUnstaging_ThenTheyReturnToTheWorkingTree()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("a.txt", "a\n"), new TestFile("gone.txt", "x\n"));
        repository.WriteFile("a.txt", "changed\n").WriteFile("new.txt", "new\n").Delete("gone.txt");
        repository.Git("add", "-A");
        var location = await LocateAsync(repository);

        await Writer.UnstageAsync(location, ["a.txt", "new.txt", "gone.txt"], false, Token);

        var entries = await ReadAsync(location);
        Assert.Equal(FileChangeKind.Modified, Entry(entries, "a.txt").WorktreeChange);
        Assert.Equal(FileChangeKind.Unmodified, Entry(entries, "a.txt").IndexChange);
        Assert.Equal(StatusEntryKind.Untracked, Entry(entries, "new.txt").Kind);
        Assert.Equal(FileChangeKind.Deleted, Entry(entries, "gone.txt").WorktreeChange);
    }

    [Fact]
    public async Task GivenStagedRename_WhenUnstagingBothPaths_ThenTheRenameIsUndone()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("old.txt", "some content\nmore\n"));
        repository.Git("mv", "old.txt", "renamed.txt");
        var location = await LocateAsync(repository);
        var renamed = Entry(await ReadAsync(location), "renamed.txt");
        Assert.Equal("old.txt", renamed.OriginalPath);

        await Writer.UnstageAsync(location, [renamed.Path, renamed.OriginalPath!], false, Token);

        var entries = await ReadAsync(location);
        Assert.Equal(FileChangeKind.Deleted, Entry(entries, "old.txt").WorktreeChange);
        Assert.Equal(StatusEntryKind.Untracked, Entry(entries, "renamed.txt").Kind);
    }

    [Fact]
    public async Task GivenUnbornBranch_WhenUnstaging_ThenTheFileBecomesUntrackedAgain()
    {
        using var repository = TestRepository.Create();
        repository.WriteFile("first.txt", "1\n").WriteFile("second.txt", "2\n");
        repository.Git("add", "first.txt", "second.txt");
        var location = await LocateAsync(repository);

        await Writer.UnstageAsync(location, ["first.txt"], true, Token);

        var entries = await ReadAsync(location);
        Assert.Equal(StatusEntryKind.Untracked, Entry(entries, "first.txt").Kind);
        Assert.Equal(FileChangeKind.Added, Entry(entries, "second.txt").IndexChange);
    }

    [Fact]
    public async Task GivenUnbornBranchAndFurtherEdits_WhenUnstaging_ThenTheFileLeavesTheIndexAndKeepsItsContent()
    {
        using var repository = TestRepository.Create();
        repository.WriteFile("f.txt", "a\n");
        repository.Git("add", "f.txt");
        repository.WriteFile("f.txt", "a\nb\n");
        var location = await LocateAsync(repository);

        await Writer.UnstageAsync(location, ["f.txt"], true, Token);

        Assert.Equal(StatusEntryKind.Untracked, Entry(await ReadAsync(location), "f.txt").Kind);
        Assert.Equal("a\nb\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "f.txt")));
    }

    [Fact]
    public async Task GivenMissingPath_WhenStaging_ThenGitFailureIsRaised()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        var location = await LocateAsync(repository);

        await Assert.ThrowsAnyAsync<Exception>(() => Writer.StageAsync(location, ["nope.txt"], Token));
    }

    [Fact]
    public async Task GivenAPatchThatMatchesTheIndex_WhenApplyingForwardAndReverse_ThenOnlyTheIndexChanges()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "one\ntwo\n"));
        repository.WriteFile("a.txt", "one\nTWO\n");
        var location = await LocateAsync(repository);
        const string patch = "diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1,2 +1,2 @@\n one\n-two\n+TWO\n";

        await Writer.ApplyPatchAsync(location, patch, PatchDirection.Forward, Token);
        Assert.Equal("one\nTWO\n", repository.Git("show", ":a.txt"));

        await Writer.ApplyPatchAsync(location, patch, PatchDirection.Reverse, Token);
        Assert.Equal("one\ntwo\n", repository.Git("show", ":a.txt"));
        Assert.Equal("one\nTWO\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "a.txt")));
    }

    [Fact]
    public async Task GivenAPatchThatDoesNotApply_WhenApplying_ThenGitFailsAndTheIndexIsUntouched()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "one\ntwo\n"));
        var location = await LocateAsync(repository);
        const string patch = "diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1,2 +1,2 @@\n one\n-zzz\n+TWO\n";

        await Assert.ThrowsAsync<GitCommandFailedException>(() => Writer.ApplyPatchAsync(location, patch, PatchDirection.Forward, Token));

        Assert.Equal("one\ntwo\n", repository.Git("show", ":a.txt"));
    }

    private static StatusEntry Entry(IReadOnlyList<StatusEntry> entries, string path) =>
        Assert.Single(entries, entry => entry.Path == path);
}
