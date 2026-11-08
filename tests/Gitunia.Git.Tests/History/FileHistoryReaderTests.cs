using Gitunia.Git.Commits.Models;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.History;
using Gitunia.Git.History.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.History;

public class FileHistoryReaderTests
{
    private const string Head = "HEAD";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenARenamedFile_WhenReadingItsHistory_ThenCommitsBeforeTheRenameAreListedUnderTheOldName()
    {
        using var repository = TestRepository.Create()
            .Commit("add", new TestFile("old.txt", "a\nb\nc\nd\ne\n"));
        repository.Commit("edit old", new TestFile("old.txt", "a\nb\nc\nd\ne\nf\n"));
        repository.Git("mv", "old.txt", "new.txt");
        repository.Git("commit", "-q", "-m", "rename");
        repository.Commit("edit new", new TestFile("new.txt", "a\nb\nc\nd\ne\nf\ng\n"));

        var entries = await ReadAsync(repository, "new.txt");

        Assert.Equal(["edit new", "rename", "edit old", "add"], entries.Select(entry => entry.Subject));
        Assert.Equal(["new.txt", "new.txt", "old.txt", "old.txt"], entries.Select(entry => entry.Path));
        Assert.Equal(FileChangeKind.Renamed, entries[1].Kind);
        Assert.Equal("old.txt", entries[1].OldPath);
        Assert.Equal(FileChangeKind.Added, entries[3].Kind);
    }

