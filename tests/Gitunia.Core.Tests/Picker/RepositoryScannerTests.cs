using Gitunia.Core.Picker;
using Gitunia.Core.Tests.Persistence;
using Xunit;

namespace Gitunia.Core.Tests.Picker;

public class RepositoryScannerTests
{
    private static string MakeRepository(string root, params string[] segments)
    {
        var directory = Path.Combine([root, .. segments]);
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        return directory;
    }

    [Fact]
    public void GivenRepositoriesAtSeveralDepths_WhenScanning_ThenAllWithinTheDepthLimitAreFoundSorted()
    {
        using var temporary = new TemporaryDirectory();
        var beta = MakeRepository(temporary.DirectoryPath, "beta");
        var alpha = MakeRepository(temporary.DirectoryPath, "group", "alpha");
        MakeRepository(temporary.DirectoryPath, "a", "b", "c", "too-deep");

        var found = RepositoryScanner.Scan([temporary.DirectoryPath], 3, 100, CancellationToken.None);

        Assert.Equal([alpha, beta], found);
    }

    [Fact]
    public void GivenRepositoryWithNestedRepository_WhenScanning_ThenOnlyTheOuterOneIsReported()
    {
        using var temporary = new TemporaryDirectory();
        var outer = MakeRepository(temporary.DirectoryPath, "outer");
        MakeRepository(outer, "vendor-copy");

        var found = RepositoryScanner.Scan([temporary.DirectoryPath], 3, 100, CancellationToken.None);

        Assert.Equal([outer], found);
    }

    [Fact]
    public void GivenWorktreeStyleGitFile_WhenScanning_ThenTheDirectoryCounts()
    {
        using var temporary = new TemporaryDirectory();
        var worktree = Path.Combine(temporary.DirectoryPath, "linked");
        Directory.CreateDirectory(worktree);
        File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: /elsewhere");

        var found = RepositoryScanner.Scan([temporary.DirectoryPath], 3, 100, CancellationToken.None);

        Assert.Equal([worktree], found);
    }

    [Fact]
    public void GivenHiddenAndDependencyDirectories_WhenScanning_ThenTheyAreSkipped()
    {
        using var temporary = new TemporaryDirectory();
        MakeRepository(temporary.DirectoryPath, ".cache", "hidden");
        MakeRepository(temporary.DirectoryPath, "node_modules", "dependency");
        var visible = MakeRepository(temporary.DirectoryPath, "visible");

        var found = RepositoryScanner.Scan([temporary.DirectoryPath], 3, 100, CancellationToken.None);

        Assert.Equal([visible], found);
    }

    [Fact]
    public void GivenMoreRepositoriesThanTheLimit_WhenScanning_ThenTheResultIsCapped()
    {
        using var temporary = new TemporaryDirectory();
        for (var index = 0; index < 5; index++)
        {
            MakeRepository(temporary.DirectoryPath, $"repo{index}");
        }

        var found = RepositoryScanner.Scan([temporary.DirectoryPath], 3, 3, CancellationToken.None);

        Assert.Equal(3, found.Length);
    }

    [Fact]
    public void GivenMissingRoot_WhenScanning_ThenNothingIsFoundAndNothingThrows()
    {
        using var temporary = new TemporaryDirectory();

        var found = RepositoryScanner.Scan([Path.Combine(temporary.DirectoryPath, "absent")], 3, 100, CancellationToken.None);

        Assert.Empty(found);
    }

    [Fact]
    public void GivenHomeWithSomeConventionalFolders_WhenChoosingDefaultRoots_ThenOnlyExistingOnesAreUsed()
    {
        var home = Path.Combine(Path.GetTempPath(), "scanner-home");
        var existing = new HashSet<string> { Path.Combine(home, "Projects"), Path.Combine(home, "src") };

        var roots = RepositoryScanner.DefaultRoots(home, existing.Contains);

        Assert.Equal([Path.Combine(home, "Projects"), Path.Combine(home, "src")], roots);
    }
}
