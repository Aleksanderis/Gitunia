using System.Text;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Xunit;

namespace Gitunia.Git.Tests.Diff;

public class DiffParserLimitTests
{
    [Fact]
    public void GivenAFileLongerThanTheLimit_WhenParsing_ThenOnlyTheFirstLinesAreKeptAndTheFileIsMarked()
    {
        var patch = BuildPatch(lineCount: 10);
        var parser = new DiffParser(maximumLinesPerFile: 4);
        var files = new List<DiffFile>();

        parser.Feed(Encoding.UTF8.GetBytes(patch), files);
        parser.Complete(files);

        var file = Assert.Single(files);
        Assert.True(file.IsTruncated);
        Assert.Equal(4, file.Hunks.Sum(hunk => hunk.Lines.Count));
        Assert.Equal("line 4", file.Hunks[0].Lines[3].Text);
    }

    [Fact]
    public void GivenAFileWithinTheLimit_WhenParsing_ThenItIsNotMarkedAndKeepsEveryLine()
    {
        var parser = new DiffParser(maximumLinesPerFile: 10);
        var files = new List<DiffFile>();

        parser.Feed(Encoding.UTF8.GetBytes(BuildPatch(lineCount: 10)), files);
        parser.Complete(files);

        var file = Assert.Single(files);
        Assert.False(file.IsTruncated);
        Assert.Equal(10, file.Hunks[0].Lines.Count);
    }

    [Fact]
    public void GivenTwoFilesAndALimit_WhenParsing_ThenTheLimitIsPerFile()
    {
        var parser = new DiffParser(maximumLinesPerFile: 3);
        var files = new List<DiffFile>();

        parser.Feed(Encoding.UTF8.GetBytes(BuildPatch(5) + BuildPatch(2, "b.txt")), files);
        parser.Complete(files);

        Assert.Equal([true, false], files.Select(file => file.IsTruncated));
        Assert.Equal(2, files[1].Hunks[0].Lines.Count);
    }

    private static string BuildPatch(int lineCount, string path = "a.txt")
    {
        var builder = new StringBuilder();
        builder.Append($"diff --git a/{path} b/{path}\nnew file mode 100644\nindex 0000000..1111111\n--- /dev/null\n+++ b/{path}\n@@ -0,0 +1,{lineCount} @@\n");
        for (var index = 1; index <= lineCount; index++)
        {
            builder.Append($"+line {index}\n");
        }

        return builder.ToString();
    }
}
