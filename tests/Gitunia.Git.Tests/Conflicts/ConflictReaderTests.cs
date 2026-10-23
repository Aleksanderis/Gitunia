using Gitunia.Git.Conflicts;
using Gitunia.Git.Conflicts.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Conflicts;

public class ConflictReaderTests
{
    private const string ConflictPath = "conflict.txt";

    private static readonly GitRunner Runner = IsolatedGit.CreateRunner();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly ConflictReader Reader = new(Runner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAConflictingMerge_WhenReading_ThenTheHunkHoldsBothSides()
    {
        using var repository = TestRepositories.Conflicted();

        var result = await ReadAsync(repository, ConflictPath);

        Assert.Equal(ConflictReadKind.Text, result.Kind);
        var hunk = Assert.Single(result.Parts, part => part.Hunk is not null).Hunk!;
        Assert.Equal("main\n", hunk.Ours);
        Assert.Equal("other\n", hunk.Theirs);
        Assert.Null(hunk.Base);
    }

    [Fact]
    public async Task GivenAConflictingRebase_WhenReading_ThenOursIsTheUpstreamSide()
    {
        using var repository = TestRepositories.RebaseConflicted();

        var result = await ReadAsync(repository, ConflictPath);

        var hunk = Assert.Single(result.Parts, part => part.Hunk is not null).Hunk!;
        Assert.Equal("main\n", hunk.Ours);
        Assert.Equal("other\n", hunk.Theirs);
    }

    [Fact]
    public async Task GivenAConflictingCherryPick_WhenReading_ThenTheHunkHoldsBothSides()
    {
        using var repository = TestRepositories.CherryPickConflicted();

        var result = await ReadAsync(repository, ConflictPath);

        var hunk = Assert.Single(result.Parts, part => part.Hunk is not null).Hunk!;
        Assert.Equal("main\n", hunk.Ours);
        Assert.Equal("other\n", hunk.Theirs);
    }

    [Theory]
    [InlineData("diff3")]
    [InlineData("zdiff3")]
    public async Task GivenABaseStyle_WhenReading_ThenTheBaseTextIsInTheHunk(string style)
    {
        using var repository = TestRepository.Create();
        repository.Git("config", "merge.conflictStyle", style);
        repository.Commit("Initial", new TestFile(ConflictPath, "original\n"))
            .CheckoutNewBranch("other")
            .Commit("Other change", new TestFile(ConflictPath, "other\n"))
            .Checkout("main")
            .Commit("Main change", new TestFile(ConflictPath, "main\n"))
            .MergeExpectingConflict("other");

        var result = await ReadAsync(repository, ConflictPath);

        var hunk = Assert.Single(result.Parts, part => part.Hunk is not null).Hunk!;
        Assert.Equal("original\n", hunk.Base);
    }

    [Fact]
    public async Task GivenSeveralHunksAndCommonLines_WhenReading_ThenJoiningThePartsGivesTheFileBack()
    {
        using var repository = TestRepository.Create();
        var original = "1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n11\n12\n";
        repository.Commit("Initial", new TestFile(ConflictPath, original))
            .CheckoutNewBranch("other")
            .Commit("Other", new TestFile(ConflictPath, original.Replace("2\n", "two\n").Replace("11\n", "eleven\n")))
            .Checkout("main")
            .Commit("Main", new TestFile(ConflictPath, original.Replace("2\n", "II\n").Replace("11\n", "XI\n")))
            .MergeExpectingConflict("other");

        var result = await ReadAsync(repository, ConflictPath);

        Assert.Equal(2, result.HunkCount);
        var onDisk = await File.ReadAllTextAsync(Path.Combine(repository.WorkingTreePath, ConflictPath), Token);
        Assert.Equal(onDisk, string.Concat(result.Parts.Select(part => part.Common ?? part.Hunk!.Raw)));
    }

    [Fact]
    public async Task GivenBothSidesAddedTheFile_WhenReading_ThenItIsUnsupportedAsAddAdd()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("base.txt", "x\n"));
        repository.CheckoutNewBranch("other").Commit("Add", new TestFile("new.txt", "other\n"));
        repository.Checkout("main").Commit("Add", new TestFile("new.txt", "main\n")).MergeExpectingConflict("other");

        var result = await ReadAsync(repository, "new.txt");

        Assert.Equal(ConflictReadKind.Unsupported, result.Kind);
        Assert.Contains("add/add", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenOneSideDeletedTheFile_WhenReading_ThenItIsUnsupportedAsDeleteModify()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile(ConflictPath, "x\n"));
        repository.CheckoutNewBranch("other").Delete(ConflictPath);
        repository.Git("commit", "-a", "-m", "Delete");
        repository.Checkout("main").Commit("Change", new TestFile(ConflictPath, "main\n"))
            .MergeExpectingConflict("other");

        var result = await ReadAsync(repository, ConflictPath);

        Assert.Equal(ConflictReadKind.Unsupported, result.Kind);
        Assert.Contains("delete/modify", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenABinaryConflict_WhenReading_ThenItIsUnsupported()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("data.bin", "a\0b"));
        repository.CheckoutNewBranch("other").Commit("Other", new TestFile("data.bin", "o\0ther"));
        repository.Checkout("main").Commit("Main", new TestFile("data.bin", "m\0ain")).MergeExpectingConflict("other");

        var result = await ReadAsync(repository, "data.bin");

        Assert.Equal(ConflictReadKind.Unsupported, result.Kind);
        Assert.Empty(result.Parts);
    }

    [Fact]
    public async Task GivenTheMarkersWereRemovedByHand_WhenReading_ThenItIsUnsupportedWithAReason()
    {
        using var repository = TestRepositories.Conflicted();
        repository.WriteFile(ConflictPath, "resolved\n");

        var result = await ReadAsync(repository, ConflictPath);

        Assert.Equal(ConflictReadKind.Unsupported, result.Kind);
        Assert.Contains("no conflict markers", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenHalfTheMarkersWereRemovedByHand_WhenReading_ThenItIsUnsupportedWithTheProblem()
    {
        using var repository = TestRepositories.Conflicted();
        repository.WriteFile(ConflictPath, "<<<<<<< HEAD\nmain\n=======\nother\n");

        var result = await ReadAsync(repository, ConflictPath);

        Assert.Equal(ConflictReadKind.Unsupported, result.Kind);
        Assert.Contains("never closed", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAPathThatIsNotConflicted_WhenReading_ThenItIsUnsupported()
    {
        using var repository = TestRepositories.Conflicted();
        repository.WriteFile("other.txt", "x\n");

        var result = await ReadAsync(repository, "other.txt");

        Assert.Equal(ConflictReadKind.Unsupported, result.Kind);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenReading_ThenItIsCancelled()
    {
        using var repository = TestRepositories.Conflicted();
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Reader.ReadAsync(location, ConflictPath, source.Token));
    }

    private static async Task<ConflictReadResult> ReadAsync(TestRepository repository, string path)
    {
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        return await Reader.ReadAsync(location, path, Token);
    }
}
