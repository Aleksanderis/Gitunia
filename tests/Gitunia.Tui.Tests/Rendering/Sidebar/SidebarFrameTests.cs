using Gitunia.Core.Graph;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Tests.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Sidebar;

public class SidebarFrameTests
{
    private static readonly ScreenModel Model = new("repo", "main", 1, 1, "NORMAL", string.Empty, 1, 2, false, string.Empty);

    private static SidebarRow Section(SidebarSection section, string title, int count, bool isExpanded) =>
        new(SidebarRowKind.Section, section, title, string.Empty, null, false, isExpanded, count, null, null);

    private static SidebarRow Branch(string name, string worktree = "", bool isCurrent = false, int? ahead = null, int? behind = null) =>
        new(SidebarRowKind.LocalBranch, SidebarSection.Branches, name, worktree, null, isCurrent, false, 0, ahead, behind);

    [Fact]
    public void GivenWorktreeRowsWithStatus_WhenRendered_ThenCountsAndTrackingShowAndCleanOnesStayPlain()
    {
        var dirty = new SidebarRow(
            SidebarRowKind.Worktree,
            SidebarSection.Worktrees,
            "wt-a",
            "feat",
            null,
            false,
            false,
            0,
            2,
            1,
            "/work/wt-a",
            new WorktreeStatus(1, 3, 0, 2, 1));
        var none = new WorktreeStatus(0, 0, 0, 0, 0);
        var clean = dirty with { Label = "wt-b", Ahead = null, Behind = null, Changes = none };
        var conflicted = dirty with { Label = "wt-c", Changes = new WorktreeStatus(0, 0, 4, 2, 1) };
        var lines = new[] { dirty, clean, conflicted }
            .Select(row => SidebarRowRenderer.Render(row, 40, false, GraphGlyphSet.Unicode).Text.TrimEnd())
            .ToArray();

        Assert.Equal("    wt-a ↑2↓1 S1 M3 feat", lines[0]);
        Assert.Equal("    wt-b feat", lines[1]);
        Assert.Equal("    wt-c ↑2↓1 U4 feat", lines[2]);
    }

    private static SidebarModel Sidebar(params SidebarRow[] rows) => new([.. rows], 0, 0, string.Empty, false, false);

    private static CommitGraph Graph() => CommitFactory.Graph(
        CommitFactory.Commit(2, "second", [1]),
        CommitFactory.Commit(1, "first", []));

    private static string[] Frame(SidebarModel? sidebar, int width, int height, GraphGlyphSet? glyphs = null) =>
        [.. ScreenComposer.Compose(Model, Graph(), 0, 0, width, height, glyphs ?? GraphGlyphSet.Unicode, null, sidebar).Select(line => line.Text.TrimEnd())];

