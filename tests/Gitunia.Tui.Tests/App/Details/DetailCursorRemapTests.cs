using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Tui.App.Details;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Tests.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App.Details;

[Trait("Os", "Independent")]
public class DetailCursorRemapTests
{
    private static readonly CommitFileChange[] Changes =
    [
        new("src/a.cs", null, FileChangeKind.Modified, null),
        new("src/b.cs", null, FileChangeKind.Modified, null),
        new("top.txt", null, FileChangeKind.Modified, null),
    ];

    [Fact]
    public void GivenTheCursorOnAFile_WhenTheListBecomesATree_ThenItMovesToTheRowOfTheSameFile()
    {
        var flat = Build(FilePathStyle.NameFirst);
        var tree = Build(FilePathStyle.Tree);

        var row = DetailCursorRemap.Row(flat, tree, 2);

        Assert.Equal("src/b.cs", tree.Files[tree.FileIndexAt(row)].Path);
        Assert.NotEqual(2, row);
    }

    [Fact]
    public void GivenTheCursorOnAFileInATree_WhenTheListBecomesFlat_ThenItMovesToTheRowOfTheSameFile()
    {
        var tree = Build(FilePathStyle.Tree);
        var flat = Build(FilePathStyle.FullPath);

        var row = DetailCursorRemap.Row(tree, flat, tree.RowOfFile(2));

        Assert.Equal("top.txt", flat.Files[flat.FileIndexAt(row)].Path);
    }

    [Fact]
    public void GivenTheCursorOnAFolder_WhenTheListBecomesFlat_ThenItMovesToTheFirstFileBelowIt()
    {
        var tree = Build(FilePathStyle.Tree);
        var flat = Build(FilePathStyle.NameFirst);

        var row = DetailCursorRemap.Row(tree, flat, 1);

        Assert.Equal("src/a.cs", flat.Files[flat.FileIndexAt(row)].Path);
    }

    [Fact]
    public void GivenTheSameStyle_WhenAskingForARow_ThenTheCursorStays()
    {
        var flat = Build(FilePathStyle.NameFirst);

        Assert.Equal(-1, DetailCursorRemap.Row(flat, Build(FilePathStyle.NameFirst), 2));
        Assert.Equal(-1, DetailCursorRemap.Row(flat, Build(FilePathStyle.Tree), null));
    }

    private static DetailContent Build(FilePathStyle style)
    {
        var commit = CommitFactory.Commit(2, "Fix", [1]);
        var entry = CommitFactory.Graph(commit, CommitFactory.Commit(1, "root", []))[0];
        var details = new CommitDetails(
            CommitFactory.Id(2),
            [CommitFactory.Id(1)],
            "Ann",
            "ann@x.org",
            DateTimeOffset.UnixEpoch,
            "Ann",
            "ann@x.org",
            DateTimeOffset.UnixEpoch,
            "m",
            Changes);
        var state = new CommitDetailsState(entry.Id, CommitDetailsStatus.Loaded, details, string.Empty);

        return DetailContentBuilder.Build(entry, state, BottomTab.Changes, null, style);
    }
}
