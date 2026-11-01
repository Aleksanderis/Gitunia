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
        state = engine.Process(state, new KeyEvent("b"), DefaultKeymap.GraphView).State;
        state = engine.Process(state, new KeyEvent("r"), DefaultKeymap.GraphView).State;
        var rows = WhichKeyBuilder.Build(engine.Continuations(state, DefaultKeymap.GraphView), state.PendingSequence);

        return Text(WhichKeyFrameBuilder.Build("<space>br", rows, width, ScreenHeight, GraphGlyphSet.Unicode));
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
                "│  Graph           │ Branch                                                  │",
                "│  Lists           │  <space>bc    │ Create a branch and check it out        │",
                "│  Global          │  <space>bC    │ Create a branch without checking out    │",
                "│  Markers         │  <space>bs    │ Switch to a branch (check out)          │",
                "│  All Global      │  <space>bn    │ Rename the branch                       │",
                "│  All Lists       │  <space>bd    │ Delete the branch (asks first)          │",
                "│> All Graph       │  <space>bu    │ Set the branch upstream                 │",
                "│  All Sidebar     │  <space>bU    │ Unset the branch upstream               │",
                "│  All Detail      │  <space>bf    │ Fast-forward the branch to its upstream │",
                "│  All Diff        │               │ (no checkout)                           │",
                "│  All Changes     │                                                         │",
                "│  All Reflog      │ Merge and rebase                                        │",
                "│  All Rebase todo │  <space>brr   │ Rebase the current branch onto the      │",
                "│  All Conflicts   │               │ selection                               │",
                "│  All Settings    │  <space>bri   │ Interactive rebase onto the selection   │",
                "│  All Repeat mode │               │ (edit the todo)                         │",
                "│  Commands        │  <space>brI   │ Interactive rebase from the root commit │",
                "│                  │               │ (selection ignored)                     │",
                "│                  │  <space>brf   │ Fix up the commit into the one before it│",
                "│ 1-19/71  Esc close  Tab next section  list keys scroll                     │",
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
                "│  Graph           │ Branch                                                                    │",
                "│  Lists           │  <space>bc    │ Create a branch and check it out                          │",
                "│  Global          │  <space>bC    │ Create a branch without checking out                      │",
                "│  Markers         │  <space>bs    │ Switch to a branch (check out)                            │",
                "│  All Global      │  <space>bn    │ Rename the branch                                         │",
                "│  All Lists       │  <space>bd    │ Delete the branch (asks first)                            │",
                "│> All Graph       │  <space>bu    │ Set the branch upstream                                   │",
                "│  All Sidebar     │  <space>bU    │ Unset the branch upstream                                 │",
                "│  All Detail      │  <space>bf    │ Fast-forward the branch to its upstream (no checkout)     │",
                "│  All Diff        │                                                                           │",
                "│  All Changes     │ Merge and rebase                                                          │",
                "│  All Reflog      │  <space>brr   │ Rebase the current branch onto the selection              │",
                "│  All Rebase todo │  <space>bri   │ Interactive rebase onto the selection (edit the todo)     │",
                "│  All Conflicts   │  <space>brI   │ Interactive rebase from the root commit (selection        │",
                "│  All Settings    │               │ ignored)                                                  │",
                "│  All Repeat mode │  <space>brf   │ Fix up the commit into the one before it (asks first)     │",
                "│  Commands        │  <space>brw   │ Reword the commit subject (asks first)                    │",
                "│                  │  <space>brd   │ Drop the commit from the branch (asks first)              │",
                "│                  │                                                                           │",
                "│ 1-19/61  Esc close  Tab next section  list keys scroll                                       │",
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
                "│  Graph           │ Graph                                                   │",
                "│  Lists           │  <space>h     │ Unbound: Jump to HEAD                   │",
                "│  Global          │  J            │ Move up                                 │",
                "│  Markers         │                                                         │",
                "│  All Global      │                                                         │",
                "│  All Lists       │                                                         │",
                "│  All Graph       │                                                         │",
                "│  All Sidebar     │                                                         │",
                "│  All Detail      │                                                         │",
                "│  All Diff        │                                                         │",
                "│  All Changes     │                                                         │",
                "│  All Reflog      │                                                         │",
                "│  All Rebase todo │                                                         │",
                "│  All Conflicts   │                                                         │",
                "│  All Settings    │                                                         │",
                "│  All Repeat mode │                                                         │",
                "│  Commands        │                                                         │",
                "│> Custom          │                                                         │",
                "│ Esc close  Tab next section                                                │",
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
                "│  Graph           │ Command palette                                         │",
                "│  Lists           │  :            │ Type to filter every action by title or │",
                "│  Global          │               │ id; Enter runs it, Esc closes           │",
                "│  Markers         │                                                         │",
                "│  All Global      │ Help                                                    │",
                "│  All Lists       │  /            │ Filter the rows of every section by key,│",
                "│  All Graph       │               │ title or action id; Enter keeps the     │",
                "│  All Sidebar     │               │ filter, Esc clears it, then closes      │",
                "│  All Detail      │                                                         │",
                "│  All Diff        │ Mouse                                                   │",
                "│  All Changes     │  Click        │ Focus a pane, select a row, switch a tab│",
                "│  All Reflog      │               │ or a help section; outside a popup it   │",
                "│  All Rebase todo │               │ closes                                  │",
                "│  All Conflicts   │  Double-click │ Graph row: the actions menu; Changes    │",
                "│  All Settings    │               │ view file: stage or unstage it          │",
                "│  All Repeat mode │  ...          │ Marker on the selected sidebar row: its │",
                "│> Commands        │               │ actions menu                            │",
                "│                  │  Wheel        │ Scroll the pane under the pointer       │",
                "│                  │  Drag         │ Select a range of rows; drag a divider  │",
                "│ 1-19/22  Esc close  Tab next section  list keys scroll                     │",
                "╰────────────────────────────────────────────────────────────────────────────╯"),
            HelpText(Table, "Commands", 80));
    }

    [Fact]
    public void GivenAFilterBeingTyped_WhenRendering_ThenTheTitleShowsItWithACursorAndTheFooterOffersEnterAndEsc()
    {
        var page = HelpBuilder.Filter(HelpBuilder.Build(Table, DefaultKeymap.GraphView), "<space>gc");

        var lines = HelpFrameBuilder.Build(
            [page], 0, DefaultKeymap.GraphView, 0, 80, ScreenHeight, GraphGlyphSet.Unicode,
            filter: "<space>gc", isEditingFilter: true)!.Lines.Select(line => line.Text).ToList();

        Assert.Equal(" Keys: Graph  / <space>gc_".PadRight(lines[0].Length), lines[0]);
        Assert.Contains("Esc clear filter  Enter keep filter", lines[^2]);
        Assert.Contains(lines, line => line.Contains("<space>gc"));
        Assert.DoesNotContain(lines, line => line.Contains("<space>gd"));
    }

    [Fact]
    public void GivenAKeptFilterWithNoMatch_WhenRendering_ThenTheTitleHasNoCursorAndTheMessageShows()
    {
        var page = HelpBuilder.Filter(HelpBuilder.Build(Table, DefaultKeymap.GraphView), "zzzqqq");

        var lines = HelpFrameBuilder.Build(
            [page], 0, DefaultKeymap.GraphView, 0, 80, ScreenHeight, GraphGlyphSet.Unicode, filter: "zzzqqq")!
            .Lines.Select(line => line.Text).ToList();

        Assert.StartsWith(" Keys: Graph  / zzzqqq", lines[0]);
        Assert.DoesNotContain("_", lines[0]);
        Assert.Contains(lines, line => line.Contains("Nothing matches the filter."));
        Assert.Contains("Esc clear filter", lines[^2]);
        Assert.DoesNotContain("Enter keep filter", lines[^2]);
    }

    [Fact]
    public void GivenTheGitWhichKeyPopupAt80Columns_WhenRendering_ThenItMatchesTheSnapshot()
    {
        Assert.Equal(
            string.Join(
                '\n',
                " <space>br-                                                        ",
                "│ i  Interactive rebase onto the selection (edit the todo)        │",
                "│ r  Rebase the current branch onto the selection                 │",
                "│ ─────────────────────────────────────────────────────────────── │",
                "│ +  +more (variants and shortcuts)                               │",
                "│ d  Drop the commit from the branch (asks first)                 │",
                "│ f  Fix up the commit into the one before it (asks first)        │",
                "│ I  Interactive rebase from the root commit (selection ignored)  │",
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
                " <space>br-                                                        ",
                "│ i  Interactive rebase onto the selection (edit the todo)        │",
                "│ r  Rebase the current branch onto the selection                 │",
                "│ ─────────────────────────────────────────────────────────────── │",
                "│ +  +more (variants and shortcuts)                               │",
                "│ d  Drop the commit from the branch (asks first)                 │",
                "│ f  Fix up the commit into the one before it (asks first)        │",
                "│ I  Interactive rebase from the root commit (selection ignored)  │",
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
