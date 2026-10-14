using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Repositories;

public class RepositoryDiscoveryTests
{
    private static readonly GitRunner Runner = new();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly RefReader Refs = new(Runner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenWorkingTreeRoot_WhenDiscovering_ThenLocationIsResolved()
    {
        using var testRepository = TestRepositories.Linear(1);

        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var expected = Canonical(testRepository, testRepository.WorkingTreePath);
        Assert.Equal(expected, Normalize(location.WorkingTreePath!));
        Assert.Equal(Path.Combine(expected, ".git"), Normalize(location.GitDirectory));
        Assert.Equal(Normalize(location.GitDirectory), Normalize(location.CommonDirectory));
        Assert.False(location.IsBare);
        Assert.False(location.IsLinkedWorktree);
    }

    [Fact]
    public async Task GivenSubdirectory_WhenDiscovering_ThenResolvesToRepositoryRoot()
    {
        using var testRepository = TestRepository.Create().Commit("Nested", new TestFile(Path.Combine("a", "b", "file.txt"), "x\n"));

        var location = await Discovery.DiscoverAsync(Path.Combine(testRepository.WorkingTreePath, "a", "b"), Token);

        Assert.Equal(Canonical(testRepository, testRepository.WorkingTreePath), Normalize(location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenBareRepository_WhenDiscovering_ThenThereIsNoWorkingTree()
    {
        using var testRepository = TestRepository.Create(bare: true);

        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        Assert.True(location.IsBare);
        Assert.Null(location.WorkingTreePath);
        Assert.Equal(Normalize(location.GitDirectory), Normalize(location.RootPath));
    }

    [Fact]
    public async Task GivenLinkedWorktree_WhenDiscovering_ThenItIsFlaggedAndSharesCommonDirectory()
    {
        using var testRepository = TestRepositories.WithWorktree(out var worktreePath);
        var main = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var linked = await Discovery.DiscoverAsync(worktreePath, Token);

        Assert.True(linked.IsLinkedWorktree);
        Assert.NotEqual(Normalize(main.RootPath), Normalize(linked.RootPath));
        Assert.Equal(Normalize(main.CommonDirectory), Normalize(linked.CommonDirectory));
        Assert.Equal(Canonical(testRepository, worktreePath), Normalize(linked.WorkingTreePath!));
        Assert.Equal("wt-branch", (await Refs.ReadHeadAsync(linked, Token)).BranchName);
        Assert.Equal("main", (await Refs.ReadHeadAsync(main, Token)).BranchName);
    }

    [Fact]
    public async Task GivenPathInsideGitDirectory_WhenDiscovering_ThenWorkingTreeIsStillFound()
    {
        using var testRepository = TestRepositories.Linear(1);

        var location = await Discovery.DiscoverAsync(Path.Combine(testRepository.WorkingTreePath, ".git", "refs"), Token);

        Assert.Equal(Canonical(testRepository, testRepository.WorkingTreePath), Normalize(location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenPathInsideLinkedWorktreeGitDirectory_WhenDiscovering_ThenLinkedWorkingTreeIsFound()
    {
        using var testRepository = TestRepositories.WithWorktree(out var worktreePath);
        var linkedGitDirectory = Path.Combine(testRepository.WorkingTreePath, ".git", "worktrees", "wt");

        var location = await Discovery.DiscoverAsync(linkedGitDirectory, Token);

        Assert.True(location.IsLinkedWorktree);
        Assert.Equal(Canonical(testRepository, worktreePath), Normalize(location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenDirectoryOutsideAnyRepository_WhenDiscovering_ThenNotARepository()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"gitunia-plain-{Guid.NewGuid().ToString("N")}");
        Directory.CreateDirectory(directory);
        try
        {
            await Assert.ThrowsAsync<NotARepositoryException>(() => Discovery.DiscoverAsync(directory, Token));
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task GivenMissingDirectory_WhenDiscovering_ThenNotARepository()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"gitunia-missing-{Guid.NewGuid().ToString("N")}");

        await Assert.ThrowsAsync<NotARepositoryException>(() => Discovery.DiscoverAsync(missing, Token));
    }

    [Fact]
    public async Task GivenPathWithSpacesAndUnicode_WhenDiscovering_ThenResolves()
    {
        using var testRepository = TestRepository.Create().Commit("Odd", new TestFile(Path.Combine("sub dir ünï", "file.txt"), "x\n"));

        var location = await Discovery.DiscoverAsync(Path.Combine(testRepository.WorkingTreePath, "sub dir ünï"), Token);

        Assert.Equal(Canonical(testRepository, testRepository.WorkingTreePath), Normalize(location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenLinkedWorktreeOfBareRepository_WhenDiscovering_ThenWorkingTreeIsLinkedAndCommonDirectoryIsBare()
    {
        using var source = TestRepositories.Linear(1);
        using var bare = TestRepository.Create(bare: true);
        source.Git("push", "-q", bare.WorkingTreePath, "main");
        var worktreePath = bare.AddWorktree("from-bare", "main");

        var location = await Discovery.DiscoverAsync(worktreePath, Token);
        var bareRepository = await Discovery.DiscoverAsync(bare.WorkingTreePath, Token);

        Assert.True(location.IsLinkedWorktree);
        Assert.False(location.IsBare);
        Assert.Equal(Normalize(bareRepository.GitDirectory), Normalize(location.CommonDirectory));
        Assert.Equal("main", (await Refs.ReadHeadAsync(location, Token)).BranchName);
    }

    [Fact]
    public async Task GivenRelativeWorktreePointer_WhenDiscoveringInsideItsGitDirectory_ThenWorkingTreeIsFound()
    {
        using var testRepository = TestRepositories.Linear(1);
        var worktreePath = Path.Combine(Path.GetDirectoryName(testRepository.WorkingTreePath)!, "relative-wt");
        string output;
        try
        {
            output = testRepository.Git(["worktree", "add", "-q", "--relative-paths", "-b", "rel", worktreePath], null);
        }
        catch (GitCommandFailedException)
        {
            return; // Git older than 2.48 has no --relative-paths
        }

        Assert.NotNull(output);
        var location = await Discovery.DiscoverAsync(Path.Combine(testRepository.WorkingTreePath, ".git", "worktrees", "relative-wt"), Token);

        Assert.Equal(Canonical(testRepository, worktreePath), Normalize(location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenSeparateGitDirectory_WhenDiscoveringInsideIt_ThenFailsUnlessCoreWorktreeIsSet()
    {
        using var testRepository = TestRepository.Create();
        var separate = Path.Combine(Path.GetDirectoryName(testRepository.WorkingTreePath)!, "separate.git");
        var worktree = Path.Combine(Path.GetDirectoryName(testRepository.WorkingTreePath)!, "separate-work");
        Directory.CreateDirectory(worktree);
        testRepository.Git("init", "-q", "-b", "main", "--separate-git-dir", separate, worktree);

        await Assert.ThrowsAsync<NotARepositoryException>(() => Discovery.DiscoverAsync(separate, Token));

        testRepository.Git(["-C", separate, "config", "core.worktree", worktree], null);
        var location = await Discovery.DiscoverAsync(separate, Token);

        Assert.Equal(Canonical(testRepository, worktree), Normalize(location.WorkingTreePath!));
    }

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

    private static string Canonical(TestRepository testRepository, string path) =>
        Normalize(testRepository.Git(["-C", path, "rev-parse", "--path-format=absolute", "--show-toplevel"], null).Trim());
}
