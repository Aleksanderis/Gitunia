using Gitunia.Core.Graph;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Tests.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Details;

public class DetailContentBuilderTests
{
    private static GraphEntry Entry(params CommitDecoration[] decorations)
    {
        var commit = CommitFactory.Commit(2, "Fix the thing", [1], decorations);
        return CommitFactory.Graph(commit, CommitFactory.Commit(1, "root", []))[0] with { Decorations = decorations };
    }

    private static CommitDetails Details(string message, params CommitFileChange[] files) => new(
        CommitFactory.Id(2),
        [CommitFactory.Id(1)],
        "Ann",
        "ann@x.org",
        new DateTimeOffset(2026, 1, 2, 3, 4, 0, TimeSpan.FromHours(2)),
        "Ann",
        "ann@x.org",
        new DateTimeOffset(2026, 1, 2, 3, 4, 0, TimeSpan.FromHours(2)),
        message,
        files);

    private static string[] Lines(DetailContent content) =>
        [.. content.Rows.Select(row => string.Concat(row.Segments.Select(segment => segment.Text)))];

    [Fact]
    public void GivenNoSelection_WhenBuilding_ThenContentIsEmpty()
    {
        var content = DetailContentBuilder.Build(null, CommitDetailsState.Empty, BottomTab.Files);

        Assert.Empty(content.Rows);
        Assert.Equal(BottomTab.Files, content.Tab);
    }

    [Fact]
    public void GivenDetailsStillLoading_WhenBuilding_ThenSubjectRefsAndLoadingNoticeShow()
    {
        var entry = Entry(new CommitDecoration("refs/heads/main", "main", RefKind.LocalBranch, true));
        var state = new CommitDetailsState(entry.Id, CommitDetailsStatus.Loading, null, string.Empty);

        var content = DetailContentBuilder.Build(entry, state, BottomTab.Details);

        var lines = Lines(content);
        Assert.Contains("refs      HEAD -> main", lines);
        Assert.Contains("  Fix the thing", lines);
        Assert.Equal("Loading...", lines[^1]);
        Assert.Empty(content.Files);
        Assert.Equal(content.Rows.Count, content.FirstFileRow);
    }

    [Fact]
    public void GivenFailedLoad_WhenBuilding_ThenTheErrorIsShown()
    {
        var entry = Entry();

        var content = DetailContentBuilder.Build(entry, new CommitDetailsState(entry.Id, CommitDetailsStatus.Failed, null, "git exploded"), BottomTab.Details);

        Assert.Equal("Could not load the commit (r retries): git exploded", Lines(content)[^1]);
    }

    [Theory]
    [InlineData(BottomTab.Details)]
    [InlineData(BottomTab.Changes)]
    public void GivenDetailsStillLoading_WhenBuildingAnyListingTab_ThenItEndsInTheLoadingNotice(BottomTab tab)
    {
        var entry = Entry();

        var content = DetailContentBuilder.Build(entry, new CommitDetailsState(entry.Id, CommitDetailsStatus.Loading, null, string.Empty), tab);

        Assert.Equal("Loading...", Lines(content)[^1]);
        Assert.Equal(tab, content.Tab);
    }

    [Fact]
    public void GivenLoadedDetailsOfAnotherCommit_WhenBuilding_ThenTheyAreNotShown()
    {
        var entry = Entry();
        var other = new CommitDetailsState(CommitFactory.Id(9), CommitDetailsStatus.Loaded, Details("stale"), string.Empty);

        var lines = Lines(DetailContentBuilder.Build(entry, other, BottomTab.Details));

        Assert.DoesNotContain("  stale", lines);
        Assert.Equal("Loading...", lines[^1]);
    }

    [Fact]
    public void GivenLoadedDetails_WhenBuildingTheDetailsTab_ThenInfoAndMessageAreListedWithoutFiles()
    {
        var entry = Entry(new CommitDecoration("refs/tags/v1", "v1", RefKind.Tag, false));
        var files = new[]
        {
            new CommitFileChange("src/a.cs", null, FileChangeKind.Modified, null),
            new CommitFileChange("new.txt", null, FileChangeKind.Added, null),
            new CommitFileChange("b ünï.txt", "a.txt", FileChangeKind.Renamed, 90),
        };
        var state = new CommitDetailsState(entry.Id, CommitDetailsStatus.Loaded, Details("Fix the thing\n\nBody text", files), string.Empty);

        var content = DetailContentBuilder.Build(entry, state, BottomTab.Details);

        Assert.Equal(
            [
                "commit    0000002000000000000000000000000000000000",
                "author    Ann <ann@x.org>  2026-01-02 03:04 +02:00",
                "parents   0000001",
                "refs      tag: v1",
                string.Empty,
                "  Fix the thing",
                string.Empty,
                "  Body text",
            ],
            Lines(content));
        Assert.Equal(content.Rows.Count, content.FirstFileRow);
        Assert.Equal(files, content.Files);
    }

