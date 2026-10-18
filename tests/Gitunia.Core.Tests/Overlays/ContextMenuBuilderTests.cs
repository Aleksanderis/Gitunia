using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Sidebar;
using Xunit;

namespace Gitunia.Core.Tests.Overlays;

public class ContextMenuBuilderTests
{
    private static readonly KeyBindingTable Table = DefaultKeymap.Create();

    private static SidebarRow Row(SidebarRowKind kind, SidebarSection section) =>
        new(kind, section, "x", string.Empty, null, false, false, 0, null, null);

    [Fact]
    public void GivenTheGraphRowActions_WhenBuilding_ThenEveryActionIsKeptInOrderWithItsKey()
    {
        var entries = ContextMenuBuilder.Build(Table, DefaultKeymap.GraphView, RowActions.OfGraphRow);

        Assert.Equal(RowActions.OfGraphRow, entries.Select(entry => entry.Action));
        Assert.All(entries, entry => Assert.NotEqual(string.Empty, entry.Keys));
        Assert.Equal("<space>gr", entries.Single(entry => entry.Action == ActionIds.RebaseOnto).Keys);
    }

    [Fact]
    public void GivenAnActionWithoutAKeyInTheView_WhenBuilding_ThenItIsLeftOut()
    {
        ActionId[] actions = [ActionIds.BranchSwitch, ActionIds.StashApply];

        var entries = ContextMenuBuilder.Build(Table, DefaultKeymap.GraphView, actions);

        Assert.Equal([ActionIds.BranchSwitch], entries.Select(entry => entry.Action));
    }

    [Theory]
    [InlineData(SidebarRowKind.LocalBranch, SidebarSection.Branches)]
    [InlineData(SidebarRowKind.RemoteBranch, SidebarSection.Remotes)]
    [InlineData(SidebarRowKind.Tag, SidebarSection.Tags)]
    [InlineData(SidebarRowKind.Worktree, SidebarSection.Worktrees)]
    [InlineData(SidebarRowKind.Stash, SidebarSection.Stashes)]
    [InlineData(SidebarRowKind.Section, SidebarSection.Branches)]
    [InlineData(SidebarRowKind.Section, SidebarSection.Tags)]
    [InlineData(SidebarRowKind.Section, SidebarSection.Worktrees)]
    [InlineData(SidebarRowKind.Section, SidebarSection.Stashes)]
    public void GivenASidebarRow_WhenBuilding_ThenEveryActionOfItsMenuHasAKeyInTheSidebar(
        SidebarRowKind kind,
        SidebarSection section)
    {
        var actions = RowActions.OfSidebarRow(Row(kind, section));

        var entries = ContextMenuBuilder.Build(Table, DefaultKeymap.SidebarView, actions);

        Assert.NotEmpty(actions);
        Assert.Equal(actions, entries.Select(entry => entry.Action));
    }

    [Fact]
    public void GivenTheRemotesHeader_WhenAskingForActions_ThenThereAreNone()
    {
        Assert.Empty(RowActions.OfSidebarRow(Row(SidebarRowKind.Section, SidebarSection.Remotes)));
    }

    [Fact]
    public void GivenTheGraphRowActions_WhenBuilding_ThenEntriesCarryTheirGroupInContiguousRuns()
    {
        var entries = ContextMenuBuilder.Build(Table, DefaultKeymap.GraphView, RowActions.OfGraphRow);

        Assert.Equal(["Branch", "Tag", "Merge and rebase", "Copy"], entries.Select(entry => entry.Group).Distinct());
        Assert.All(entries, entry => Assert.NotNull(entry.Group));
    }

    [Fact]
    public void GivenAShortMenu_WhenBuilding_ThenThereAreNoGroups()
    {
        var actions = RowActions.OfSidebarRow(Row(SidebarRowKind.Stash, SidebarSection.Stashes));

        var entries = ContextMenuBuilder.Build(Table, DefaultKeymap.SidebarView, actions);

        Assert.True(entries.Count <= ContextMenuBuilder.UngroupedMaximum);
        Assert.All(entries, entry => Assert.Null(entry.Group));
    }

    [Fact]
    public void GivenALongMenuOfOneGroup_WhenBuilding_ThenThereAreNoGroups()
    {
        var actions = RowActions.OfSidebarRow(Row(SidebarRowKind.Worktree, SidebarSection.Worktrees));

        var entries = ContextMenuBuilder.Build(Table, DefaultKeymap.SidebarView, actions);

        Assert.True(entries.Count > ContextMenuBuilder.UngroupedMaximum);
        Assert.All(entries, entry => Assert.Null(entry.Group));
    }
}
