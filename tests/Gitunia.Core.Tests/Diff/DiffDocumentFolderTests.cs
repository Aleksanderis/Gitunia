using Gitunia.Core.Diff;
using Gitunia.Git.Diff;
using Xunit;

namespace Gitunia.Core.Tests.Diff;

[Trait("Os", "Independent")]
public class DiffDocumentFolderTests
{
    [Fact]
    public void GivenThreeFiles_WhenCuttingTheFirstAndThird_ThenRowsHunksAndFileAnchorsAreRenumbered()
    {
        var document = Build("a/one.txt", "b/two.txt", "a/sub/three.txt");

        var cut = document.ForFiles(document.FileIndexesUnder("a"));

        Assert.Equal([["a/one.txt"], ["a/sub/three.txt"]], cut.FilePaths);
        Assert.Equal(0, cut.FileRows[0]);
        Assert.Equal(cut.Rows.Count / 2, cut.FileRows[1]);
        Assert.Equal([1, 1 + cut.Rows.Count / 2], cut.HunkRows);
        Assert.Equal(0, cut.Rows[cut.FileRows[0]].FileIndex);
        Assert.Equal(1, cut.Rows[cut.FileRows[1]].FileIndex);
        Assert.DoesNotContain(cut.Rows, row => row.Text.Contains("two.txt", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAFolderName_WhenFindingItsFiles_ThenOnlyPathsBelowTheFolderMatch()
    {
        var document = Build("src/a.txt", "src2/b.txt", "src");

        Assert.Equal([0], document.FileIndexesUnder("src"));
        Assert.Empty(document.FileIndexesUnder("sr"));
        Assert.Empty(document.FileIndexesUnder("src/a.txt"));
    }

    [Fact]
    public void GivenWarningsAboveTheFirstFile_WhenCuttingFileZero_ThenTheWarningsStayAndOtherwiseTheyGo()
    {
        var builder = new DiffDocumentBuilder();
        foreach (var path in new[] { "a.txt", "b.txt" })
        {
            builder.Add(DiffParser.Parse(Patch(path)).Items[0]);
        }

        var document = builder.Build(["careful"]);

        Assert.Equal("careful", document.ForFiles([0]).Rows[0].Text);
        Assert.DoesNotContain(document.ForFiles([1]).Rows, row => row.Text == "careful");
    }

    private static DiffDocument Build(params string[] paths)
    {
        var builder = new DiffDocumentBuilder();
        foreach (var path in paths)
        {
            builder.Add(DiffParser.Parse(Patch(path)).Items[0]);
        }

        return builder.Build([]);
    }

    private static string Patch(string path) =>
        $"diff --git a/{path} b/{path}\n--- a/{path}\n+++ b/{path}\n@@ -1 +1 @@\n-old\n+new\n";
}
