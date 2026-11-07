using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Worktrees;
using Xunit;
using static Gitunia.Core.Tests.Worktrees.WorktreeTestData;

namespace Gitunia.Core.Tests.Worktrees;

// Switching to a worktree and opening one as a tab, and the overview that the poller reads.
public class WorktreeSwitchReducerTests
{
    private static readonly string Linked = Sibling("wt");

    [Fact]
    public void GivenAWorktreeRow_WhenSwitching_ThenItsPathIsLocatedToSwitch()
    {
        var context = Context(MainAnd(Info(Linked, "wt")));

        var update = Reduce(new WorktreeSwitchRequested(Tab, WorktreeRow(Linked)), context);

        var locate = Assert.IsType<LocateWorktreeEffect>(Assert.Single(update.Effects));
        Assert.Equal(Linked, locate.Path);
        Assert.True(locate.IsSwitch);
    }

    [Fact]
    public void GivenAWorktreeRow_WhenOpening_ThenItsPathIsLocatedToOpenEvenWhileBusy()
    {
        var context = Context(MainAnd(Info(Linked, "wt")), busy: true);

        var update = Reduce(new WorktreeOpenRequested(Tab, WorktreeRow(Linked)), context);

        Assert.False(Assert.IsType<LocateWorktreeEffect>(Assert.Single(update.Effects)).IsSwitch);
    }

    [Fact]
    public void GivenALocalBranchHeldByAWorktree_WhenOpening_ThenThatWorktreeIsLocated()
    {
        var refs = new[] { Local("main", true), Local("topic", checkedOutIn: Linked) };
        var context = Context(MainAnd(Info(Linked, "topic")), [.. refs]);

        var update = Reduce(new WorktreeOpenRequested(Tab, Row(SidebarRowKind.LocalBranch, "topic")), context);

        Assert.Equal(Linked, Assert.IsType<LocateWorktreeEffect>(Assert.Single(update.Effects)).Path);
    }

    [Fact]
    public void GivenARunningOperation_WhenSwitching_ThenItIsRefusedAsBusy()
    {
        var update = Reduce(
            new WorktreeSwitchRequested(Tab, WorktreeRow(Linked)),
            Context(MainAnd(Info(Linked, "wt")), busy: true));

        Assert.Equal(WorktreeTexts.BusyNotice, Warning(update.Effects));
    }

    [Fact]
    public void GivenNoWorktreeRow_WhenSwitching_ThenAskedToSelectOne()
    {
        var update = Reduce(
            new WorktreeSwitchRequested(Tab, new WorktreeTarget(null, null, null, null, null)),
            Context(MainAnd()));

        Assert.Equal(WorktreeTexts.NoWorktreeNotice, Warning(update.Effects));
    }

    [Fact]
    public void GivenThePrunableBareAndOwnWorktrees_WhenSwitching_ThenEachIsRefusedWithoutLoad()
    {
        var gone = Sibling("gone");
        var bare = Sibling("bare");
        var context = Context(MainAnd(Info(gone, "gone", prunable: true), Info(bare, bare: true)));

        Assert.Equal(
            $"{gone} is gone from disk: {{key:worktree.prune|prune it}}",
            Warning(Reduce(new WorktreeSwitchRequested(Tab, WorktreeRow(gone)), context).Effects));
        Assert.Equal(
            "The bare repository has no working tree to switch to",
            Warning(Reduce(new WorktreeSwitchRequested(Tab, WorktreeRow(bare)), context).Effects));
        Assert.Equal(
            "This tab already shows that worktree",
            Warning(Reduce(new WorktreeSwitchRequested(Tab, WorktreeRow(Root)), context).Effects));
    }

