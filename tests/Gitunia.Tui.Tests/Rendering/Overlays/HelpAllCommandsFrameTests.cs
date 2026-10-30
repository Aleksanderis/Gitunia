using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

/// <summary>Snapshots of the all-commands help tabs and the which-key popup at 80 and 120 columns (#838).</summary>
public class HelpAllCommandsFrameTests
{
    private const int ScreenHeight = 24;

    private static readonly KeyBindingTable Table = DefaultKeymap.Create();

    private static string Text(OverlayFrame? frame) =>
        frame is null ? "<none>" : string.Join('\n', frame.Lines.Select(line => line.Text));

    private static string HelpText(KeyBindingTable table, string title, int width)
    {
        var tabs = HelpBuilder.Build(table, DefaultKeymap.GraphView);
        var index = tabs.ToList().FindIndex(tab => tab.Title == title);

        return Text(HelpFrameBuilder.Build(tabs, index, DefaultKeymap.GraphView, 0, width, ScreenHeight, GraphGlyphSet.Unicode));
    }

    private static string WhichKeyText(int width)
    {
        var engine = new KeymapEngine(Table);
        var state = engine.Process(KeymapState.Initial, new KeyEvent(" "), DefaultKeymap.GraphView).State;
        state = engine.Process(state, new KeyEvent("g"), DefaultKeymap.GraphView).State;
        var rows = WhichKeyBuilder.Build(engine.Continuations(state, DefaultKeymap.GraphView), state.PendingSequence);

        return Text(WhichKeyFrameBuilder.Build("<space>g", rows, width, ScreenHeight, GraphGlyphSet.Unicode));
    }

    private static KeyBindingTable Customised() => KeymapConfigurator.Apply(
        Table,
        [
            new ConfigKeyEntry { View = "Graph", Key = "J", Action = "list.up" },
            new ConfigKeyEntry { View = "Graph", Key = "<space>h", Action = "none" },
        ]).Table;

    [Fact]
    public void GivenTheAllGraphHelpTabAt80Columns_WhenRendering_ThenItMatchesTheSnapshot()
    {
        Assert.Equal(
            string.Join(
                '\n',
                " Keys: Graph                                                                  ",
                "│ [All Graph] 7/17                                                           │",
                "│ Branch                                                                     │",
                "│  <space>bc    │ Create a branch and check it out                           │",
                "│  <space>bC    │ Create a branch without checking out                       │",
                "│  <space>bs    │ Switch to a branch (check out)                             │",
                "│  <space>br    │ Rename the branch                                          │",
                "│  <space>bd    │ Delete the branch (asks first)                             │",
                "│  <space>bu    │ Set the branch upstream                                    │",
                "│  <space>bU    │ Unset the branch upstream                                  │",
                "│  <space>bf    │ Fast-forward the branch to its upstream (no checkout)      │",
                "│                                                                            │",
                "│ Merge and rebase                                                           │",
                "│  <space>gr    │ Rebase the current branch onto the selection               │",
                "│  <space>gi    │ Interactive rebase onto the selection (edit the todo)      │",
                "│  <space>gI    │ Interactive rebase from the root commit (selection ignored)│",
                "│  <space>gf    │ Fix up the commit into the one before it (asks first)      │",
                "│  <space>gw    │ Reword the commit subject (asks first)                     │",
                "│  <space>gd    │ Drop the commit from the branch (asks first)               │",
                "│                                                                            │",
                "│ 1-18/60  Esc close  Tab next tab  list keys scroll                         │",
                "╰────────────────────────────────────────────────────────────────────────────╯"),
            HelpText(Table, "All Graph", 80));
    }

