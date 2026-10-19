using System.Text;
using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Tests.Persistence;
using Xunit;

namespace Gitunia.Core.Tests.Config;

public sealed class ConfigWriterTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public async Task GivenNoFile_WhenSetting_ThenTheFileAndDirectoryAreCreatedWithOnlyThatKey()
    {
        var writer = new ConfigWriter(Path.Combine(_directory.DirectoryPath, "nested"));
        var snapshot = await writer.ReadAsync(TestContext.Current.CancellationToken);

        var result = await writer.SetAsync(["theme"], "\"dark\"", snapshot, TestContext.Current.CancellationToken);

        Assert.Equal(ConfigWriteStatus.Written, result.Status);
        Assert.Equal("{\n  \"theme\": \"dark\"\n}\n", await File.ReadAllTextAsync(writer.FilePath, Token));
    }

    [Fact]
    public async Task GivenAFileWithComments_WhenSetting_ThenTheOtherBytesAreUntouchedAndNoTemporaryFileRemains()
    {
        var writer = Create("{\n  // keep me\n  \"theme\": \"auto\", /* x */\n}\n");
        var snapshot = await writer.ReadAsync(Token);

        var result = await writer.SetAsync(["theme"], "\"dark\"", snapshot, Token);

        Assert.Equal(ConfigWriteStatus.Written, result.Status);
        var expected = "{\n  // keep me\n  \"theme\": \"dark\", /* x */\n}\n";
        Assert.Equal(expected, await File.ReadAllTextAsync(writer.FilePath, Token));
        Assert.Equal([writer.FilePath], Directory.GetFiles(_directory.DirectoryPath));
    }

    [Fact]
    public async Task GivenWrittenResult_WhenSettingAgainWithItsSnapshot_ThenItIsNotStale()
    {
        var writer = Create("{}");
        var first = await writer.SetAsync(["theme"], "\"dark\"", await writer.ReadAsync(Token), Token);

        var second = await writer.SetAsync(["frameStyle"], "\"square\"", first.Snapshot, Token);

        Assert.Equal(ConfigWriteStatus.Written, second.Status);
        var text = await File.ReadAllTextAsync(writer.FilePath, Token);
        Assert.Contains("\"theme\": \"dark\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheFileChangedAfterReading_WhenSetting_ThenNothingIsWrittenAndTheNewContentIsReturned()
    {
        var writer = Create("{ \"theme\": \"auto\" }");
        var snapshot = await writer.ReadAsync(Token);
        await File.WriteAllTextAsync(writer.FilePath, "{ \"theme\": \"light\" }", Token);

        var result = await writer.SetAsync(["theme"], "\"dark\"", snapshot, Token);

        Assert.Equal(ConfigWriteStatus.Stale, result.Status);
        Assert.Equal("{ \"theme\": \"light\" }", result.Snapshot.Text);
        Assert.Equal("{ \"theme\": \"light\" }", await File.ReadAllTextAsync(writer.FilePath, Token));
    }

    [Fact]
    public async Task GivenTheFileAppearedAfterReadingMissing_WhenSetting_ThenItIsStale()
    {
        var writer = new ConfigWriter(_directory.DirectoryPath);
        var snapshot = await writer.ReadAsync(Token);
        await File.WriteAllTextAsync(writer.FilePath, "{}", Token);

        var result = await writer.SetAsync(["theme"], "\"dark\"", snapshot, Token);

        Assert.Equal(ConfigWriteStatus.Stale, result.Status);
    }

    [Fact]
    public async Task GivenAFileThatDoesNotParse_WhenSetting_ThenItIsRefusedAndLeftByteForByte()
    {
        const string broken = "{ \"theme\": \"auto\" \"x\": 1 }";
        var writer = Create(broken);

        var result = await writer.SetAsync(["theme"], "\"dark\"", await writer.ReadAsync(Token), Token);

        Assert.Equal(ConfigWriteStatus.Refused, result.Status);
        Assert.Contains("line 1", result.Message, StringComparison.Ordinal);
        Assert.Equal(broken, await File.ReadAllTextAsync(writer.FilePath, Token));
    }

    [Fact]
    public async Task GivenAFileWithBomAndCrLf_WhenSetting_ThenBomAndLineEndingsAreKept()
    {
        var writer = new ConfigWriter(_directory.DirectoryPath);
        var original = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("{\r\n  \"a\": 1\r\n}\r\n"));
        await File.WriteAllBytesAsync(writer.FilePath, [.. original], Token);

        var result = await writer.SetAsync(["theme"], "\"dark\"", await writer.ReadAsync(Token), Token);

        Assert.Equal(ConfigWriteStatus.Written, result.Status);
        var bytes = await File.ReadAllBytesAsync(writer.FilePath, Token);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Equal(
            "{\r\n  \"a\": 1,\r\n  \"theme\": \"dark\"\r\n}\r\n",
            Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Fact]
    public async Task GivenACommentOnlyFile_WhenSetting_ThenTheCommentSurvives()
    {
        var writer = Create("// nothing yet\n");

        var result = await writer.SetAsync(["theme"], "\"dark\"", await writer.ReadAsync(Token), Token);

        Assert.Equal(ConfigWriteStatus.Written, result.Status);
        var text = await File.ReadAllTextAsync(writer.FilePath, Token);
        Assert.StartsWith("// nothing yet\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenASymlinkedFile_WhenSetting_ThenTheTargetChangesAndTheLinkStays()
    {
        var target = Path.Combine(_directory.DirectoryPath, "dotfiles", "real.json");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllTextAsync(target, "{ \"theme\": \"auto\" }", Token);
        var configDirectory = Path.Combine(_directory.DirectoryPath, "config");
        Directory.CreateDirectory(configDirectory);
        var writer = new ConfigWriter(configDirectory);
        try
        {
            File.CreateSymbolicLink(writer.FilePath, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        var result = await writer.SetAsync(["theme"], "\"dark\"", await writer.ReadAsync(Token), Token);

        Assert.Equal(ConfigWriteStatus.Written, result.Status);
        Assert.NotNull(new FileInfo(writer.FilePath).LinkTarget);
        Assert.Equal("{ \"theme\": \"dark\" }", await File.ReadAllTextAsync(target, Token));
        Assert.Equal([writer.FilePath], Directory.GetFiles(configDirectory));
        Assert.Equal([target], Directory.GetFiles(Path.GetDirectoryName(target)!));
    }

    [Fact]
    public async Task GivenFilePermissions_WhenSetting_ThenTheyAreKept()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var writer = Create("{}");
        File.SetUnixFileMode(writer.FilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        await writer.SetAsync(["theme"], "\"dark\"", await writer.ReadAsync(Token), Token);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(writer.FilePath));
    }

    [Fact]
    public async Task GivenACancelledWrite_WhenSetting_ThenTheOriginalFileSurvives()
    {
        var writer = Create("{ \"theme\": \"auto\" }");
        var snapshot = await writer.ReadAsync(Token);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => writer.SetAsync(["theme"], "\"dark\"", snapshot, cancellation.Token));

        Assert.Equal("{ \"theme\": \"auto\" }", await File.ReadAllTextAsync(writer.FilePath, Token));
        Assert.Equal([writer.FilePath], Directory.GetFiles(_directory.DirectoryPath));
    }

    [Fact]
    public async Task GivenAFileThatIsNotUtf8_WhenReading_ThenTheSnapshotCarriesAnError()
    {
        var writer = new ConfigWriter(_directory.DirectoryPath);
        await File.WriteAllBytesAsync(writer.FilePath, [0x7B, 0xFF, 0x7D], Token);

        var snapshot = await writer.ReadAsync(Token);

        Assert.NotNull(snapshot.Error);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ConfigWriter Create(string content)
    {
        var writer = new ConfigWriter(_directory.DirectoryPath);
        File.WriteAllText(writer.FilePath, content);

        return writer;
    }
}
