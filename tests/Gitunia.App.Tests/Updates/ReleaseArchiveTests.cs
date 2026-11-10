using Gitunia.App.Updates;
using Xunit;

namespace Gitunia.App.Tests.Updates;

public sealed class ReleaseArchiveTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("gitunia-archive-test-").FullName;

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void GivenATarGz_WhenExtractingTheBinary_ThenItIsFoundInTheNestedFolder()
    {
        var archive = WriteFile("a.tar.gz", FakeRelease.TarGz("gitunia-1.0.0-linux-x64/gitunia", [7, 8, 9]));
        var destination = Path.Combine(_directory, "out");

        ReleaseArchive.ExtractBinary(archive, "gitunia", destination);

        Assert.Equal([7, 8, 9], File.ReadAllBytes(destination));
    }

    [Fact]
    public void GivenAZip_WhenExtractingTheBinary_ThenItIsFoundInTheNestedFolder()
    {
        var archive = WriteFile("a.zip", FakeRelease.Zip("gitunia-1.0.0-win-x64/gitunia.exe", [4, 5]));
        var destination = Path.Combine(_directory, "out.exe");

        ReleaseArchive.ExtractBinary(archive, "gitunia.exe", destination);

        Assert.Equal([4, 5], File.ReadAllBytes(destination));
    }

    [Fact]
    public void GivenAnEntryWithTraversalInItsName_WhenExtracting_ThenOnlyTheChosenDestinationIsWritten()
    {
        var archive = WriteFile("a.tar.gz", FakeRelease.TarGz("../../evil/gitunia", [1]));
        var destination = Path.Combine(_directory, "out");

        ReleaseArchive.ExtractBinary(archive, "gitunia", destination);

        Assert.Equal(["a.tar.gz", "out"], Directory.GetFileSystemEntries(_directory).Select(Path.GetFileName).Order());
    }

    [Theory]
    [InlineData("a.tar.gz")]
    [InlineData("a.zip")]
    public void GivenAnArchiveWithoutTheBinary_WhenExtracting_ThenItFails(string name)
    {
        var archive = WriteFile(name, name.EndsWith(".zip", StringComparison.Ordinal)
            ? FakeRelease.Zip("x/other", [1])
            : FakeRelease.TarGz("x/other", [1]));

        var exception = Assert.Throws<UpdateException>(
            () => ReleaseArchive.ExtractBinary(archive, "gitunia", Path.Combine(_directory, "out")));

        Assert.Contains("holds no gitunia", exception.Message);
    }

    [Fact]
    public void GivenGarbage_WhenExtracting_ThenItFailsWithAMessage()
    {
        var archive = WriteFile("a.tar.gz", [1, 2, 3, 4]);

        Assert.Throws<UpdateException>(
            () => ReleaseArchive.ExtractBinary(archive, "gitunia", Path.Combine(_directory, "out")));
    }

    [Fact]
    public void GivenTheRightChecksum_WhenVerifying_ThenItPasses()
    {
        var content = new byte[] { 1, 2, 3 };
        var archive = WriteFile("a.tar.gz", content);

        ReleaseArchive.VerifyChecksum(archive, $"{FakeRelease.Sha256(content).ToUpperInvariant()}  a.tar.gz\n");
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc  a.tar.gz")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000  a.tar.gz")]
    public void GivenAMissingOrWrongChecksum_WhenVerifying_ThenItFails(string checksumText)
    {
        var archive = WriteFile("a.tar.gz", [1, 2, 3]);

        Assert.Throws<UpdateException>(() => ReleaseArchive.VerifyChecksum(archive, checksumText));
    }

    private string WriteFile(string name, byte[] content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, content);

        return path;
    }
}