    [Fact]
    public void GivenAnotherTabShowingTheWorktree_WhenSwitching_ThenRefusedPointingToOpen()
    {
        var other = TabOf(new TabId(2), Linked);
        var context = Context(MainAnd(Info(Linked, "wt")), others: [other]);

        var update = Reduce(new WorktreeSwitchRequested(Tab, WorktreeRow(Linked)), context);

        Assert.Equal($"{Linked} is already open in tab {other.Title}: {{key:worktree.open|go there}}", Warning(update.Effects));
    }

    [Fact]
    public void GivenALocalBranchNoWorktreeHolds_WhenSwitching_ThenNoWorktreeNoticeIsShown()
    {
        var context = Context(MainAnd(), [Local("topic")]);

        var update = Reduce(new WorktreeSwitchRequested(Tab, Row(SidebarRowKind.LocalBranch, "topic")), context);

        Assert.Equal(WorktreeTexts.NoWorktreeNotice, Warning(update.Effects));
    }

    [Fact]
    public void GivenALocatedWorktree_WhenOpening_ThenATabOpensAndTheStatusSaysSo()
    {
        var located = new WorktreeLocated(Tab, Location(Linked), false);

        var update = Reduce(located, Context());

        Assert.Equal(new OpenRepositoryCommand(located.Location), update.Workspace);
        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal($"Opened worktree {Linked}", post.Text);
        Assert.Equal(StatusSeverity.Info, post.Severity);
    }

    [Fact]
    public void GivenALocatedWorktree_WhenSwitching_ThenTheTabIsPointedAtItInPlace()
    {
        var located = new WorktreeLocated(Tab, Location(Linked), true);

        var update = Reduce(located, Context());

        Assert.Equal(new SwitchWorktreeCommand(Tab, located.Location), update.Workspace);
        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal($"Switched to worktree {Linked}", post.Text);
    }

    [Fact]
    public void GivenAnOperationStartedWhileLocating_WhenSwitching_ThenTheTabStaysWhereItIs()
    {
        var update = Reduce(new WorktreeLocated(Tab, Location(Linked), true), Context(busy: true));

        Assert.Null(update.Workspace);
        Assert.Equal($"{WorktreeTexts.BusyNotice}; the tab stays on {Root}", Warning(update.Effects));
    }

    [Fact]
    public void GivenATabOpenedOnItMeanwhile_WhenSwitching_ThenTheTabStaysWhereItIs()
    {
        var other = TabOf(new TabId(2), Linked);

        var update = Reduce(new WorktreeLocated(Tab, Location(Linked), true), Context(others: [other]));

        Assert.Null(update.Workspace);
        Assert.Equal(
            $"{Linked} is already open in tab {other.Title}: the tab stays where it is",
            Warning(update.Effects));
    }

    [Fact]
    public void GivenANewOverview_WhenRead_ThenTheTabTakesIt()
    {
        var statuses = ImmutableDictionary<string, WorktreeStatus>.Empty.Add(Linked, new WorktreeStatus(1, 2, 0, 0, 0));

        var update = Reduce(new WorktreeStatusesRead(Tab, Location(Root), statuses), Context());

        var command = Assert.IsType<TabCommand>(update.Workspace);
        Assert.Equal(Tab, command.TabId);
        Assert.Equal(statuses, Assert.IsType<WorktreeStatusesLoadedCommand>(command.Command).Statuses);
    }

    [Fact]
    public void GivenTheSameOverviewOrATabThatMoved_WhenRead_ThenNothingChanges()
    {
        var statuses = ImmutableDictionary<string, WorktreeStatus>.Empty.Add(Linked, new WorktreeStatus(1, 2, 0, 0, 0));
        var tab = TabOf(Tab, Root) with { WorktreeStatuses = statuses };
        var context = new WorktreeContext(tab, [tab], false);

        var same = Reduce(new WorktreeStatusesRead(Tab, Location(Root), statuses), context);
        var moved = Reduce(new WorktreeStatusesRead(Tab, Location(Linked), statuses.Clear()), context);

        Assert.Null(same.Workspace);
        Assert.Null(moved.Workspace);
    }
}
