using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Trees;
using Gitunia.Git.Trees.Models;
using Xunit;

namespace Gitunia.Git.Tests.Trees;

public class CommitTreeReaderTests
{
    private static readonly GitRunner Runner = new();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly CommitTreeReader Reader = new(Runner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<IReadOnlyList<TreeEntry>> ReadAsync(TestRepository testRepository, string revision)
    {
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        Assert.True(ObjectId.TryParse(testRepository.Git("rev-parse", revision).Trim(), out var id));

        return await Reader.ReadAsync(location, id, cancellationToken: Token);
    }

    [Fact]
    public async Task GivenNestedDirectoriesAndOddNames_WhenReading_ThenThePathsMatchLsTree()
    {
        using var testRepository = TestRepository.Create().Commit(
            "root",
            new TestFile("a.txt", "a\n"),
            new TestFile("dir/sub/deep.txt", "deep\n"),
            new TestFile("dir/b.txt", "b\n"),
            new TestFile("with space/ünï cödé.txt", "u\n"),
            new TestFile("-dash.txt", "d\n"));

        var entries = await ReadAsync(testRepository, "HEAD");

        var expected = testRepository.Git("-c", "core.quotepath=off", "ls-tree", "-r", "--name-only", "HEAD")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(expected, entries.Select(entry => entry.Path));
        Assert.Equal(2, entries.Single(entry => entry.Path == "a.txt").Size);
    }

    [Fact]
    public async Task GivenARenameAndAnOlderCommit_WhenReading_ThenEachCommitShowsItsOwnTree()
    {
        using var testRepository = TestRepository.Create()
            .Commit(
                "one",
                new TestFile("old name.txt", "line1\nline2\nline3\nline4\n"),
                new TestFile("keep.txt", "k\n"));
        testRepository.Git("mv", "old name.txt", "sub dir-ünï.txt");
        testRepository.Commit("two", new TestFile("keep.txt", "k2\n"));

        var latest = await ReadAsync(testRepository, "HEAD");
        var first = await ReadAsync(testRepository, "HEAD~1");

        Assert.Equal(["keep.txt", "sub dir-ünï.txt"], latest.Select(entry => entry.Path));
        Assert.Equal(["keep.txt", "old name.txt"], first.Select(entry => entry.Path));
        Assert.Equal(3, latest.Single(entry => entry.Path == "keep.txt").Size);
        Assert.Equal(2, first.Single(entry => entry.Path == "keep.txt").Size);
    }

    [Fact]
    public async Task GivenSubmoduleExecutableAndSymlinkEntries_WhenReading_ThenTheyAreTypedAsSuch()
    {
        using var testRepository = TestRepository.Create()
            .Commit("root", new TestFile("a.txt", "a\n"), new TestFile("run.sh", "#!/bin/sh\n"));
        var blob = testRepository.Git("rev-parse", "HEAD:a.txt").Trim();
        var commit = testRepository.Head;
        testRepository.Git("update-index", "--chmod=+x", "run.sh");
        testRepository.Git("update-index", "--add", "--cacheinfo", $"160000,{commit},vendor/lib");
        testRepository.Git("update-index", "--add", "--cacheinfo", $"120000,{blob},link");
        testRepository.Git("commit", "-q", "-m", "special entries");

        var entries = await ReadAsync(testRepository, "HEAD");

        Assert.Equal(TreeEntryKind.Executable, entries.Single(entry => entry.Path == "run.sh").Kind);
        Assert.Equal(TreeEntryKind.Symlink, entries.Single(entry => entry.Path == "link").Kind);
        var submodule = entries.Single(entry => entry.Path == "vendor/lib");
        Assert.Equal(TreeEntryKind.Submodule, submodule.Kind);
        Assert.Null(submodule.Size);
        Assert.Equal(commit, submodule.Id.ToString());
    }

    [Fact]
    public async Task GivenACommitWithNoFiles_WhenReading_ThenTheTreeIsEmpty()
    {
        using var testRepository = TestRepository.Create();
        testRepository.Git("commit", "-q", "--allow-empty", "-m", "empty");

        var entries = await ReadAsync(testRepository, "HEAD");

        Assert.Empty(entries);
    }

    [Fact]
    public async Task GivenAnUnknownCommit_WhenReading_ThenItFailsWithAGitError()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("a.txt", "a\n"));
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        Assert.True(ObjectId.TryParse(new string('a', 40), out var missing));

        await Assert.ThrowsAnyAsync<Exception>(() => Reader.ReadAsync(location, missing, cancellationToken: Token));
    }
}
