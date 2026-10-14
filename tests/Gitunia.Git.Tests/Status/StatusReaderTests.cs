using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Status;

public class StatusReaderTests
{
    private static readonly GitRunner Runner = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<StatusSnapshot> ReadAsync(TestRepository testRepository, bool includeIgnored = false)
    {
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);
        var snapshot = await StatusReader.ReadStatusAsync(repository, includeIgnored, Token);
        Assert.Empty(snapshot.Warnings);
        return snapshot;
    }

    private static string Numbered(int count) => string.Concat(Enumerable.Range(1, count).Select(number => number + "\n"));

    [Fact]
    public async Task GivenUntrackedDirectory_WhenReadingWithNormalMode_ThenItIsOneEntryButStillDirty()
    {
        using var testRepository = TestRepositories.Linear(1);
        testRepository.WriteFile("dir/one.txt", "1\n");
        testRepository.WriteFile("dir/two.txt", "2\n");
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var all = await StatusReader.ReadStatusAsync(repository, false, Token);
        var normal = await StatusReader.ReadStatusAsync(repository, false, Token, UntrackedFilesMode.Normal);

        Assert.Equal(2, all.Entries.Count);
        var entry = Assert.Single(normal.Entries);
        Assert.Equal(StatusEntryKind.Untracked, entry.Kind);
    }

    [Fact]
    public async Task GivenCleanRepository_WhenReadingStatus_ThenNoEntriesAndBranchHead()
    {
        using var testRepository = TestRepositories.Linear(2);

        var snapshot = await ReadAsync(testRepository);

        Assert.Empty(snapshot.Entries);
        Assert.Equal(new HeadState(HeadKind.Branch, "main", testRepository.Head), snapshot.Branch.Head);
        Assert.Null(snapshot.Branch.UpstreamName);
    }

    [Fact]
    public async Task GivenUnbornBranchWithUntrackedFile_WhenReadingStatus_ThenUnbornHead()
    {
        using var testRepository = TestRepository.Create();
        testRepository.WriteFile("first.txt", "a\n");

        var snapshot = await ReadAsync(testRepository);

        Assert.Equal(new HeadState(HeadKind.Unborn, "main", null), snapshot.Branch.Head);
        var entry = Assert.Single(snapshot.Entries);
        Assert.Equal(StatusEntryKind.Untracked, entry.Kind);
    }

    [Fact]
    public async Task GivenDetachedHead_WhenReadingStatus_ThenDetachedHead()
    {
        using var testRepository = TestRepositories.Linear(2);
        testRepository.Git("checkout", "-q", "--detach", "HEAD~1");

        var snapshot = await ReadAsync(testRepository);

        Assert.Equal(HeadKind.Detached, snapshot.Branch.Head.Kind);
        Assert.Null(snapshot.Branch.Head.BranchName);
        Assert.Equal(testRepository.Head, snapshot.Branch.Head.CommitHash);
    }

    [Fact]
    public async Task GivenBranchAheadAndBehindUpstream_WhenReadingStatus_ThenCountsAreRead()
    {
        using var testRepository = TestRepositories.Linear(2);
        testRepository.Branch("upstream");
        testRepository.Git("branch", "--set-upstream-to=upstream", "main");
        testRepository.Commit("Local one", ("local1.txt", "a\n"));
        testRepository.Commit("Local two", ("local2.txt", "a\n"));
        testRepository.Checkout("upstream");
        testRepository.Commit("Remote one", ("remote1.txt", "a\n"));
        testRepository.Checkout("main");

        var snapshot = await ReadAsync(testRepository);

        Assert.Equal("upstream", snapshot.Branch.UpstreamName);
        Assert.Equal(2, snapshot.Branch.Ahead);
        Assert.Equal(1, snapshot.Branch.Behind);
    }

    [Fact]
    public async Task GivenStagedUnstagedAndUntrackedFiles_WhenReadingStatus_ThenEachKindIsReported()
    {
        using var testRepository = TestRepository.Create().Commit("Initial", ("tracked.txt", "a\n"), ("staged.txt", "a\n"), ("both.txt", "a\n"));
        testRepository.WriteFile("tracked.txt", "changed\n");
        testRepository.WriteFile("staged.txt", "changed\n");
        testRepository.Git("add", "staged.txt");
        testRepository.WriteFile("both.txt", "first\n");
        testRepository.Git("add", "both.txt");
        testRepository.WriteFile("both.txt", "second\n");
        testRepository.WriteFile(Path.Combine("sub", "untracked.txt"), "u\n");

        var snapshot = await ReadAsync(testRepository);

        var byPath = snapshot.Entries.ToDictionary(entry => entry.Path);
        Assert.Equal((FileChangeKind.Unmodified, FileChangeKind.Modified), (byPath["tracked.txt"].IndexChange, byPath["tracked.txt"].WorktreeChange));
        Assert.Equal((FileChangeKind.Modified, FileChangeKind.Unmodified), (byPath["staged.txt"].IndexChange, byPath["staged.txt"].WorktreeChange));
        Assert.Equal((FileChangeKind.Modified, FileChangeKind.Modified), (byPath["both.txt"].IndexChange, byPath["both.txt"].WorktreeChange));
        Assert.Equal(StatusEntryKind.Untracked, byPath["sub/untracked.txt"].Kind);
    }

    [Fact]
    public async Task GivenStagedRename_WhenReadingStatus_ThenRenameWithSourceIsReported()
    {
        using var testRepository = TestRepository.Create().Commit("Initial", ("old name.txt", Numbered(40)));
        testRepository.Git("mv", "old name.txt", "ünï cødé.txt");

        var snapshot = await ReadAsync(testRepository);

        var entry = Assert.Single(snapshot.Entries);
        Assert.Equal(StatusEntryKind.Renamed, entry.Kind);
        Assert.Equal("ünï cødé.txt", entry.Path);
        Assert.Equal("old name.txt", entry.OriginalPath);
        Assert.Equal(100, entry.Similarity);
    }

    [Fact]
    public async Task GivenStagedCopyWithCopyDetectionEnabled_WhenReadingStatus_ThenCopyIsReported()
    {
        using var testRepository = TestRepository.Create().Commit("Initial", ("original.txt", Numbered(40)));
        testRepository.Git("config", "status.renames", "copies");

        // Git only detects a copy when the source is modified in the same change.
        testRepository.WriteFile("original.txt", Numbered(40) + "modified\n");
        testRepository.WriteFile("copy.txt", Numbered(40) + "extra\n");
        testRepository.Git("add", "original.txt", "copy.txt");

        var snapshot = await ReadAsync(testRepository);

        var entry = Assert.Single(snapshot.Entries, candidate => candidate.Kind == StatusEntryKind.Copied);
        Assert.Equal("copy.txt", entry.Path);
        Assert.Equal("original.txt", entry.OriginalPath);
    }

    [Fact]
    public async Task GivenOddPaths_WhenReadingStatus_ThenPathsAreReportedVerbatim()
    {
        using var testRepository = TestRepositories.OddPaths();
        foreach (var path in new[] { "with space.txt", "ünï cødé.txt", "quote's.txt", Path.Combine("dir with space", "nested file.txt") })
        {
            testRepository.WriteFile(path, "changed\n");
        }

        testRepository.WriteFile("日本語 ✓.txt", "new\n");

        var snapshot = await ReadAsync(testRepository);

        Assert.Equal(
            ["dir with space/nested file.txt", "quote's.txt", "with space.txt", "ünï cødé.txt", "日本語 ✓.txt"],
            snapshot.Entries.Select(entry => entry.Path).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task GivenIgnoredFile_WhenReadingWithAndWithoutIgnored_ThenOnlyIncludedOnRequest()
    {
        using var testRepository = TestRepository.Create().Commit("Initial", (".gitignore", "*.log\n"));
        testRepository.WriteFile("debug.log", "x\n");

        var without = await ReadAsync(testRepository);
        var with = await ReadAsync(testRepository, includeIgnored: true);

        Assert.Empty(without.Entries);
        var entry = Assert.Single(with.Entries);
        Assert.Equal(StatusEntryKind.Ignored, entry.Kind);
        Assert.Equal("debug.log", entry.Path);
    }

    [Fact]
    public async Task GivenMergeConflict_WhenReadingStatus_ThenUnmergedEntryIsReported()
    {
        using var testRepository = TestRepositories.Conflicted();

        var snapshot = await ReadAsync(testRepository);

        var entry = Assert.Single(snapshot.Entries);
        Assert.Equal(StatusEntryKind.Unmerged, entry.Kind);
        Assert.Equal(ConflictKind.BothModified, entry.Conflict);
        Assert.Equal("conflict.txt", entry.Path);
    }

    [Fact]
    public async Task GivenDeletedByThemConflict_WhenReadingStatus_ThenConflictKindIsRead()
    {
        using var testRepository = TestRepository.Create()
            .Commit("Initial", ("file.txt", "a\n"))
            .CheckoutNewBranch("other")
            .Commit("Other change", ("file.txt", "other\n"))
            .Checkout("main");
        testRepository.Git("rm", "-q", "file.txt");
        testRepository.Git("commit", "-q", "-m", "Delete file");
        testRepository.MergeExpectingConflict("other");

        var snapshot = await ReadAsync(testRepository);

        var entry = Assert.Single(snapshot.Entries);
        Assert.Equal(ConflictKind.DeletedByUs, entry.Conflict);
    }
}
