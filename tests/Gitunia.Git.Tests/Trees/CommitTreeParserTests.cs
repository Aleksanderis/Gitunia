using System.Text;
using Gitunia.Git.Trees;
using Gitunia.Git.Trees.Models;
using Xunit;

namespace Gitunia.Git.Tests.Trees;

public class CommitTreeParserTests
{
    private const string Blob = "e69de29bb2d1d6434b8b29ae775ad8c2e48c5391";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private static byte[] Records(params string[] records) =>
        Encoding.UTF8.GetBytes(string.Concat(records.Select(record => $"{record}\0")));

    [Fact]
    public void GivenFilesOfEveryKind_WhenParsing_ThenEachHasItsKindAndSize()
    {
        var output = Records(
            $"100644 blob {Blob}      12\ta.txt",
            $"100755 blob {Blob}     345\tbin/run.sh",
            $"120000 blob {Blob}       5\tlink",
            $"160000 commit {Commit}       -\tvendor/lib");

        var result = CommitTreeParser.Parse(output);

        Assert.Empty(result.Warnings);
        Assert.Equal(
            [
                ("a.txt", TreeEntryKind.File, 12L),
                ("bin/run.sh", TreeEntryKind.Executable, 345L),
                ("link", TreeEntryKind.Symlink, 5L),
                ("vendor/lib", TreeEntryKind.Submodule, -1L),
            ],
            result.Items.Select(entry => (entry.Path, entry.Kind, entry.Size ?? -1L)));
    }

    [Fact]
    public void GivenOddPaths_WhenParsing_ThenThePathIsKeptWhole()
    {
        var output = Records(
            $"100644 blob {Blob}       1\twith space/ünï cödé.txt",
            $"100644 blob {Blob}       1\ttab\tin name",
            $"100644 blob {Blob}       1\tnew\nline");

        var result = CommitTreeParser.Parse(output);

        Assert.Equal(
            ["with space/ünï cödé.txt", "tab\tin name", "new\nline"],
            result.Items.Select(entry => entry.Path));
    }

    [Fact]
    public void GivenAPathThatIsNotUtf8_WhenParsing_ThenItRoundTripsThroughTheEscapedText()
    {
        var prefix = Encoding.ASCII.GetBytes($"100644 blob {Blob}       1\t");
        var output = (byte[])[.. prefix, 0x66, 0xE9, 0x2E, 0x74, 0x78, 0x74, 0x00];

        var result = CommitTreeParser.Parse(output);

        var entry = Assert.Single(result.Items);
        Assert.Equal(0x66, entry.Path[0]);
        Assert.Equal(0xDC00 + 0xE9, entry.Path[1]);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("100644 blob nothex       1\ta.txt")]
    [InlineData("100644 tree 0123456789abcdef0123456789abcdef01234567       -\td")]
    [InlineData("100644 blob 0123456789abcdef0123456789abcdef01234567    x\ta.txt")]
    [InlineData("100644 blob 0123456789abcdef0123456789abcdef01234567       1\t")]
    public void GivenAMalformedRecord_WhenParsing_ThenItIsSkippedWithAWarning(string record)
    {
        var output = Records(record, $"100644 blob {Blob}       1\tkept.txt");

        var result = CommitTreeParser.Parse(output);

        Assert.Equal(["kept.txt"], result.Items.Select(entry => entry.Path));
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void GivenEmptyOutput_WhenParsing_ThenThereAreNoEntries()
    {
        var result = CommitTreeParser.Parse([]);

        Assert.Empty(result.Items);
        Assert.Empty(result.Warnings);
    }
}
