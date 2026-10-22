using Gitunia.Git.Blobs;
using Gitunia.Git.Blobs.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Blobs;

public class BlobReaderTests
{
    private static readonly GitRunner Runner = IsolatedGit.CreateRunner();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly BlobReader Reader = new(Runner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<BlobContent> ReadAsync(TestRepository repository, string path, long? knownSize = null)
    {
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
        Assert.True(ObjectId.TryParse(repository.Git("rev-parse", $"HEAD:{path}").Trim(), out var blob));

        return await Reader.ReadAsync(location, blob, knownSize, Token);
    }

    [Fact]
    public async Task GivenATextFile_WhenReading_ThenTheTextAndSizeComeBack()
    {
        using var repository = TestRepository.Create().Commit("root", new TestFile("a.txt", "héllo\nworld\n"));

        var content = await ReadAsync(repository, "a.txt");

        Assert.Equal(BlobKind.Text, content.Kind);
        Assert.Equal("héllo\nworld\n", content.Text);
        Assert.Equal(13, content.Size);
    }

    [Fact]
    public async Task GivenAFileWithANulByte_WhenReading_ThenItIsBinaryWithoutText()
    {
        using var repository = TestRepository.Create().Commit("root", new TestFile("b.bin", "ab\0cd"));

        var content = await ReadAsync(repository, "b.bin");

        Assert.Equal(BlobKind.Binary, content.Kind);
        Assert.Equal(string.Empty, content.Text);
        Assert.Equal(5, content.Size);
    }

    [Fact]
    public async Task GivenAnEmptyFile_WhenReading_ThenItIsEmptyText()
    {
        using var repository = TestRepository.Create().Commit("root", new TestFile("e.txt", string.Empty));

        var content = await ReadAsync(repository, "e.txt");

        Assert.Equal(new BlobContent(BlobKind.Text, string.Empty, 0), content);
    }

    [Fact]
    public async Task GivenAFileOverTheCap_WhenReading_ThenItIsTooLargeAndHasNoText()
    {
        var text = new string('x', BlobReader.MaximumBytes + 1);
        using var repository = TestRepository.Create().Commit("root", new TestFile("big.txt", text));

        var content = await ReadAsync(repository, "big.txt");

        Assert.Equal(BlobKind.TooLarge, content.Kind);
        Assert.Equal(string.Empty, content.Text);
        Assert.Equal(BlobReader.MaximumBytes + 1, content.Size);
    }

    [Fact]
    public async Task GivenAFileExactlyAtTheCap_WhenReading_ThenItIsShown()
    {
        var text = new string('x', BlobReader.MaximumBytes);
        using var repository = TestRepository.Create().Commit("root", new TestFile("edge.txt", text));

        var content = await ReadAsync(repository, "edge.txt");

        Assert.Equal(BlobKind.Text, content.Kind);
        Assert.Equal(BlobReader.MaximumBytes, content.Text.Length);
    }

    [Fact]
    public async Task GivenAKnownSizeOverTheCap_WhenReading_ThenGitIsNotAskedForTheBlob()
    {
        using var repository = TestRepository.Create().Commit("root", new TestFile("a.txt", "small\n"));

        var content = await ReadAsync(repository, "a.txt", knownSize: BlobReader.MaximumBytes + 5);

        Assert.Equal(new BlobContent(BlobKind.TooLarge, string.Empty, BlobReader.MaximumBytes + 5), content);
    }
}
