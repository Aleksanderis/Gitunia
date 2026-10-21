using Gitunia.Core.FileTree;
using Gitunia.Core.Graph;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Trees.Models;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Loading.Trees;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Sidebar;
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

    private static TreeEntry File(string path, long size, TreeEntryKind kind = TreeEntryKind.File) =>
        new(path, kind, CommitFactory.Id(7), kind == TreeEntryKind.Submodule ? null : size);

    private static FileTreeView TreeView(GraphEntry entry, params TreeEntry[] entries) =>
        TreeView(entry, new HashSet<string>(), string.Empty, entries);

    private static FileTreeView TreeView(GraphEntry entry, IReadOnlySet<string> open, params TreeEntry[] entries) =>
        TreeView(entry, open, string.Empty, entries);

    private static FileTreeView TreeView(
        GraphEntry entry,
        IReadOnlySet<string> open,
        string filter,
        params TreeEntry[] entries)
    {
        var model = FileTreeModel.Build(entries);

        return new FileTreeView(
            new CommitTreeState(entry.Id, CommitDetailsStatus.Loaded, model, string.Empty),
            model.Rows(open, filter),
            filter,
            SidebarGlyphs.Unicode);
    }

    private static string[] Lines(DetailContent content) =>
        [.. content.Rows.Select(row => string.Concat(row.Segments.Select(segment => segment.Text)))];

    [Fact]
    public void GivenMergeCommit_WhenBuildingChanges_ThenHeadingNamesTheFirstParent()
    {
        var entry = Entry();
        var merge = Details("merge") with { Parents = [CommitFactory.Id(1), CommitFactory.Id(3)] };
        var state = new CommitDetailsState(entry.Id, CommitDetailsStatus.Loaded, merge, string.Empty);

        var lines = Lines(DetailContentBuilder.Build(entry, state, BottomTab.Changes));

        Assert.Equal($"no files changed (merge: against first parent {CommitFactory.Id(1).ToString()[..7]})", lines[0]);
    }

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
    public void GivenAPathStyle_WhenBuildingTheChangesTab_ThenTheContentCarriesItAndEachFileRowItsPath()
    {
        var entry = Entry();
        var files = new[]
        {
            new CommitFileChange("src/a.cs", null, FileChangeKind.Modified, null),
            new CommitFileChange("lib/b.cs", "src/b.cs", FileChangeKind.Renamed, 90),
        };
        var state = new CommitDetailsState(entry.Id, CommitDetailsStatus.Loaded, Details("Fix", files), string.Empty);

        var content = DetailContentBuilder.Build(entry, state, BottomTab.Changes, null, FilePathStyle.NameFirst);

        Assert.Equal(FilePathStyle.NameFirst, content.PathStyle);
        Assert.Equal(new FileRowPath("src/a.cs", null), content.Rows[1].File);
        Assert.Equal(new FileRowPath("lib/b.cs", "src/b.cs"), content.Rows[2].File);
        Assert.Null(content.Rows[0].File);
        var empty = DetailContentBuilder.Build(null, state, BottomTab.Changes, null, FilePathStyle.NameFirst);
        Assert.Equal(FilePathStyle.NameFirst, empty.PathStyle);
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
    public void GivenNoTreeYet_WhenBuildingTheFilesTab_ThenItShowsLoadingAndNoFileRows(CommitDetailsStatus status)
    {
        var entry = Entry();
        var loaded = status == CommitDetailsStatus.Loaded;
        var state = new CommitDetailsState(entry.Id, status, loaded ? Details("m", new CommitFileChange("a", null, FileChangeKind.Added, null)) : null, string.Empty);

        var content = DetailContentBuilder.Build(entry, state, BottomTab.Files);

        Assert.Equal(["Loading..."], Lines(content));
        Assert.Equal(content.Rows.Count, content.FirstFileRow);
        Assert.Empty(content.Tree);
        Assert.Null(content.FileCount);
    }

    [Fact]
    public void GivenALoadedTree_WhenBuildingTheFilesTab_ThenRowsShowChevronsSizesAndKinds()
    {
        var entry = Entry();
        var view = TreeView(
            entry,
            File("docs/guide.md", 2048),
            File("src/lib/a.cs", 10),
            File("src/lib/b.cs", 1_500_000),
            File("run.sh", 99, TreeEntryKind.Executable),
            File("link", 3, TreeEntryKind.Symlink),
            File("vendor/mod", 0, TreeEntryKind.Submodule),
            File("README", 0));

        var content = DetailContentBuilder.Build(entry, CommitDetailsState.Empty, BottomTab.Files, view);

        Assert.Equal(
            [
                "▸ docs  1 file",
                "▸ src/lib  2 files",
                "▸ vendor  1 file",
                "  README  0 B",
                "  link  (symlink, 3 B)",
                "  run.sh  (executable, 99 B)",
            ],
            Lines(content));
        Assert.Equal(6, content.Tree.Count);
        Assert.Equal(7, content.FileCount);
        Assert.Equal(content.Rows.Count, content.FirstFileRow);
    }

    [Fact]
    public void GivenAnOpenDirectory_WhenBuildingTheFilesTab_ThenChildrenAreIndentedUnderAnOpenChevron()
    {
        var entry = Entry();
        var view = TreeView(
            entry,
            new HashSet<string> { "src" },
            File("src/a.cs", 2048),
            File("src/sub/b.cs", 1),
            File("src/sub/c.cs", 1));

        var lines = Lines(DetailContentBuilder.Build(entry, CommitDetailsState.Empty, BottomTab.Files, view));

        Assert.Equal(["▾ src", "  ▸ sub  2 files", "    a.cs  2.0 KB"], lines);
    }

    [Fact]
    public void GivenAnAsciiGlyphSet_WhenBuildingTheFilesTab_ThenDirectoriesUseAsciiChevrons()
    {
        var entry = Entry();
        var view = TreeView(entry, File("src/a.cs", 1)) with { Glyphs = SidebarGlyphs.Ascii };

        var lines = Lines(DetailContentBuilder.Build(entry, CommitDetailsState.Empty, BottomTab.Files, view));

        Assert.Equal("> src  1 file", lines[0]);
    }

    [Fact]
    public void GivenATreeOfAnotherCommit_WhenBuildingTheFilesTab_ThenItShowsLoadingNotThatTree()
    {
        var entry = Entry();
        var other = TreeView(CommitFactory.Graph(CommitFactory.Commit(9, "other", []))[0], File("x.txt", 1));

        var content = DetailContentBuilder.Build(entry, CommitDetailsState.Empty, BottomTab.Files, other);

        Assert.Equal(["Loading..."], Lines(content));
        Assert.Empty(content.Tree);
    }

    [Fact]
    public void GivenAFailedTree_WhenBuildingTheFilesTab_ThenItSaysSoAndHowToRetry()
    {
        var entry = Entry();
        var view = new FileTreeView(
            new CommitTreeState(entry.Id, CommitDetailsStatus.Failed, null, "boom"),
            [],
            string.Empty,
            SidebarGlyphs.Unicode);

        var content = DetailContentBuilder.Build(entry, CommitDetailsState.Empty, BottomTab.Files, view);

        Assert.Equal(["Could not load the tree (<space>r retries): boom"], Lines(content));
    }

    [Fact]
    public void GivenAnEmptyTree_WhenBuildingTheFilesTab_ThenItSaysTheCommitHasNoFiles()
    {
        var entry = Entry();

        var content = DetailContentBuilder.Build(entry, CommitDetailsState.Empty, BottomTab.Files, TreeView(entry));

        Assert.Equal(["The commit has no files"], Lines(content));
        Assert.Equal(0, content.FileCount);
    }

    [Fact]
    public void GivenAFilterWithNoMatch_WhenBuildingTheFilesTab_ThenItSaysNoFileMatches()
    {
        var entry = Entry();
        var view = TreeView(entry, new HashSet<string>(), "zzz", File("a.txt", 1));

        var content = DetailContentBuilder.Build(entry, CommitDetailsState.Empty, BottomTab.Files, view);

        Assert.Equal(["No file matches the filter"], Lines(content));
        Assert.Equal(1, content.FileCount);
    }

    [Fact]
    public void GivenATree_WhenBuildingAnotherTab_ThenTheTreeRowsAreNotListed()
    {
        var entry = Entry();
        var view = TreeView(entry, File("a.txt", 1));

        var content = DetailContentBuilder.Build(entry, CommitDetailsState.Empty, BottomTab.Details, view);

        Assert.Empty(content.Tree);
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