    [Fact]
    public void GivenLoadedDetails_WhenBuildingTheChangesTab_ThenOnlyTheFilesAreListed()
    {
        var entry = Entry();
        var files = new[]
        {
            new CommitFileChange("src/a.cs", null, FileChangeKind.Modified, null),
            new CommitFileChange("new.txt", null, FileChangeKind.Added, null),
            new CommitFileChange("b ünï.txt", "a.txt", FileChangeKind.Renamed, 90),
        };
        var state = new CommitDetailsState(entry.Id, CommitDetailsStatus.Loaded, Details("Fix the thing\n\nBody text", files), string.Empty);

        var content = DetailContentBuilder.Build(entry, state, BottomTab.Changes);

        Assert.Equal(
            [
                "3 files changed",
                "M src/a.cs",
                "A new.txt",
                "R90 a.txt -> b ünï.txt",
            ],
            Lines(content));
        Assert.Equal(1, content.FirstFileRow);
        Assert.Equal(files, content.Files);
        Assert.Equal(3, content.FileCount);
    }

    [Theory]
    [InlineData(CommitDetailsStatus.Loading)]
    [InlineData(CommitDetailsStatus.Loaded)]
    public void GivenAnyLoadState_WhenBuildingTheFilesTab_ThenItShowsThePlaceholderAndNoFileRows(CommitDetailsStatus status)
    {
        var entry = Entry();
        var loaded = status == CommitDetailsStatus.Loaded;
        var state = new CommitDetailsState(entry.Id, status, loaded ? Details("m", new CommitFileChange("a", null, FileChangeKind.Added, null)) : null, string.Empty);

        var content = DetailContentBuilder.Build(entry, state, BottomTab.Files);

        Assert.Equal(["The file tree is not implemented yet"], Lines(content));
        Assert.Equal(content.Rows.Count, content.FirstFileRow);
    }

    [Fact]
    public void GivenDifferentCommitter_WhenBuilding_ThenCommitterRowAppears()
    {
        var entry = Entry();
        var details = Details("m") with { CommitterName = "Bob", CommitterEmail = "bob@x.org" };

        var lines = Lines(DetailContentBuilder.Build(entry, new CommitDetailsState(entry.Id, CommitDetailsStatus.Loaded, details, string.Empty), BottomTab.Details));

        Assert.Contains(lines, line => line.StartsWith("committer Bob <bob@x.org>", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenRootCommitAndNoFiles_WhenBuilding_ThenSaysSo()
    {
        var entry = Entry();
        var details = Details("m") with { Parents = [] };

        var state = new CommitDetailsState(entry.Id, CommitDetailsStatus.Loaded, details, string.Empty);

        Assert.Contains("parents   none (root commit)", Lines(DetailContentBuilder.Build(entry, state, BottomTab.Details)));
        Assert.Equal(["no files changed"], Lines(DetailContentBuilder.Build(entry, state, BottomTab.Changes)));
    }

    [Fact]
    public void GivenFileRowsOfEachKind_WhenBuilding_ThenSegmentRolesMarkAddedAndDeleted()
    {
        var entry = Entry();
        var files = new[]
        {
            new CommitFileChange("a", null, FileChangeKind.Added, null),
            new CommitFileChange("d", null, FileChangeKind.Deleted, null),
            new CommitFileChange("m", null, FileChangeKind.Modified, null),
        };
        var state = new CommitDetailsState(entry.Id, CommitDetailsStatus.Loaded, Details("m", files), string.Empty);

        var content = DetailContentBuilder.Build(entry, state, BottomTab.Changes);

        Assert.Equal(
            [SegmentRole.FileAdded, SegmentRole.FileDeleted, SegmentRole.FileChanged],
            content.Rows.Skip(content.FirstFileRow).Select(row => row.Segments[0].Role));
    }
}
