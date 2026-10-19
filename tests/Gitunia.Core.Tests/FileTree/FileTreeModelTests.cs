using Gitunia.Core.FileTree;
using Gitunia.Git;
using Gitunia.Git.Trees.Models;
using Xunit;

namespace Gitunia.Core.Tests.FileTree;

public class FileTreeModelTests
{
    private static readonly IReadOnlySet<string> NothingOpen = new HashSet<string>();

    private static TreeEntry File(string path, long size = 1, TreeEntryKind kind = TreeEntryKind.File)
    {
        Assert.True(ObjectId.TryParse("0123456789abcdef0123456789abcdef01234567", out var id));

        return new TreeEntry(path, kind, id, kind == TreeEntryKind.Submodule ? null : size);
    }

    private static string[] Labels(IEnumerable<FileTreeRow> rows) =>
        [.. rows.Select(row => $"{new string(' ', row.Depth)}{row.Label}{SlashOf(row)}")];

    private static string SlashOf(FileTreeRow row) => row.Kind == FileTreeRowKind.Directory ? "/" : string.Empty;

    [Fact]
    public void GivenFilesAndDirectories_WhenListingWithNothingOpen_ThenDirectoriesComeFirstAndStayClosed()
    {
        var model = FileTreeModel.Build([File("z.txt"), File("src/a.cs"), File("README"), File("docs/x.md")]);

        var rows = model.Rows(NothingOpen, string.Empty);

        Assert.Equal(["docs/", "src/", "README", "z.txt"], Labels(rows));
        Assert.All(rows.Where(row => row.Kind == FileTreeRowKind.Directory), row => Assert.False(row.IsExpanded));
    }

    [Fact]
    public void GivenAnOpenDirectory_WhenListing_ThenItsChildrenFollowIndented()
    {
        var model = FileTreeModel.Build([File("src/b.cs"), File("src/a.cs"), File("src/lib/c.cs"), File("top.txt")]);

        var rows = model.Rows(new HashSet<string> { "src" }, string.Empty);

        Assert.Equal(["src/", " lib/", " a.cs", " b.cs", "top.txt"], Labels(rows));
    }

    [Fact]
    public void GivenAChainOfSingleChildDirectories_WhenListing_ThenItFoldsIntoOneRowKeyedByTheDeepestPath()
    {
        var model = FileTreeModel.Build([File("a/b/c/one.txt"), File("a/b/c/two.txt"), File("other.txt")]);

        var closed = model.Rows(NothingOpen, string.Empty);
        var open = model.Rows(new HashSet<string> { "a/b/c" }, string.Empty);

        Assert.Equal(["a/b/c/", "other.txt"], Labels(closed));
        Assert.Equal("a/b/c", closed[0].Path);
        Assert.Equal(["a/b/c/", " one.txt", " two.txt", "other.txt"], Labels(open));
    }

    [Fact]
    public void GivenADirectoryWithFilesAndASubdirectory_WhenListing_ThenItDoesNotFold()
    {
        var model = FileTreeModel.Build([File("a/file.txt"), File("a/b/deep.txt")]);

        var rows = model.Rows(new HashSet<string> { "a" }, string.Empty);

        Assert.Equal(["a/", " b/", " file.txt"], Labels(rows));
    }

    [Fact]
    public void GivenDirectories_WhenListing_ThenTheyCountAllFilesBelowThemWhileClosed()
    {
        var model = FileTreeModel.Build([File("a/b/1"), File("a/b/2"), File("a/3"), File("4")]);

        var rows = model.Rows(NothingOpen, string.Empty);

        Assert.Equal(3, rows[0].FileCount);
        Assert.Equal(4, model.FileCount);
    }

