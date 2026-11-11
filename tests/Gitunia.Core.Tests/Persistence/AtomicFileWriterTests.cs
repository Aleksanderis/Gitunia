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
    public async Task GivenTargetHeldWithoutDeleteSharing_WhenItIsReleasedShortly_ThenWriteSucceeds()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows denies a rename over an open file.");
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.DirectoryPath, "data.json");
        await File.WriteAllTextAsync(path, "original", TestContext.Current.CancellationToken);
        var holder = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var release = Task.Run(
            async () =>
            {
                await Task.Delay(60, TestContext.Current.CancellationToken);
                await holder.DisposeAsync();
            },
            TestContext.Current.CancellationToken);

        await AtomicFileWriter.WriteAsync(path, "replacement"u8.ToArray(), TestContext.Current.CancellationToken);
        await release;

        Assert.Equal("replacement", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GivenTargetOpenWithDeleteSharing_WhenWriting_ThenWriteSucceedsAtOnce()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.DirectoryPath, "data.json");
        await File.WriteAllTextAsync(path, "original", TestContext.Current.CancellationToken);
        await using var reader = SharedFile.OpenRead(path);

        await AtomicFileWriter.WriteAsync(path, "replacement"u8.ToArray(), TestContext.Current.CancellationToken);

        Assert.Equal("replacement", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GivenStaleAndFreshTemporaryFiles_WhenRemovingStale_ThenOnlyOldGuidNamedOnesGo()
    {
        using var directory = new TemporaryDirectory();
        var stale = Path.Combine(directory.DirectoryPath, $"session.json.{Guid.NewGuid():N}.tmp");
        var fresh = Path.Combine(directory.DirectoryPath, $"session.json.{Guid.NewGuid():N}.tmp");
        var foreign = Path.Combine(directory.DirectoryPath, "notes.tmp");
        var target = Path.Combine(directory.DirectoryPath, "session.json");
        foreach (var file in new[] { stale, fresh, foreign, target })
        {
            File.WriteAllText(file, "x");
        }

        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddMinutes(-5));
        File.SetLastWriteTimeUtc(foreign, DateTime.UtcNow.AddMinutes(-5));

        AtomicFileWriter.RemoveStaleTemporaryFiles(directory.DirectoryPath);

        Assert.False(File.Exists(stale));
        Assert.True(File.Exists(fresh));
        Assert.True(File.Exists(foreign));
        Assert.True(File.Exists(target));
    }

    [Fact]
    public void GivenMissingDirectory_WhenRemovingStale_ThenNothingThrows()
    {
        using var directory = new TemporaryDirectory();

        AtomicFileWriter.RemoveStaleTemporaryFiles(Path.Combine(directory.DirectoryPath, "missing"));
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
