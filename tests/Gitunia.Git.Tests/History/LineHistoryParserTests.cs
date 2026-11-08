using Gitunia.Git.Commits.Models;
using Gitunia.Git.History;
using Xunit;

namespace Gitunia.Git.Tests.History;

public class LineHistoryParserTests
{
    private const string Header = "\u001e1111111111111111111111111111111111111111\u001fAnn\u001f2026-11-02\u001fadd lines\n\n";

    [Fact]
    public void GivenACreatedFilePatch_WhenParsing_ThenTheEntryKeepsThePatchAndTheNewPath()
    {
        const string patch = "diff --git a/f.txt b/f.txt\nnew file mode 100644\n--- /dev/null\n+++ b/f.txt\n@@ -0,0 +1,1 @@\n+a\n";

        var result = LineHistoryParser.Parse(Header + patch, "f.txt");

        var entry = Assert.Single(result.Items);
        Assert.Equal(("f.txt", null, FileChangeKind.Modified), (entry.Path, entry.OldPath, entry.Kind));
        Assert.Equal("add lines", entry.Subject);
        Assert.Contains("+a", entry.Patch, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenARenamePatch_WhenParsing_ThenTheEntryNamesBothPaths()
    {
        const string patch = "diff --git a/old.txt b/new.txt\n--- a/old.txt\n+++ b/new.txt\n@@ -1,1 +1,1 @@\n-a\n+b\n";

        var result = LineHistoryParser.Parse(Header + patch, "new.txt");

        var entry = Assert.Single(result.Items);
        Assert.Equal(("new.txt", "old.txt", FileChangeKind.Renamed), (entry.Path, entry.OldPath, entry.Kind));
    }

    [Fact]
    public void GivenARecordWithoutPatch_WhenParsing_ThenTheFollowedPathIsUsed()
    {
        var result = LineHistoryParser.Parse("\u001e2222222222222222222222222222222222222222\u001fAnn\u001f2026-11-02\u001fempty", "f.txt");

        var entry = Assert.Single(result.Items);
        Assert.Equal("f.txt", entry.Path);
        Assert.Equal(string.Empty, entry.Patch);
    }

    [Fact]
    public void GivenAMalformedRecord_WhenParsing_ThenItIsSkippedWithAWarning()
    {
        var result = LineHistoryParser.Parse("\u001enot a header\n\n", "f.txt");

        Assert.Empty(result.Items);
        Assert.Single(result.Warnings);
    }
}
