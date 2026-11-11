using Gitunia.Core.FileTree;
using Gitunia.Core.Workspaces.Repositories;
using Xunit;

namespace Gitunia.Core.Tests.FileTree;

[Trait("Os", "Independent")]
public class PathTreeTests
{
    [Fact]
    public void GivenNestedPaths_WhenBuilt_ThenFoldersComeBeforeFilesInOrdinalOrder()
    {
        var rows = PathTree.Rows(["z.txt", "src/b.cs", "src/deep/c.cs", "a.txt"]);

        Assert.Equal(["src", "deep", "c.cs", "b.cs", "a.txt", "z.txt"], rows.Select(row => row.Name));
        Assert.Equal([0, 1, 2, 1, 0, 0], rows.Select(row => row.Depth));
    }

    [Fact]
    public void GivenAnUntrackedDirectoryPath_WhenBuilt_ThenItIsOneLeafThatKeepsItsSlash()
    {
        var rows = PathTree.Rows(["build/", "src/x.cs"]);

        Assert.Equal(["src", "x.cs", "build/"], rows.Select(row => row.Name));
        Assert.False(rows[2].IsFolder);
    }

    [Fact]
    public void GivenAClosedFolder_WhenBuilt_ThenItsRowsAreHidden()
    {
        var rows = PathTree.Rows(["src/a.cs", "src/deep/b.cs"], new HashSet<string> { "src" });

        Assert.Equal(["src"], rows.Select(row => row.Name));
    }

    [Fact]
    public void GivenAFolderAndAPath_WhenAskingIsUnder_ThenOnlyRealDescendantsMatch()
    {
        Assert.True(PathTree.IsUnder("src", "src/a.cs"));
        Assert.True(PathTree.IsUnder("src", "src/"));
        Assert.False(PathTree.IsUnder("src", "src2/a.cs"));
        Assert.False(PathTree.IsUnder("src", "src"));
    }

    [Fact]
    public void GivenTreeRows_WhenMappingRanges_ThenFilesAreDedupedAndHiddenOnesIncluded()
    {
        var rows = WorkingRows.Tree(["src/a.cs", "src/b.cs", "top.txt"], new HashSet<string>());

        Assert.Equal([0, 1], rows.EntriesOfRows(0, 2));
        Assert.Equal(0, rows.ParentFolderRow(1));
        Assert.Equal(-1, rows.ParentFolderRow(0));
        Assert.Equal(3, rows.StepFile(1, true, 5));
        Assert.Equal(2, rows.StepFile(3, false, 1));
        Assert.Equal(1, rows.StepFile(0, true, 1));
    }

    [Fact]
    public void GivenAClosedFolder_WhenMappingRows_ThenHiddenFilesStillBelongToTheFolderRow()
    {
        var rows = WorkingRows.Tree(["src/a.cs", "src/b.cs"], new HashSet<string> { "src" });

        Assert.Equal(1, rows.Count);
        Assert.Equal(-1, rows.RowOfEntry(0));
        Assert.Equal(0, rows.RowOfClosedAncestor("src/b.cs"));
        Assert.Equal([0, 1], rows.EntriesOfRows(0, 0));
    }

    [Fact]
    public void GivenHundredThousandPaths_WhenBuilt_ThenNoSubstringIsMadePerKnownFolder()
    {
        var paths = new List<string>();
        for (var index = 0; index < 100_000; index++)
        {
            paths.Add($"src/module{index % 20}/sub{index % 300}/deep/file{index}.cs");
        }

        PathTree.Rows(paths);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var rows = PathTree.Rows(paths);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Measured 33.1 MB with a substring per component, 19.1 MB with span lookups.
        Assert.True(allocated < 25_000_000, $"allocated {allocated} bytes");
        Assert.Equal("src", rows[0].Name);
        Assert.Equal("module0", rows[1].Name);
    }
}
