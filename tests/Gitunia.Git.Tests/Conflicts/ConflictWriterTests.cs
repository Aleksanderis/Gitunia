using System.Text;
using Gitunia.Git.Conflicts;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Conflicts;

public class ConflictWriterTests
{
    private static readonly GitRunner Runner = IsolatedGit.CreateRunner();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly ConflictWriter Writer = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAResolvedText_WhenWriting_ThenTheFileHoldsExactlyThatTextAndTheIndexStaysConflicted()
    {
        using var repository = TestRepositories.Conflicted();
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        await Writer.WriteAsync(location, "conflict.txt", "line one\r\nüñí\r\n", Token);

        var bytes = await File.ReadAllBytesAsync(Path.Combine(repository.WorkingTreePath, "conflict.txt"), Token);
        Assert.Equal("line one\r\nüñí\r\n", Encoding.UTF8.GetString(bytes));
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Contains("conflict.txt", repository.Git("ls-files", "-u"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("a/../../outside.txt")]
    [InlineData(".git/hooks/pre-commit")]
    public async Task GivenAPathLeavingTheWorkingTree_WhenWriting_ThenItIsRefusedAndNothingIsWritten(string path)
    {
        using var repository = TestRepositories.Conflicted();
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        await Assert.ThrowsAsync<ArgumentException>(() => Writer.WriteAsync(location, path, "x", Token));

        Assert.False(File.Exists(Path.GetFullPath(Path.Combine(repository.WorkingTreePath, path))));
    }
}