    [Fact]
    public void GivenTheAllGraphHelpTabAt120Columns_WhenRendering_ThenItMatchesTheSnapshot()
    {
        Assert.Equal(
            string.Join(
                '\n',
                " Keys: Graph                                                                                    ",
                "│ [All Graph] 7/17                                                                             │",
                "│ Branch                                                                                       │",
                "│  <space>bc    │ Create a branch and check it out                                             │",
                "│  <space>bC    │ Create a branch without checking out                                         │",
                "│  <space>bs    │ Switch to a branch (check out)                                               │",
                "│  <space>br    │ Rename the branch                                                            │",
                "│  <space>bd    │ Delete the branch (asks first)                                               │",
                "│  <space>bu    │ Set the branch upstream                                                      │",
                "│  <space>bU    │ Unset the branch upstream                                                    │",
                "│  <space>bf    │ Fast-forward the branch to its upstream (no checkout)                        │",
                "│                                                                                              │",
                "│ Merge and rebase                                                                             │",
                "│  <space>gr    │ Rebase the current branch onto the selection                                 │",
                "│  <space>gi    │ Interactive rebase onto the selection (edit the todo)                        │",
                "│  <space>gI    │ Interactive rebase from the root commit (selection ignored)                  │",
                "│  <space>gf    │ Fix up the commit into the one before it (asks first)                        │",
                "│  <space>gw    │ Reword the commit subject (asks first)                                       │",
                "│  <space>gd    │ Drop the commit from the branch (asks first)                                 │",
                "│                                                                                              │",
                "│ 1-18/59  Esc close  Tab next tab  list keys scroll                                           │",
                "╰──────────────────────────────────────────────────────────────────────────────────────────────╯"),
            HelpText(Table, "All Graph", 120));
    }

    [Fact]
    public void GivenTheCustomHelpTabAt80Columns_WhenRendering_ThenItMatchesTheSnapshot()
    {
        Assert.Equal(
            string.Join(
                '\n',
                " Keys: Graph                                                                  ",
                "│ [Custom] 18/18                                                             │",
                "│ Graph                                                                      │",
                "│  <space>h     │ Unbound: Jump to HEAD                                      │",
                "│  J            │ Move up                                                    │",
                "│ Esc close  Tab next tab                                                    │",
                "╰────────────────────────────────────────────────────────────────────────────╯"),
            HelpText(Customised(), "Custom", 80));
    }

    [Fact]
    public void GivenTheCommandsHelpTabAt80Columns_WhenRendering_ThenItMatchesTheSnapshot()
    {
        Assert.Equal(
            string.Join(
                '\n',
                " Keys: Graph                                                                  ",
                "│ [Commands] 17/17                                                           │",
                "│ Command palette                                                            │",
                "│  :            │ Type to filter every action by title or id; Enter runs it, │",
                "│               │ Esc closes                                                 │",
                "│                                                                            │",
                "│ Mouse                                                                      │",
                "│  Click        │ Focus a pane, select a row, switch a tab or a help tab;    │",
                "│               │ outside a popup it closes                                  │",
                "│  Double-click │ Graph row: the actions menu; working-tree file: stage or   │",
                "│               │ unstage it                                                 │",
                "│  ...          │ Marker on the selected sidebar row: its actions menu       │",
                "│  Wheel        │ Scroll the pane under the pointer                          │",
                "│  Drag         │ Select a range of rows; drag a divider to resize the panes │",
                "│  Tab row      │ Chips: commands (palette), + (new tab), fetch, pull and    │",
                "│               │ push                                                       │",
                "│ Esc close  Tab next tab                                                    │",
                "╰────────────────────────────────────────────────────────────────────────────╯"),
            HelpText(Table, "Commands", 80));
    }

