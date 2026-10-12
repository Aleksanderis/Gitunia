using System.Collections.Immutable;
using Gitunia.Core.Graph;
using Gitunia.Core.Sidebar;
using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering;

public class SidebarFrameTests
{
    private static readonly ScreenModel Model = new("repo", "main", 1, 1, "NORMAL", string.Empty, 1, 2, false, string.Empty);

    private static SidebarRow Section(SidebarSection section, string title, int count, bool isExpanded) =>
        new(SidebarRowKind.Section, section, title, string.Empty, null, false, isExpanded, count, null, null);

    private static SidebarRow Branch(string name, string worktree = "", bool isCurrent = false, int? ahead = null, int? behind = null) =>
        new(SidebarRowKind.LocalBranch, SidebarSection.Branches, name, worktree, null, isCurrent, false, 0, ahead, behind);

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

        var frame = Frame(sidebar, 80, 9);

        Assert.Equal(
            string.Join(
                '\n',
                " ● Gitunia  ─  repo",
                " 1 repo  main",
                " ▾ Branches 3       │●  0000002 second",
                "  * main ↑2↓1 wt:re…│●  0000001 first",
                "    feature… wt:wt-…│",
                "    old             │",
                " ▸ Tags 12          │",
                "                    │",
                " NORMAL  1/2  j/k gg G ^d ^u @ ? : q"),
            string.Join('\n', frame));
    }

    [Fact]
    public void GivenSidebarRows_WhenComposingWithoutUnicode_ThenGlyphsAreAscii()
    {
        var sidebar = Sidebar(
            Section(SidebarSection.Branches, "Branches", 1, isExpanded: true),
            Branch("main", isCurrent: true, ahead: 1, behind: 3),
            Section(SidebarSection.Tags, "Tags", 2, isExpanded: false));

        var frame = Frame(sidebar, 80, 8, GraphGlyphSet.Ascii);

        Assert.Equal(" v Branches 1       |*  0000002 second", frame[2]);
        Assert.Equal("  * main +1-3       |*  0000001 first", frame[3]);
        Assert.Equal(" > Tags 2           |", frame[4]);
    }

    [Fact]
    public void GivenActiveFilter_WhenComposing_ThenFilterLineIsTheLastSidebarRow()
    {
        var sidebar = Sidebar(Section(SidebarSection.Branches, "Branches", 1, isExpanded: true), Branch("feature")) with { FilterText = "feat", IsEditingFilter = true };

        var frame = Frame(sidebar, 80, 7);

        Assert.Equal("/feat_              │", frame[5]);
    }

    [Fact]
    public void GivenScrolledSidebar_WhenComposing_ThenRowsStartAtTheTopRow()
    {
        var rows = Enumerable.Range(0, 10).Select(index => Branch("branch" + index)).ToArray();

        var lines = ScreenComposer.Compose(Model, Graph(), 0, 0, 80, 7, GraphGlyphSet.Unicode, null, Sidebar(rows) with { TopRow = 6, Cursor = 7 });

        Assert.StartsWith("    branch6", lines[2].Text, StringComparison.Ordinal);
        Assert.StartsWith("    branch8", lines[4].Text, StringComparison.Ordinal);
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
    public void GivenFocusedSidebar_WhenComposing_ThenDividerIsTheFocusedBorderRole()
    {
        var lines = ScreenComposer.Compose(Model, Graph(), 0, 0, 80, 6, GraphGlyphSet.Unicode, null, Sidebar() with { IsFocused = true });

        Assert.Contains(lines[2].Segments, segment => segment.Role == SegmentRole.FocusedBorder);
        Assert.DoesNotContain(
            ScreenComposer.Compose(Model, Graph(), 0, 0, 80, 6, GraphGlyphSet.Unicode, null, Sidebar())[2].Segments,
            segment => segment.Role == SegmentRole.FocusedBorder);
    }

    [Fact]
    public void GivenEmptySidebar_WhenComposing_ThenColumnIsBlank()
    {
        var model = new SidebarModel(ImmutableArray<SidebarRow>.Empty, 0, 0, string.Empty, false, false);

        var frame = Frame(model, 80, 6);

        Assert.Equal("                    │●  0000002 second", frame[2]);
    }
}