    [Fact]
    public void GivenSidebarRows_WhenComposingAt80x9_ThenSidebarSitsLeftOfTheGraphWithADivider()
    {
        var sidebar = Sidebar(
            Section(SidebarSection.Branches, "Branches", 3, isExpanded: true),
            Branch("main", "repo", isCurrent: true, ahead: 2, behind: 1),
            Branch("feature/login", "wt-login"),
            Branch("old"),
            Section(SidebarSection.Tags, "Tags", 12, isExpanded: false));

        var frame = Frame(sidebar, 80, 10);

        Assert.Equal("   1 repo", frame[0]);
        Assert.StartsWith("╭─ repo ─────────────┬─ History ─", frame[1], StringComparison.Ordinal);
        Assert.StartsWith("│  ▾ Branches 3   ...│ ●  second", frame[2], StringComparison.Ordinal);
        Assert.StartsWith("│   * main ↑2↓1 w…   │ ●  first", frame[3], StringComparison.Ordinal);
        Assert.StartsWith("│     feature… wt…   │", frame[4], StringComparison.Ordinal);
        Assert.StartsWith("│     old            │", frame[5], StringComparison.Ordinal);
        Assert.StartsWith("│  ▸ Tags 12", frame[6], StringComparison.Ordinal);
        Assert.StartsWith("│                    │", frame[7], StringComparison.Ordinal);
        Assert.StartsWith("╰────────────────────┴───", frame[8], StringComparison.Ordinal);
        Assert.EndsWith("╮", frame[1], StringComparison.Ordinal);
        Assert.EndsWith("╯", frame[8], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenSidebarRows_WhenComposingWithoutUnicode_ThenGlyphsAreAscii()
    {
        var sidebar = Sidebar(
            Section(SidebarSection.Branches, "Branches", 1, isExpanded: true),
            Branch("main", isCurrent: true, ahead: 1, behind: 3),
            Section(SidebarSection.Tags, "Tags", 2, isExpanded: false));

        var frame = Frame(sidebar, 80, 8, GraphGlyphSet.Ascii);

        Assert.StartsWith("|  v Branches 1   ...| *  second", frame[2], StringComparison.Ordinal);
        Assert.StartsWith("|   * main +1-3      | *  first", frame[3], StringComparison.Ordinal);
        Assert.StartsWith("|  > Tags 2", frame[4], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenActiveFilter_WhenComposing_ThenFilterLineIsTheLastSidebarRow()
    {
        var sidebar = Sidebar(Section(SidebarSection.Branches, "Branches", 1, isExpanded: true), Branch("feature")) with { FilterText = "feat", IsEditingFilter = true };

        var frame = Frame(sidebar, 80, 8);

        Assert.StartsWith("│ /feat_             │", frame[5], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenScrolledSidebar_WhenComposing_ThenRowsStartAtTheTopRow()
    {
        var rows = Enumerable.Range(0, 10).Select(index => Branch($"branch{index}")).ToArray();

        var lines = ScreenComposer.Compose(Model, Graph(), 0, 0, 80, 9, GraphGlyphSet.Unicode, null, Sidebar(rows) with { TopRow = 6, Cursor = 7 });

        Assert.StartsWith("│     branch6", lines[2].Text, StringComparison.Ordinal);
        Assert.StartsWith("│     branch8", lines[4].Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SidebarSection.Branches, "Branches", 3)]
    [InlineData(SidebarSection.Tags, "Tags", 2)]
    [InlineData(SidebarSection.Worktrees, "Worktrees", 2)]
    [InlineData(SidebarSection.Stashes, "Stashes", 2)]
    public void GivenASectionHeaderInAWideSidebar_WhenRendering_ThenOnlyTitleAndCountAreDrawn(
        SidebarSection section,
        string title,
        int count)
    {
        var row = Section(section, title, count, isExpanded: true);

        var line = SidebarRowRenderer.Render(row, 160, isSelected: false, GraphGlyphSet.Unicode);

        Assert.Equal(160, TextWidth.Of(line.Text));
        Assert.EndsWith($"{title} {count}", line.Text.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain("[", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenSelectedSidebarRow_WhenRenderingIt_ThenTheLineIsMarkedSelected()
    {
        var line = SidebarRowRenderer.Render(Branch("main"), 20, isSelected: true, GraphGlyphSet.Unicode);

        Assert.True(line.IsSelected);
        Assert.Equal("    main", line.Text.TrimEnd());
    }

    [Fact]
    public void GivenLongNameAndWorktree_WhenRenderingNarrow_ThenNameIsCutButWorktreeMarkerStaysVisible()
    {
        var line = SidebarRowRenderer.Render(Branch("feature/a-very-long-branch-name", "wt-a"), 24, isSelected: false, GraphGlyphSet.Unicode);

        Assert.Equal(24, TextWidth.Of(line.Text));
        Assert.EndsWith("wt:wt-a", line.Text.TrimEnd(), StringComparison.Ordinal);
        Assert.Contains("…", line.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(59)]
    [InlineData(40)]
    [InlineData(20)]
    public void GivenNarrowTerminal_WhenComposing_ThenSidebarIsHiddenAndGraphUsesTheWholeWidth(int width)
    {
        var withSidebar = ScreenComposer.Compose(Model, Graph(), 0, 0, width, 8, GraphGlyphSet.Unicode, null, Sidebar(Section(SidebarSection.Branches, "Branches", 0, true)));
        var without = ScreenComposer.Compose(Model, Graph(), 0, 0, width, 8, GraphGlyphSet.Unicode);

        Assert.Equal(without.Select(line => line.Text), withSidebar.Select(line => line.Text));
    }

    [Theory]
    [InlineData(60, 8)]
    [InlineData(80, 24)]
    [InlineData(200, 60)]
    [InlineData(61, 3)]
    [InlineData(90, 1)]
    public void GivenAnyTerminalSize_WhenComposingWithSidebar_ThenEveryLineFillsTheWidthExactly(int width, int height)
    {
        var sidebar = Sidebar(Section(SidebarSection.Branches, "Branches", 1, true), Branch("修正ブランチ", "ワークツリー", true, 1, 1));

        var lines = ScreenComposer.Compose(Model, Graph(), 0, 0, width, height, GraphGlyphSet.Unicode, null, sidebar);

        Assert.Equal(height, lines.Count);
        Assert.All(lines, line => Assert.Equal(width, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenFocusedSidebar_WhenComposing_ThenItsFrameIsTheFocusedBorderRoleAndTheGraphsIsNot()
    {
        var focused = ScreenComposer.Compose(Model, Graph(), 0, 0, 80, 8, GraphGlyphSet.Unicode, null, Sidebar() with { IsFocused = true });
        var unfocused = ScreenComposer.Compose(Model, Graph(), 0, 0, 80, 8, GraphGlyphSet.Unicode, null, Sidebar());

        Assert.Equal(SegmentRole.FocusedBorder, focused[2].Segments[0].Role);
        Assert.Equal(SegmentRole.Border, focused[2].Segments[^1].Role);
        Assert.Equal(SegmentRole.Border, unfocused[2].Segments[0].Role);
        Assert.Equal(SegmentRole.FocusedBorder, unfocused[2].Segments[^1].Role);
    }

    [Fact]
    public void GivenEmptySidebar_WhenComposing_ThenColumnIsBlank()
    {
        var model = new SidebarModel([], 0, 0, string.Empty, false, false);

        var frame = Frame(model, 80, 6);

        Assert.StartsWith("│                    │ ●  second", frame[2], StringComparison.Ordinal);
    }
}
