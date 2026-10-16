using System.Text;
using Gitunia.Platform.Files;
using Xunit;

namespace Gitunia.Platform.Tests.Files;

public class RawPathFileReaderTests
{
    [Fact]
    public void GivenExistingFile_WhenReading_ThenItsBytesAreReturned()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Raw byte paths are not read on Windows.");
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "hello");

            var content = RawPathFileReader.TryReadAll(Encoding.UTF8.GetBytes(path));

            Assert.Equal("hello", Encoding.UTF8.GetString(content!));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GivenMissingFileOrDirectory_WhenReading_ThenNullIsReturned()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Raw byte paths are not read on Windows.");
        var missing = Path.Combine(Path.GetTempPath(), $"gitunia-missing-{Guid.NewGuid()}");

        Assert.Null(RawPathFileReader.TryReadAll(Encoding.UTF8.GetBytes(missing)));
        Assert.Null(RawPathFileReader.TryReadAll(Encoding.UTF8.GetBytes(Path.GetTempPath())));
    }
}