    [Fact]
    public async Task GivenAFileChangedOnTwoBranchesAndMerged_WhenReadingItsHistory_ThenBothBranchesCommitsAreListed()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("f.txt", "1\n2\n3\n"));
        repository.CheckoutNewBranch("side");
        repository.Commit("side edit", new TestFile("f.txt", "1\n2\n3\nside\n"));
        repository.Checkout("main");
        repository.Commit("main edit", new TestFile("f.txt", "top\n1\n2\n3\n"));
        repository.Merge("side");

        var entries = await ReadAsync(repository, "f.txt");

        var subjects = entries.Select(entry => entry.Subject).ToList();
        Assert.Contains("side edit", subjects);
        Assert.Contains("main edit", subjects);
        Assert.Equal("base", subjects[^1]);
        Assert.Equal(subjects.Count, entries.Select(entry => entry.Hash).Distinct().Count());
    }

    [Fact]
    public async Task GivenAFileDeletedAndAddedAgain_WhenReadingItsHistory_ThenTheDeletionAndTheReAdditionAreListed()
    {
        using var repository = TestRepository.Create().Commit("add", new TestFile("f.txt", "one\n"));
        repository.Git("rm", "-q", "f.txt");
        repository.Git("commit", "-q", "-m", "delete");
        repository.Commit("add again", new TestFile("f.txt", "two\n"));

        var entries = await ReadAsync(repository, "f.txt");

        Assert.Equal(["add again", "delete", "add"], entries.Select(entry => entry.Subject));
        Assert.Equal(
            [FileChangeKind.Added, FileChangeKind.Deleted, FileChangeKind.Added],
            entries.Select(entry => entry.Kind));
    }

    [Fact]
    public async Task GivenAPathWithSpacesAndNonAsciiLetters_WhenReadingItsHistory_ThenItIsFound()
    {
        using var repository = TestRepository.Create()
            .Commit("add", new TestFile("dir with space/日本 ü.txt", "x\n"))
            .Commit("edit", new TestFile("dir with space/日本 ü.txt", "y\n"));

        var entries = await ReadAsync(repository, "dir with space/日本 ü.txt");

        Assert.Equal(["edit", "add"], entries.Select(entry => entry.Subject));
        Assert.All(entries, entry => Assert.Equal("dir with space/日本 ü.txt", entry.Path));
    }

    [Fact]
    public async Task GivenAGlobCharacterInThePath_WhenReadingItsHistory_ThenOnlyThatFileIsMatched()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows file names cannot contain an asterisk.");

        using var repository = TestRepository.Create()
            .Commit("add star", new TestFile("a*.txt", "x\n"))
            .Commit("add other", new TestFile("ab.txt", "y\n"));

        var entries = await ReadAsync(repository, "a*.txt");

        Assert.Equal(["add star"], entries.Select(entry => entry.Subject));
    }

    [Fact]
    public async Task GivenAnUnbornHeadWithAStagedFile_WhenReadingItsHistory_ThenTheListIsEmptyNotAnError()
    {
        using var repository = TestRepository.Create();
        repository.WriteFile("a.txt", "x\n");
        repository.Git("add", "a.txt");

        var entries = await ReadAsync(repository, "a.txt");

        Assert.Empty(entries);
    }

    [Fact]
    public async Task GivenSignatureShowingInTheUserConfig_WhenReadingItsHistory_ThenTheRecordsStillParse()
    {
        using var repository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1\n"));
        repository.Git("config", "log.showSignature", "true");

        var entries = await ReadAsync(repository, "a.txt");

        Assert.Equal(["one"], entries.Select(entry => entry.Subject));
    }

    [Fact]
    public async Task GivenAFileThatWasNeverCommitted_WhenReadingItsHistory_ThenTheListIsEmpty()
    {
        using var repository = TestRepository.Create().Commit("add", new TestFile("a.txt", "x\n"));
        repository.WriteFile("untracked.txt", "u\n");

        var entries = await ReadAsync(repository, "untracked.txt");

        Assert.Empty(entries);
    }

    [Fact]
    public async Task GivenARevisionInThePast_WhenReadingItsHistory_ThenLaterCommitsAreNotListed()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1\n"))
            .Commit("two", new TestFile("a.txt", "2\n"));
        var second = repository.Head;
        repository.Commit("three", new TestFile("a.txt", "3\n"));

        var entries = await ReadAsync(repository, "a.txt", second);

        Assert.Equal(["two", "one"], entries.Select(entry => entry.Subject));
    }

    [Fact]
    public async Task GivenALimit_WhenReadingItsHistory_ThenAtMostThatManyCommitsAreReturned()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1\n"))
            .Commit("two", new TestFile("a.txt", "2\n"))
            .Commit("three", new TestFile("a.txt", "3\n"));

        var entries = await ReadAsync(repository, "a.txt", limit: 2);

        Assert.Equal(["three", "two"], entries.Select(entry => entry.Subject));
    }

    [Fact]
    public async Task GivenAPendingCancellation_WhenReadingItsHistory_ThenTheReadIsCancelled()
    {
        using var repository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1\n"));
        var runner = repository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new FileHistoryReader(runner).ReadAsync(location, Head, "a.txt", 10, source.Token));
    }

    [Fact]
    public async Task GivenARenameEntry_WhenReadingItsDiff_ThenGitPairsTheOldAndTheNewName()
    {
        using var repository = TestRepository.Create()
            .Commit("add", new TestFile("old.txt", "a\nb\nc\nd\ne\nf\n"));
        repository.Git("mv", "old.txt", "new.txt");
        repository.WriteFile("new.txt", "a\nb\nc\nd\ne\nf\ng\n");
        repository.Git("add", "-A");
        repository.Git("commit", "-q", "-m", "rename and edit");
        var entries = await ReadAsync(repository, "new.txt");

        var files = await DiffOfAsync(repository, entries[0]);

        var file = Assert.Single(files);
        Assert.Equal(DiffFileStatus.Renamed, file.Status);
        Assert.Equal("old.txt", file.OldPath);
        Assert.Equal("new.txt", file.NewPath);
        Assert.Contains(file.Hunks.SelectMany(hunk => hunk.Lines), line => line.Text == "g");
    }

    [Fact]
    public async Task GivenAMergeCommit_WhenReadingItsDiff_ThenItIsTheDiffAgainstTheFirstParentOfThatFileOnly()
    {
        using var repository = TestRepository.Create()
            .Commit("base", new TestFile("f.txt", "1\n2\n3\n"), new TestFile("other.txt", "o\n"));
        repository.CheckoutNewBranch("side");
        repository.Commit("side edit", new TestFile("f.txt", "1\n2\n3\nside\n"), new TestFile("other.txt", "side\n"));
        repository.Checkout("main");
        repository.Commit("main edit", new TestFile("f.txt", "top\n1\n2\n3\n"));
        repository.Merge("side");
        var merge = new FileHistoryEntry(repository.Head, "T", "2026-01-01", "Merge", "f.txt", null, FileChangeKind.Modified);

        var files = await DiffOfAsync(repository, merge);

        var file = Assert.Single(files);
        Assert.Equal("f.txt", file.Path);
        var added = file.Hunks.SelectMany(hunk => hunk.Lines).Where(line => line.Kind == DiffLineKind.Added);
        Assert.Equal(["side"], added.Select(line => line.Text));
    }

    [Fact]
    public async Task GivenTheRootCommit_WhenReadingItsDiff_ThenTheWholeFileIsAdded()
    {
        using var repository = TestRepository.Create().Commit("add", new TestFile("a b.txt", "x\ny\n"));
        var entries = await ReadAsync(repository, "a b.txt");

        var files = await DiffOfAsync(repository, entries[0]);

        var file = Assert.Single(files);
        Assert.Equal(DiffFileStatus.Added, file.Status);
        Assert.Equal(2, file.Hunks.SelectMany(hunk => hunk.Lines).Count(line => line.Kind == DiffLineKind.Added));
    }

    [Fact]
    public async Task GivenALineRange_WhenReadingItsHistory_ThenOnlyCommitsThatChangedThoseLinesComeWithTheirPatch()
    {
        using var repository = TestRepository.Create().Commit("add", new TestFile("f.txt", "a\nb\nc\nd\n"));
        repository.Commit("edit b", new TestFile("f.txt", "a\nB\nc\nd\n"));
        repository.Commit("edit d", new TestFile("f.txt", "a\nB\nc\nD\n"));

        var entries = await ReadRangeAsync(repository, "f.txt", new LineRange(2, 3));

        Assert.Equal(["edit b", "add"], entries.Select(entry => entry.Subject));
        Assert.Contains("+B", entries[0].Patch, StringComparison.Ordinal);
        Assert.DoesNotContain("D", entries[0].Patch, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenLinesThatShifted_WhenReadingTheirHistory_ThenTheWalkFollowsThemToWhereTheyWere()
    {
        using var repository = TestRepository.Create().Commit("add", new TestFile("f.txt", "a\nb\nc\nd\n"));
        repository.Commit("edit c", new TestFile("f.txt", "a\nb\nC\nd\n"));
        repository.Commit("insert on top", new TestFile("f.txt", "x\ny\na\nb\nC\nd\n"));

        var entries = await ReadRangeAsync(repository, "f.txt", new LineRange(5, 5));

        Assert.Equal(["edit c", "add"], entries.Select(entry => entry.Subject));
    }

    [Fact]
    public async Task GivenARenamedFile_WhenReadingALineRange_ThenCommitsBeforeTheRenameAreFoundUnderTheOldName()
    {
        using var repository = TestRepository.Create().Commit("add", new TestFile("old.txt", "a\nb\nc\n"));
        repository.Commit("edit old", new TestFile("old.txt", "a\nB\nc\n"));
        repository.Git("mv", "old.txt", "new.txt");
        repository.Git("commit", "-q", "-m", "rename");

        var entries = await ReadRangeAsync(repository, "new.txt", new LineRange(2, 2));

        Assert.Equal(["edit old", "add"], entries.Select(entry => entry.Subject));
        Assert.All(entries, entry => Assert.Equal("old.txt", entry.Path));
    }

    [Fact]
    public async Task GivenARangeAddedByTheLastCommit_WhenReadingItsHistory_ThenThatCommitIsTheOnlyEntry()
    {
        using var repository = TestRepository.Create().Commit("add", new TestFile("f.txt", "a\nb\n"));
        repository.Commit("append", new TestFile("f.txt", "a\nb\nc\nd\n"));

        var entries = await ReadRangeAsync(repository, "f.txt", new LineRange(3, 4));

        var entry = Assert.Single(entries);
        Assert.Equal("append", entry.Subject);
        Assert.Contains("+c", entry.Patch, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAWorkingTreeThatDiffersFromHead_WhenReadingARange_ThenTheLineNumbersAreThoseOfHead()
    {
        using var repository = TestRepository.Create().Commit("add", new TestFile("f.txt", "a\nb\nc\n"));
        repository.Commit("edit c", new TestFile("f.txt", "a\nb\nC\n"));
        repository.WriteFile("f.txt", "new top\na\nb\nC\n");

        var entries = await ReadRangeAsync(repository, "f.txt", new LineRange(3, 3));

        Assert.Equal(["edit c", "add"], entries.Select(entry => entry.Subject));
    }

    [Fact]
    public async Task GivenARangeBeyondTheEndOfTheFile_WhenReading_ThenGitsMessageSaysHowManyLinesThereAre()
    {
        using var repository = TestRepository.Create().Commit("add", new TestFile("f.txt", "a\nb\n"));
        var runner = repository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() =>
            new FileHistoryReader(runner).ReadRangeAsync(location, Head, "f.txt", new LineRange(5, 9), 10, Token));

        Assert.Contains("has only 2 lines", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAFileThatIsNotInTheStartRevision_WhenReadingARange_ThenItFailsWithTheMissingPath()
    {
        using var repository = TestRepository.Create().Commit("add", new TestFile("f.txt", "a\n"));
        var runner = repository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() =>
            new FileHistoryReader(runner).ReadRangeAsync(location, Head, "nope.txt", new LineRange(1, 1), 10, Token));

        Assert.Contains("nope.txt", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnUnbornHead_WhenReadingARange_ThenTheListIsEmpty()
    {
        using var repository = TestRepository.Create();
        var runner = repository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);

        var result = await new FileHistoryReader(runner)
            .ReadRangeAsync(location, Head, "f.txt", new LineRange(1, 1), 10, Token);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GivenACommitThatDeletedTheRange_WhenReadingFromBeforeIt_ThenThatCommitShowsOnlyRemovedLines()
    {
        using var repository = TestRepository.Create().Commit("add", new TestFile("f.txt", "a\nb\nc\nd\n"));
        repository.Commit("drop b and c", new TestFile("f.txt", "a\nd\n"));
        var runner = repository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);

        var result = await new FileHistoryReader(runner)
            .ReadRangeAsync(location, repository.Git("rev-parse", "HEAD~1").Trim(), "f.txt", new LineRange(2, 3), 10, Token);

        var entry = Assert.Single(result.Items);
        Assert.Equal("add", entry.Subject);
    }

    [Fact]
    public async Task GivenARangeChangedOnBothSidesOfAMerge_WhenReadingItsHistory_ThenBothBranchesCommitsAreListed()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("f.txt", "1\n2\n3\n4\n5\n6\n7\n"));
        repository.CheckoutNewBranch("side");
        repository.Commit("side edit 2", new TestFile("f.txt", "1\nS2\n3\n4\n5\n6\n7\n"));
        repository.Checkout("main");
        repository.Commit("main edit 6", new TestFile("f.txt", "1\n2\n3\n4\n5\nM6\n7\n"));
        repository.Merge("side");

        var entries = await ReadRangeAsync(repository, "f.txt", new LineRange(1, 7));

        var subjects = entries.Select(entry => entry.Subject).ToList();
        Assert.Contains("side edit 2", subjects);
        Assert.Contains("main edit 6", subjects);
        Assert.Equal("base", subjects[^1]);
    }

    [Fact]
    public async Task GivenUserConfigThatDropsThePathPrefixes_WhenReadingARange_ThenThePathsAreStillThoseOfTheFile()
    {
        using var repository = TestRepository.Create().Commit("add", new TestFile("a/b.txt", "x\ny\n"));
        repository.Git("config", "diff.noprefix", "true");

        var entries = await ReadRangeAsync(repository, "a/b.txt", new LineRange(1, 1));

        Assert.Equal("a/b.txt", Assert.Single(entries).Path);
    }

    private static async Task<IReadOnlyList<FileHistoryEntry>> ReadRangeAsync(
        TestRepository repository,
        string path,
        LineRange range)
    {
        var runner = repository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);
        var result = await new FileHistoryReader(runner).ReadRangeAsync(location, Head, path, range, 100, Token);

        Assert.Empty(result.Warnings);

        return result.Items;
    }

    private static async Task<IReadOnlyList<FileHistoryEntry>> ReadAsync(
        TestRepository repository,
        string path,
        string revision = Head,
        int limit = 100)
    {
        var runner = repository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);
        var result = await new FileHistoryReader(runner).ReadAsync(location, revision, path, limit, Token);

        Assert.Empty(result.Warnings);

        return result.Items;
    }

    private static async Task<List<DiffFile>> DiffOfAsync(TestRepository repository, FileHistoryEntry entry)
    {
        var runner = repository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);
        var files = new List<DiffFile>();
        await foreach (var file in new DiffReader(runner)
            .ReadAsync(location, FileHistoryDiffCommand.For(entry), cancellationToken: Token))
        {
            files.Add(file);
        }

        return files;
    }
}
