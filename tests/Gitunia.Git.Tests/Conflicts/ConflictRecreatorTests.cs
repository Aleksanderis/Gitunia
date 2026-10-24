using Gitunia.Git.Conflicts;
using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Conflicts;

public class ConflictRecreatorTests
{
    private static readonly GitRunner Runner = IsolatedGit.CreateRunner();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly ConflictRecreator Recreator = new(Runner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAResolvedByHandFile_WhenRecreating_ThenTheMarkersComeBack()
    {
        using var repository = TestRepositories.Conflicted();
        var path = Path.Combine(repository.WorkingTreePath, "conflict.txt");
        File.WriteAllText(path, "resolved by hand\n");
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        await Recreator.RecreateAsync(location, "conflict.txt", Token);

        var text = File.ReadAllText(path);
        Assert.Contains("<<<<<<<", text, StringComparison.Ordinal);
        Assert.Contains("main\n=======\nother\n", text.Replace("\r", string.Empty), StringComparison.Ordinal);
        Assert.Contains("conflict.txt", repository.Git("ls-files", "-u"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAFileMarkedResolved_WhenRecreating_ThenTheIndexIsConflictedAgain()
    {
        using var repository = TestRepositories.Conflicted();
        var path = Path.Combine(repository.WorkingTreePath, "conflict.txt");
        File.WriteAllText(path, "resolved\n");
        repository.Git("add", "conflict.txt");
        Assert.DoesNotContain("conflict.txt", repository.Git("ls-files", "-u"), StringComparison.Ordinal);
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        await Recreator.RecreateAsync(location, "conflict.txt", Token);

        Assert.Contains("conflict.txt", repository.Git("ls-files", "-u"), StringComparison.Ordinal);
        Assert.Contains("<<<<<<<", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAPathGitDoesNotKnow_WhenRecreating_ThenGitsRefusalIsThrown()
    {
        using var repository = TestRepositories.Conflicted();
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => Recreator.RecreateAsync(location, "missing.txt", Token));
    }

    [Fact]
    public async Task GivenAGlobCharacterInThePath_WhenRecreating_ThenOnlyThatLiteralFileIsTouched()
    {
        using var repository = TestRepositories.Conflicted();
        var path = Path.Combine(repository.WorkingTreePath, "conflict.txt");
        File.WriteAllText(path, "keep me\n");
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => Recreator.RecreateAsync(location, "conflict.*", Token));

        Assert.Equal("keep me\n", File.ReadAllText(path));
    }
}
