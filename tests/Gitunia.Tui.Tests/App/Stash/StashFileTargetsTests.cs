using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;
using Gitunia.Tui.App.Stash;
using Gitunia.Tui.Rendering.Details;
using Xunit;

namespace Gitunia.Tui.Tests.App.Stash;

[Trait("Os", "Independent")]
public class StashFileTargetsTests
{
    private static readonly StashEntry Stash = new(0, "stash@{0}", "abc123", "wip");

    [Fact]
    public void GivenTrackedAndUntrackedFiles_WhenResolving_ThenTheTrailingFilesAreUntracked()
    {
        var content = Content(BottomTab.Changes, "a.txt", "b.txt", "new.txt");

        var (trackedStash, trackedChange) = StashFileTargets.Resolve(Stash, content, 2, 1)!;
        var (_, untrackedChange) = StashFileTargets.Resolve(Stash, content, 3, 1)!;

        Assert.Equal(("b.txt", StashFileOrigin.Tracked), (trackedChange.Change.Path, trackedChange.Origin));
        Assert.Equal(("new.txt", StashFileOrigin.Untracked), (untrackedChange.Change.Path, untrackedChange.Origin));
        Assert.Equal(Stash, trackedStash);
    }

    [Fact]
    public void GivenTheCursorIsOnAHeadingRow_WhenResolving_ThenThereIsNoTarget()
    {
        var content = Content(BottomTab.Changes, "a.txt");

        Assert.Null(StashFileTargets.Resolve(Stash, content, 0, 0));
        Assert.Null(StashFileTargets.Resolve(Stash, content, -1, 0));
        Assert.Null(StashFileTargets.Resolve(Stash, content, 99, 0));
    }

    [Fact]
    public void GivenNoStashIsShownOrAnotherTab_WhenResolving_ThenThereIsNoTarget()
    {
        Assert.Null(StashFileTargets.Resolve(null, Content(BottomTab.Changes, "a.txt"), 1, 0));
        Assert.Null(StashFileTargets.Resolve(Stash, Content(BottomTab.Details, "a.txt"), 1, 0));
    }

    // Row 0 is a heading, row n + 1 shows file n.
    private static DetailContent Content(BottomTab tab, params string[] paths)
    {
        var files = paths.Select(path => new CommitFileChange(path, null, FileChangeKind.Modified, null)).ToList();
        List<DetailRow> rows = [new DetailRow([])];
        rows.AddRange(files.Select((_, index) => new DetailRow([], FileIndex: index)));

        return new DetailContent("abc", rows, 1, files, tab);
    }
}
