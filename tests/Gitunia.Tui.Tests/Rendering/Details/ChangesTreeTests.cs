using Gitunia.App.Loading.Commits;
using Gitunia.Core.Graph;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Tests.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Details;

public class ChangesTreeTests
{
    private static readonly CommitFileChange[] Changes =
    [
        new("top.txt", null, FileChangeKind.Modified, null),
        new("src/b.cs", null, FileChangeKind.Added, null),
        new("src/a.cs", null, FileChangeKind.Modified, null),
        new("src/deep/x.cs", "old/x.cs", FileChangeKind.Renamed, 90),
        new("docs/readme.md", null, FileChangeKind.Deleted, null),
    ];

    [Fact]
    public void GivenFilesInSeveralFolders_WhenBuildingTheTree_ThenFoldersComeBeforeFilesAtEveryLevel()
    {
        var rows = ChangesTree.Rows(Changes);

        Assert.Equal(
            [
                "0 docs/", "1 readme.md",
                "0 src/", "1 deep/", "2 x.cs", "1 a.cs", "1 b.cs",
                "0 top.txt",
            ],
            [.. rows.Select(row => $"{row.Depth} {(row.IsFolder ? row.Name + "/" : row.Name)}")]);
        Assert.Equal("src/deep", rows.Single(row => row.Name == "deep").FolderPath);
        Assert.Equal(3, rows.Single(row => row.Name == "x.cs").FileIndex);
    }

    [Fact]
    public void GivenTheTreeStyle_WhenBuildingTheChangesTab_ThenFolderAndFileRowsCarryTheirPathAndIndex()
    {
        var content = Build(FilePathStyle.Tree);

        Assert.Equal(
            [
                "5 files changed",
                "docs/",
                "  D readme.md",
                "src/",
                "  deep/",
                "    R old/x.cs -> x.cs",
                "  M a.cs",
                "  A b.cs",
                "M top.txt",
            ],
            [.. content.Rows.Select(row => string.Concat(row.Segments.Select(segment => segment.Text)))]);
        Assert.Equal("src/deep", content.FolderAt(4));
        Assert.Null(content.FolderAt(5));
        Assert.Equal(-1, content.FileIndexAt(1));
        Assert.Equal(4, content.FileIndexAt(2));
        Assert.Equal(2, content.RowOfFile(4));
        Assert.Equal(2, content.FirstFileRow);
        Assert.Equal(5, content.Files.Count);
    }

    [Fact]
    public void GivenTheTreeStyle_WhenARenameStaysInItsFolder_ThenOnlyTheOldNameShows()
    {
        var entry = Entry();
        var files = new[] { new CommitFileChange("src/new.cs", "src/old.cs", FileChangeKind.Renamed, 100) };

        var state = State(entry, files);

        var content = DetailContentBuilder.Build(entry, state, BottomTab.Changes, null, FilePathStyle.Tree);

        Assert.Equal(
            "  R old.cs -> new.cs",
            string.Concat(content.Rows[2].Segments.Select(segment => segment.Text)));
    }

    [Theory]
    [InlineData(FilePathStyle.NameFirst)]
    [InlineData(FilePathStyle.FullPath)]
    public void GivenAFlatStyle_WhenBuildingTheChangesTab_ThenEveryFileRowKnowsItsIndexAndNoRowIsAFolder(
        FilePathStyle style)
    {
        var content = Build(style);

        Assert.Equal([-1, 0, 1, 2, 3, 4], [.. content.Rows.Select(row => row.FileIndex)]);
        Assert.All(content.Rows, row => Assert.Null(row.Folder));
        Assert.Equal(1, content.FirstFileRow);
    }

    [Fact]
    public void GivenTheTreeStyle_WhenTheCommitHasNoFiles_ThenOnlyTheHeadingShows()
    {
        var entry = Entry();

        var content = DetailContentBuilder.Build(entry, State(entry, []), BottomTab.Changes, null, FilePathStyle.Tree);

        Assert.Single(content.Rows);
        Assert.Equal(1, content.FirstFileRow);
    }

    private static DetailContent Build(FilePathStyle style)
    {
        var entry = Entry();

        return DetailContentBuilder.Build(entry, State(entry, Changes), BottomTab.Changes, null, style);
    }

    private static GraphEntry Entry()
    {
        var commit = CommitFactory.Commit(2, "Fix the thing", [1]);

        return CommitFactory.Graph(commit, CommitFactory.Commit(1, "root", []))[0];
    }

    private static CommitDetailsState State(GraphEntry entry, IReadOnlyList<CommitFileChange> files) => new(
        entry.Id,
        CommitDetailsStatus.Loaded,
        new CommitDetails(
            CommitFactory.Id(2),
            [CommitFactory.Id(1)],
            "Ann",
            "ann@x.org",
            DateTimeOffset.UnixEpoch,
            "Ann",
            "ann@x.org",
            DateTimeOffset.UnixEpoch,
            "m",
            files),
        string.Empty);
}