    [Fact]
    public void GivenSpecialEntries_WhenListing_ThenTheirKindsAndSizesAreKept()
    {
        var model = FileTreeModel.Build(
        [
            File("run.sh", 20, TreeEntryKind.Executable),
            File("link", 3, TreeEntryKind.Symlink),
            File("vendor", 0, TreeEntryKind.Submodule),
            File("plain", 7),
        ]);

        var rows = model.Rows(NothingOpen, string.Empty);

        Assert.Equal(
            [
                ("link", FileTreeRowKind.Symlink, 3L),
                ("plain", FileTreeRowKind.File, 7L),
                ("run.sh", FileTreeRowKind.Executable, 20L),
                ("vendor", FileTreeRowKind.Submodule, -1L),
            ],
            rows.Select(row => (row.Label, row.Kind, row.Size ?? -1L)));
    }

    [Fact]
    public void GivenAFilter_WhenListing_ThenOnlyMatchingFilesAndTheirDirectoriesShowOpen()
    {
        var model = FileTreeModel.Build(
            [
                File("src/core/Parser.cs"),
                File("src/core/Other.cs"),
                File("src/ui/View.cs"),
                File("docs/parser.md"),
                File("README"),
            ]);

        var rows = model.Rows(NothingOpen, "PARSER");

        Assert.Equal(["docs/", " parser.md", "src/core/", " Parser.cs"], Labels(rows));
        Assert.All(rows.Where(row => row.Kind == FileTreeRowKind.Directory), row => Assert.True(row.IsExpanded));
    }

    [Fact]
    public void GivenAFilterThatNamesADirectory_WhenListing_ThenEveryFileBelowItMatches()
    {
        var model = FileTreeModel.Build([File("src/a.cs"), File("src/b.cs"), File("docs/c.md")]);

        var rows = model.Rows(NothingOpen, "src/");

        Assert.Equal(["src/", " a.cs", " b.cs"], Labels(rows));
    }

    [Fact]
    public void GivenAFilterWithNoMatch_WhenListing_ThenThereAreNoRows()
    {
        var model = FileTreeModel.Build([File("a.txt")]);

        Assert.Empty(model.Rows(NothingOpen, "zzz"));
    }

    [Fact]
    public void GivenAFilterAndASiblingThatDoesNotMatch_WhenListing_ThenTheSingleMatchingChainStillFolds()
    {
        var model = FileTreeModel.Build([File("a/b/hit.txt"), File("a/miss.txt")]);

        var rows = model.Rows(NothingOpen, "hit");

        Assert.Equal(["a/b/", " hit.txt"], Labels(rows));
    }

    [Fact]
    public void GivenNoEntries_WhenListing_ThenThereAreNoRows()
    {
        var model = FileTreeModel.Build([]);

        Assert.Empty(model.Rows(NothingOpen, string.Empty));
        Assert.Equal(0, model.FileCount);
    }

    [Fact]
    public void GivenOddNames_WhenListing_ThenTheyAreKeptAndOrderedOrdinally()
    {
        var model = FileTreeModel.Build([File("b b/ünï.txt"), File("B.txt"), File("a.txt"), File("-x")]);

        var rows = model.Rows(new HashSet<string> { "b b" }, string.Empty);

        Assert.Equal(["b b/", " ünï.txt", "-x", "B.txt", "a.txt"], Labels(rows));
    }

    [Fact]
    public void GivenARow_WhenFindingItsParent_ThenItIsTheNearestEarlierRowOneLevelUp()
    {
        var model = FileTreeModel.Build([File("a/x/1"), File("a/x/2"), File("a/y"), File("top")]);
        var rows = model.Rows(new HashSet<string> { "a", "a/x" }, string.Empty);

        Assert.Equal(["a/", " x/", "  1", "  2", " y", "top"], Labels(rows));
        Assert.Equal(1, FileTreeModel.ParentRow(rows, 2));
        Assert.Equal(0, FileTreeModel.ParentRow(rows, 4));
        Assert.Equal(-1, FileTreeModel.ParentRow(rows, 0));
        Assert.Equal(-1, FileTreeModel.ParentRow(rows, 5));
        Assert.Equal(-1, FileTreeModel.ParentRow(rows, 99));
    }
}