    [Fact]
    public void GivenTheGitWhichKeyPopupAt80Columns_WhenRendering_ThenItMatchesTheSnapshot()
    {
        Assert.Equal(
            string.Join(
                '\n',
                " <space>g-                                                         ",
                "│ i  Interactive rebase onto the selection (edit the todo)        │",
                "│ r  Rebase the current branch onto the selection                 │",
                "│                                                                 │",
                "│ m  Merge the selection (fast-forward if possible)               │",
                "│ M  Merge the selection with a merge commit                      │",
                "│                                                                 │",
                "│ p  Cherry-pick the selection onto the current branch            │",
                "│ v  Revert the selection with new commits                        │",
                "│                                                                 │",
                "│ x  Reset the current branch to the selection                    │",
                "│ ─────────────────────────────────────────────────────────────── │",
                "│ …  +more (variants and shortcuts)                               │",
                "│ d  Drop the commit from the branch (asks first)                 │",
                "│ f  Fix up the commit into the one before it (asks first)        │",
                "│ I  Interactive rebase from the root commit (selection ignored)  │",
                "│ s  Squash-merge the selection (stages changes)                  │",
                "│ w  Reword the commit subject (asks first)                       │",
                "╰─────────────────────────────────────────────────────────────────╯"),
            WhichKeyText(80));
    }

    [Fact]
    public void GivenTheGitWhichKeyPopupAt120Columns_WhenRendering_ThenItMatchesTheSnapshot()
    {
        Assert.Equal(
            string.Join(
                '\n',
                " <space>g-                                                         ",
                "│ i  Interactive rebase onto the selection (edit the todo)        │",
                "│ r  Rebase the current branch onto the selection                 │",
                "│                                                                 │",
                "│ m  Merge the selection (fast-forward if possible)               │",
                "│ M  Merge the selection with a merge commit                      │",
                "│                                                                 │",
                "│ p  Cherry-pick the selection onto the current branch            │",
                "│ v  Revert the selection with new commits                        │",
                "│                                                                 │",
                "│ x  Reset the current branch to the selection                    │",
                "│ ─────────────────────────────────────────────────────────────── │",
                "│ …  +more (variants and shortcuts)                               │",
                "│ d  Drop the commit from the branch (asks first)                 │",
                "│ f  Fix up the commit into the one before it (asks first)        │",
                "│ I  Interactive rebase from the root commit (selection ignored)  │",
                "│ s  Squash-merge the selection (stages changes)                  │",
                "│ w  Reword the commit subject (asks first)                       │",
                "╰─────────────────────────────────────────────────────────────────╯"),
            WhichKeyText(120));
    }

    [Fact]
    public void GivenEntriesOfOneGroupInTwoClusters_WhenGrouping_ThenABlankLineSeparatesTheClusters()
    {
        PaletteEntry[] entries =
        [
            new(new ActionId("a.one"), "One", "1", true, "Group", "a"),
            new(new ActionId("a.two"), "Two", "2", true, "Group", "a"),
            new(new ActionId("b.one"), "Three", "3", true, "Group", "b"),
            new(new ActionId("b.two"), "Four", "4", true, "Group", "b"),
        ];

        var grouped = GroupedRows.Of(entries, 20, scrolls: false);

        Assert.Equal(6, grouped.Lines.Count);
        Assert.Equal("Group", grouped.Lines[0].Header);
        Assert.Equal(new GroupedRow(null, null), grouped.Lines[3]);
        Assert.Equal(2, grouped.Lines[4].Entry);
        Assert.Null(grouped.EntryAt(3));
    }

    [Fact]
    public void GivenTheGapsAreDropped_WhenGroupingTightly_ThenNoBlankLineSeparatesTheClusters()
    {
        PaletteEntry[] entries =
        [
            new(new ActionId("a.one"), "One", "1", true, "Group", "a"),
            new(new ActionId("a.two"), "Two", "2", true, "Group", "a"),
            new(new ActionId("b.one"), "Three", "3", true, "Group", "b"),
            new(new ActionId("b.two"), "Four", "4", true, "Group", "b"),
        ];

        var grouped = GroupedRows.Of(entries, 5, scrolls: false);

        Assert.Equal(5, grouped.Lines.Count);
        Assert.DoesNotContain(grouped.Lines, line => line is { Header: null, Entry: null });
    }
}
