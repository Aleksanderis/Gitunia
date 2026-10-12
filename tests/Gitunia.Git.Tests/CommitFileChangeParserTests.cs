using System.Text;
using Xunit;

namespace Gitunia.Git.Tests;

public class CommitFileChangeParserTests
{
    private const string Hash = "0000000000000000000000000000000000000000";

    private static string Entry(string status, params string[] paths) =>
        $":100644 100644 {Hash} {Hash} {status}\0" + string.Concat(paths.Select(path => path + "\0"));

    private static ParseResult<CommitFileChange> Parse(string text) => CommitFileChangeParser.Parse(Encoding.UTF8.GetBytes(text));

    [Fact]
    public void GivenEveryStatusLetter_WhenParsing_ThenKindsMatch()
    {
        var result = Parse(Entry("A", "a") + Entry("M", "m") + Entry("D", "d") + Entry("T", "t") + Entry("U", "u"));

        Assert.Equal(
            [FileChangeKind.Added, FileChangeKind.Modified, FileChangeKind.Deleted, FileChangeKind.TypeChanged, FileChangeKind.UpdatedButUnmerged],
            result.Items.Select(file => file.Kind));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void GivenRenameAndCopy_WhenParsing_ThenBothPathsAndSimilarityAreKept()
    {
        var result = Parse(Entry("R086", "old name.txt", "new name.txt") + Entry("C100", "orig", "copy"));

        Assert.Equal(new CommitFileChange("new name.txt", "old name.txt", FileChangeKind.Renamed, 86), result.Items[0]);
        Assert.Equal(new CommitFileChange("copy", "orig", FileChangeKind.Copied, 100), result.Items[1]);
    }

    [Fact]
    public void GivenUnicodePath_WhenParsing_ThenPathIsDecoded()
    {
        var result = Parse(Entry("M", "日本/ünï.txt"));

        Assert.Equal("日本/ünï.txt", Assert.Single(result.Items).Path);
    }

    [Fact]
    public void GivenGarbageAndUnknownStatus_WhenParsing_ThenSkippedWithWarningsAndNoThrow()
    {
        var result = Parse("garbage\0" + Entry("X", "weird") + Entry("M", "ok") + ":100644 100644 a b M\0");

        Assert.Equal("ok", Assert.Single(result.Items).Path);
        Assert.Equal(3, result.Warnings.Count);
    }

    [Fact]
    public void GivenEmptyOutput_WhenParsing_ThenNothing()
    {
        var result = Parse(string.Empty);

        Assert.Empty(result.Items);
        Assert.Empty(result.Warnings);
    }
}
