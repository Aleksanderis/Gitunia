using Gitunia.Core.Persistence;
using Xunit;

namespace Gitunia.Core.Tests.Persistence;

public class AtomicFileWriterTests
{
    [Fact]
    public async Task GivenExistingFile_WhenWriteIsCancelled_ThenOriginalContentSurvivesAndNoTemporaryFileRemains()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.DirectoryPath, "data.json");
        await File.WriteAllTextAsync(path, "original", TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => AtomicFileWriter.WriteAsync(path, "replacement"u8.ToArray(), cancellation.Token));

        Assert.Equal("original", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFiles(directory.DirectoryPath));
    }

    [Fact]
    public async Task GivenMissingDirectory_WhenWriting_ThenDirectoryIsCreatedAndContentWritten()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.DirectoryPath, "a", "b", "data.json");

        await AtomicFileWriter.WriteAsync(path, "hello"u8.ToArray(), TestContext.Current.CancellationToken);

        Assert.Equal("hello", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }
}
