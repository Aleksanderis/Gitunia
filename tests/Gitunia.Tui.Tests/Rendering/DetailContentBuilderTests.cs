using Gitunia.Core.Graph;
using Gitunia.Git;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering;

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
        content.Rows.Select(row => string.Concat(row.Segments.Select(segment => segment.Text))).ToArray();

    [Fact]
    public void GivenNoSelection_WhenBuilding_ThenContentIsEmpty()
    {
        Assert.Same(DetailContent.Empty, DetailContentBuilder.Build(null, CommitDetailsState.Empty));
    }

    [Fact]
    public void GivenDetailsStillLoading_WhenBuilding_ThenSubjectRefsAndLoadingNoticeShow()
    {
        var entry = Entry(new CommitDecoration("refs/heads/main", "main", RefKind.LocalBranch, true));
        var state = new CommitDetailsState(entry.Id, CommitDetailsStatus.Loading, null, string.Empty);

        var content = DetailContentBuilder.Build(entry, state);

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

        var content = DetailContentBuilder.Build(entry, new CommitDetailsState(entry.Id, CommitDetailsStatus.Failed, null, "git exploded"));

        Assert.Equal("Could not load the commit (r retries): git exploded", Lines(content)[^1]);
    }

    [Fact]
    public void GivenLoadedDetailsOfAnotherCommit_WhenBuilding_ThenTheyAreNotShown()
    {
        var entry = Entry();
        var other = new CommitDetailsState(CommitFactory.Id(9), CommitDetailsStatus.Loaded, Details("stale"), string.Empty);

        var lines = Lines(DetailContentBuilder.Build(entry, other));

        Assert.DoesNotContain("  stale", lines);
        Assert.Equal("Loading...", lines[^1]);
    }

    [Fact]
    public void GivenLoadedDetails_WhenBuilding_ThenInfoMessageAndFilesAreListedInOrder()
    {
        var entry = Entry(new CommitDecoration("refs/tags/v1", "v1", RefKind.Tag, false));
        var files = new[]
        {
            new CommitFileChange("src/a.cs", null, FileChangeKind.Modified, null),
            new CommitFileChange("new.txt", null, FileChangeKind.Added, null),
            new CommitFileChange("b ünï.txt", "a.txt", FileChangeKind.Renamed, 90),
        };
        var state = new CommitDetailsState(entry.Id, CommitDetailsStatus.Loaded, Details("Fix the thing\n\nBody text", files), string.Empty);

        var content = DetailContentBuilder.Build(entry, state);

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
                string.Empty,
                "3 files changed",
                "  M src/a.cs",
                "  A new.txt",
                "  R90 a.txt -> b ünï.txt",
            ],
            Lines(content));
        Assert.Equal(10, content.FirstFileRow);
        Assert.Equal(files, content.Files);
    }

    [Fact]
    public void GivenDifferentCommitter_WhenBuilding_ThenCommitterRowAppears()
    {
        var entry = Entry();
        var details = Details("m") with { CommitterName = "Bob", CommitterEmail = "bob@x.org" };

        var lines = Lines(DetailContentBuilder.Build(entry, new CommitDetailsState(entry.Id, CommitDetailsStatus.Loaded, details, string.Empty)));

        Assert.Contains(lines, line => line.StartsWith("committer Bob <bob@x.org>", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenRootCommitAndNoFiles_WhenBuilding_ThenSaysSo()
    {
        var entry = Entry();
        var details = Details("m") with { Parents = [] };

        var lines = Lines(DetailContentBuilder.Build(entry, new CommitDetailsState(entry.Id, CommitDetailsStatus.Loaded, details, string.Empty)));

        Assert.Contains("parents   none (root commit)", lines);
        Assert.Equal("no files changed", lines[^1]);
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

        var content = DetailContentBuilder.Build(entry, state);

        Assert.Equal(
            [SegmentRole.FileAdded, SegmentRole.FileDeleted, SegmentRole.FileChanged],
            content.Rows.Skip(content.FirstFileRow).Select(row => row.Segments[0].Role));
    }
}
