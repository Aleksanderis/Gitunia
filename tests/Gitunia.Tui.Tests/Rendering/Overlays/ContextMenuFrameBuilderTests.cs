using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Sidebar;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

public class ContextMenuFrameBuilderTests
{
    private const int GraphMenuGroups = 4;

    private static readonly KeyBindingTable Table = DefaultKeymap.Create();

    private static PaletteState Menu(int selected = 0) => new(
        ContextMenuBuilder.Build(Table, DefaultKeymap.GraphView, RowActions.OfGraphRow),
        SelectedIndex: selected);

    private static OverlayFrame Frame(PaletteState menu, MenuAnchor anchor, int width = 80, int height = 24) =>
        ContextMenuFrameBuilder.Build(menu, anchor, width, height, GraphGlyphSet.Unicode)!;

    [Fact]
    public void GivenAnAnchor_WhenBuilding_ThenTheMenuHangsUnderTheRowWithTitleAndKeysAtTheRight()
    {
        var frame = Frame(Menu(), new MenuAnchor(4, 1));

        Assert.Equal(6, frame.Column);
        Assert.Equal(2, frame.Row);
        Assert.Equal(" Actions".PadRight(frame.Width), frame.Lines[0].Text);
        Assert.Contains("Branch", frame.Lines[1].Text, StringComparison.Ordinal);
        Assert.Contains("Switch", frame.Lines[4].Text, StringComparison.Ordinal);
        Assert.EndsWith("<space>bs │", frame.Lines[4].Text, StringComparison.Ordinal);
        Assert.Equal(RowActions.OfGraphRow.Count + GraphMenuGroups + 2, frame.Height);
        Assert.All(frame.Lines, line => Assert.Equal(frame.Width, line.Text.Length));
    }

    [Fact]
    public void GivenARowNearTheBottom_WhenBuilding_ThenTheMenuHangsAboveIt()
    {
        var frame = Frame(Menu(), new MenuAnchor(0, 22), height: 24);

        Assert.Equal(22 - frame.Height, frame.Row);
    }

    [Fact]
    public void GivenARowNearTheRightEdge_WhenBuilding_ThenTheMenuStaysOnScreen()
    {
        var frame = Frame(Menu(), new MenuAnchor(78, 3), width: 80);

        Assert.Equal(80 - frame.Width, frame.Column);
    }

    [Fact]
    public void GivenAShortScreen_WhenBuilding_ThenTheMenuScrollsAndTheHighlightStaysVisible()
    {
        var menu = Menu(selected: RowActions.OfGraphRow.Count - 1);

        var frame = Frame(menu, new MenuAnchor(0, 0), height: 8);

        Assert.True(frame.Height <= 8);
        Assert.Contains("Copy", string.Join('\n', frame.Lines.Select(line => line.Text)), StringComparison.Ordinal);
        Assert.Equal(RowActions.OfGraphRow.Count - 1, ContextMenuFrameBuilder.EntryIndexAt(menu, frame.Height - 2, 8));
    }

    [Fact]
    public void GivenATinyScreen_WhenBuilding_ThenThereIsNoFrame()
    {
        Assert.Null(ContextMenuFrameBuilder.Build(Menu(), new MenuAnchor(0, 0), 10, 24, GraphGlyphSet.Unicode));
        Assert.Null(ContextMenuFrameBuilder.Build(Menu(), new MenuAnchor(0, 0), 80, 3, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenTheBorderLines_WhenAskingForAnEntry_ThenNoneIsReturned()
    {
        var menu = Menu();

        Assert.Null(ContextMenuFrameBuilder.EntryIndexAt(menu, 0, 24));
        Assert.Null(ContextMenuFrameBuilder.EntryIndexAt(menu, 1, 24));
        Assert.Equal(0, ContextMenuFrameBuilder.EntryIndexAt(menu, 2, 24));
        Assert.Null(ContextMenuFrameBuilder.EntryIndexAt(menu, RowActions.OfGraphRow.Count + GraphMenuGroups + 1, 24));
    }

    [Theory]
    [InlineData(SidebarRowKind.LocalBranch, SidebarSection.Branches)]
    [InlineData(SidebarRowKind.Tag, SidebarSection.Tags)]
    [InlineData(SidebarRowKind.Section, SidebarSection.Worktrees)]
    [InlineData(SidebarRowKind.Worktree, SidebarSection.Worktrees)]
    [InlineData(SidebarRowKind.Stash, SidebarSection.Stashes)]
    [InlineData(SidebarRowKind.Section, SidebarSection.Stashes)]
    [InlineData(SidebarRowKind.Section, SidebarSection.Branches)]
    [InlineData(SidebarRowKind.Section, SidebarSection.Tags)]
    public void GivenAFormerHeaderChipAction_WhenLookingInTheMenus_ThenTheSectionRowsOfferIt(
        SidebarRowKind kind,
        SidebarSection section)
    {
        // The chips are dropped (#469): every action a chip ran must be in the menu of the section's rows.
        var itemKind = section switch
        {
            SidebarSection.Branches => SidebarRowKind.LocalBranch,
            SidebarSection.Tags => SidebarRowKind.Tag,
            SidebarSection.Worktrees => SidebarRowKind.Worktree,
            _ => SidebarRowKind.Stash,
        };
        var offered = new[] { SidebarRowKind.Section, itemKind }
            .SelectMany(rowKind => RowActions.OfSidebarRow(Row(rowKind, section)))
            .ToHashSet();

        var missing = ChipActions[section].Where(action => !offered.Contains(action)).ToList();

        Assert.Empty(missing);
        Assert.NotEmpty(RowActions.OfSidebarRow(Row(kind, section)));
    }

    // The actions the dropped header chips ran, per section.
    private static readonly Dictionary<SidebarSection, ActionId[]> ChipActions = new()
    {
        [SidebarSection.Branches] =
        [
            ActionIds.BranchCreate, ActionIds.BranchSwitch, ActionIds.MergeBranch, ActionIds.BranchRename,
            ActionIds.BranchDelete, ActionIds.BranchCreateOnly, ActionIds.MergeBranchNoFastForward,
            ActionIds.MergeBranchSquash, ActionIds.RebaseOnto, ActionIds.ResetBranch, ActionIds.PickCherry,
            ActionIds.PickRevert, ActionIds.BranchSetUpstream, ActionIds.BranchUnsetUpstream,
        ],
        [SidebarSection.Tags] =
        [
            ActionIds.TagCreate, ActionIds.MergeBranch, ActionIds.TagDelete, ActionIds.TagPush,
            ActionIds.TagDeleteRemote, ActionIds.MergeBranchNoFastForward, ActionIds.MergeBranchSquash,
            ActionIds.RebaseOnto, ActionIds.ResetBranch, ActionIds.PickCherry, ActionIds.PickRevert,
        ],
        [SidebarSection.Worktrees] =
        [
            ActionIds.WorktreeAdd, ActionIds.WorktreeRemove, ActionIds.WorktreeLock, ActionIds.WorktreeUnlock,
            ActionIds.WorktreePrune,
        ],
        [SidebarSection.Stashes] =
        [
            ActionIds.StashPush, ActionIds.StashApply, ActionIds.StashPop, ActionIds.StashDrop,
            ActionIds.StashPushUntracked,
        ],
    };

    private static SidebarRow Row(SidebarRowKind kind, SidebarSection section) =>
        new(kind, section, "x", string.Empty, null, false, false, 0, null, null);
}
