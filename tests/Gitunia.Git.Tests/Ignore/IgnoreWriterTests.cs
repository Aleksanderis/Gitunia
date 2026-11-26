using Gitunia.Git.Ignore;
using Gitunia.Git.Tests.Discard;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Ignore;

public class IgnoreWriterTests
{
    private static readonly IgnoreWriter Writer = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAnUntrackedFile_WhenAddingItsPath_ThenGitNoLongerListsItAndTheFileIsCreated()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("logs/debug.log", "x\n");
        var location = await DiscardSupport.LocationOfAsync(repository);
        Assert.Contains("?? logs/debug.log", Status(repository), StringComparison.Ordinal);

        var outcome = await Writer.AddAsync(location, "/logs/debug.log", Token);

        Assert.Equal(IgnoreAddOutcome.Added, outcome);
        Assert.Equal("/logs/debug.log\n", DiscardSupport.Read(repository, ".gitignore"));
        Assert.DoesNotContain("debug.log", Status(repository), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnExtensionPattern_WhenAdding_ThenEveryFileWithItDisappearsFromTheUntrackedList()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("one.log", "1\n").WriteFile("deep/two.log", "2\n").WriteFile("keep.txt", "k\n");
        var location = await DiscardSupport.LocationOfAsync(repository);

        await Writer.AddAsync(location, "*.log", Token);

        var status = Status(repository);
        Assert.DoesNotContain(".log", status, StringComparison.Ordinal);
        Assert.Contains("keep.txt", status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAFileWithoutATrailingNewline_WhenAdding_ThenTheOldLastLineIsKeptOnItsOwnLine()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile(".gitignore", "bin/"));
        var location = await DiscardSupport.LocationOfAsync(repository);

        await Writer.AddAsync(location, "/obj/", Token);

        Assert.Equal("bin/\n/obj/\n", DiscardSupport.Read(repository, ".gitignore"));
    }

    [Fact]
    public async Task GivenAFileWithWindowsLineEndings_WhenAdding_ThenTheNewLineUsesThemToo()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile(".gitignore", "bin/\r\n"));
        var location = await DiscardSupport.LocationOfAsync(repository);

        await Writer.AddAsync(location, "/obj/", Token);

        Assert.Equal("bin/\r\n/obj/\r\n", DiscardSupport.Read(repository, ".gitignore"));
    }

    [Fact]
    public async Task GivenThePatternIsAlreadyListed_WhenAdding_ThenNothingIsWritten()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile(".gitignore", "bin/\r\n*.log\n"));
        var location = await DiscardSupport.LocationOfAsync(repository);

        var first = await Writer.AddAsync(location, "bin/", Token);
        var second = await Writer.AddAsync(location, "*.log", Token);

        Assert.Equal(IgnoreAddOutcome.AlreadyListed, first);
        Assert.Equal(IgnoreAddOutcome.AlreadyListed, second);
        Assert.Equal("bin/\r\n*.log\n", DiscardSupport.Read(repository, ".gitignore"));
    }

    [Fact]
    public async Task GivenAPatternWithGlobCharactersEscaped_WhenAdding_ThenGitMatchesOnlyThatName()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows file names cannot contain *.");

        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("a*b.txt", "1\n").WriteFile("axb.txt", "2\n");
        var location = await DiscardSupport.LocationOfAsync(repository);

        await Writer.AddAsync(location, "/a\\*b.txt", Token);

        var status = Status(repository);
        Assert.DoesNotContain("a*b.txt", status, StringComparison.Ordinal);
        Assert.Contains("axb.txt", status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenABareRepository_WhenAdding_ThenItIsRefused()
    {
        using var repository = TestRepository.Create(bare: true);
        var location = await DiscardSupport.LocationOfAsync(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Writer.AddAsync(location, "*.log", Token));
    }

    private static string Status(TestRepository repository) => repository.Git("status", "--porcelain", "-uall");
}
