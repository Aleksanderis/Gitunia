using Gitunia.Core.Diff;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Diff.Models;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.Rendering.WorkingTree;

[Trait("Os", "Independent")]
public class WorkingTreeDiffFrameTests
{
    private static ScreenModel Model(PaneKind focused) =>
        new("repo", "main", 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty, Hints: PaneHints.For(focused));

    private static DiffDocument Document()
    {
        var hunk = new DiffHunk(
            "@@ -1,3 +1,3 @@",
            1,
            3,
            1,
            3,
            string.Empty,
            [
                new DiffLine(DiffLineKind.Context, "first", 1, 1, false),
                new DiffLine(DiffLineKind.Removed, "second", 2, null, false),
                new DiffLine(DiffLineKind.Added, "SECOND", null, 2, false),
                new DiffLine(DiffLineKind.Context, "third", 3, 3, false),
            ]);
        var file = new DiffFile("src/app.cs", "src/app.cs", DiffFileStatus.Modified, null, null, null, false, [], [hunk]);
        var builder = new DiffDocumentBuilder();
        builder.Add(file);
        return builder.Build([]);
    }

    private static WorkingTreeModel Working(PaneKind focused, WorkingTreeSection section = WorkingTreeSection.Unstaged, int selectedRow = 3)
    {
        var tree = Tree(Modified("src/app.cs"), StagedModified("README.md"));
        var list = section == WorkingTreeSection.Staged ? tree.Staged : tree.Unstaged;
        tree = tree with { Selection = new WorkingTreeSelection(section, list[0].Path, 0) };
        var diff = new DiffPaneModel(Document(), selectedRow, 0, WorkingPanes.IsDiff(focused));
        return new WorkingTreeModel(tree, focused, Diff: diff);
    }

    private static string Frame(WorkingTreeModel working, int width, int height, GraphGlyphSet? glyphs = null) =>
        string.Join(
            '\n',
            ScreenComposer.ComposeWorkingTree(Model(working.Focused), working, width, height, glyphs ?? GraphGlyphSet.Unicode)
                .Select(line => line.Text.TrimEnd()));

    [Fact]
    public void GivenAnUnstagedFileUnderTheCursor_WhenComposingAt90x16_ThenTheDiffShowsBesideTheLists()
    {
        var frame = Frame(Working(PaneKind.WorkingUnstaged), 90, 16);

        Assert.Equal(
            string.Join(
                '\n',
                "   1 repo",
                "╭─ Unstaged 1 ────────────────┬─ Diff  unstaged  change 1/1 …─ stage ─ hunk › ─ view ▾ ──╮",
                "│ M src/app.cs                │ M src/app.cs                                             │",
                "│                             │ ── @@ -1,3 +1,3 @@ ───────────────────────────────────── │",
                "│                             │   1   1  first                                           │",
                "│                             │   2     -second                                          │",
                "│                             │       2 +SECOND                                          │",
                "│                             │   3   3  third                                           │",
                "├─ Staged 1 ──────────────────┼─ Commit message ─────────────────────────────────────────┤",
                "│ M README.md                 │ Press i or Enter to write the message, <space>c in the … │",
                "│                             │                                                          │",
                "│                             │                                                          │",
                "│                             │                                                          │",
                "│                             │ [ ] SignOff  [ ] No-Verify  [ ] Amend     Ctrl-g Commit  │",
                "╰─────────────────────────────┴──────────────────────────────────────────────────────────╯",
                " NORMAL  1/1  ? help  : commands  q quit  j/k move  <space>a stage  <space>A stage all  E…"),
            frame);
    }

    [Fact]
    public void GivenAStagedFileWithTheDiffFocused_WhenComposingAt90x16_ThenTheTitleNamesTheStagedSide()
    {
        var frame = Frame(Working(PaneKind.WorkingStagedDiff, WorkingTreeSection.Staged), 90, 16);

        Assert.Contains("─ Diff  staged  change 1/1  ", frame, StringComparison.Ordinal);
        Assert.Contains(" unstage ", frame, StringComparison.Ordinal);
        Assert.DoesNotContain(" discard ", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheDiffFocusedOnANarrowScreen_WhenComposingAt60x12_ThenTheDiffTakesTheWholeScreen()
    {
        var frame = Frame(Working(PaneKind.WorkingUnstagedDiff), 60, 12);

        Assert.Equal(
            string.Join(
                '\n',
                "   1 repo",
                "╭─ Diff  unstaged  change 1/1 …─ stage ─ hunk › ─ view ▾ ──╮",
                "│ M src/app.cs                                             │",
                "│ ── @@ -1,3 +1,3 @@ ───────────────────────────────────── │",
                "│   1   1  first                                           │",
                "│   2     -second                                          │",
                "│       2 +SECOND                                          │",
                "│   3   3  third                                           │",
                "│                                                          │",
                "│                                                          │",
                "╰──────────────────────────────────────────────────────────╯",
                " NORMAL  1/1  ? help  : commands  q quit  j/k scroll  <spac…"),
            frame);
    }

    [Fact]
    public void GivenAListFocusedOnANarrowScreen_WhenComposingAt60x12_ThenTheListShowsAndTheDiffWaitsForEnter()
    {
        var frame = Frame(Working(PaneKind.WorkingUnstaged), 60, 12);

        Assert.Contains("─ Unstaged 1 ─", frame, StringComparison.Ordinal);
        Assert.DoesNotContain("@@ -1,3", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheCursorOnTheChangedLine_WhenComposing_ThenOnlyTheDiffRowIsSelected()
    {
        var lines = ScreenComposer.ComposeWorkingTree(
            Model(PaneKind.WorkingUnstagedDiff),
            Working(PaneKind.WorkingUnstagedDiff, selectedRow: 4),
            90,
            16,
            GraphGlyphSet.Unicode);

        var selected = lines.Where(line => line.Segments.Any(segment => segment.Selection == SegmentSelection.Focused)).ToList();
        Assert.Single(selected);
        Assert.Contains("SECOND", selected[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenWrapAndContextOptions_WhenBuildingTheTitle_ThenTheMarkersComeBeforeThePath()
    {
        var model = Working(PaneKind.WorkingUnstagedDiff);
        var diff = model.Diff! with { IsWrapped = true, OptionsLabel = "[ctx 6]" };

        var title = WorkingTreePaneRenderer.DiffTitle(model.Tree, diff);

        Assert.Equal("Diff  unstaged  change 1/1  [wrap]  [ctx 6]  src/app.cs", title);
    }

    [Fact]
    public void GivenTheRepeatMode_WhenBuildingTheTitle_ThenTheMarkerComesRightAfterTheName()
    {
        var model = Working(PaneKind.WorkingUnstagedDiff);
        var diff = model.Diff! with { OptionsLabel = "[ctx 6]", IsRepeating = true };

        var title = WorkingTreePaneRenderer.DiffTitle(model.Tree, diff);
        var empty = WorkingTreePaneRenderer.DiffTitle(null, diff);

        Assert.Equal("Diff  [repeat]  unstaged  change 1/1  [ctx 6]  src/app.cs", title);
        Assert.Equal("Diff  [repeat]", empty);
    }

    [Fact]
    public void GivenNoFileOrNoDiff_WhenBuildingTheTitle_ThenItIsJustDiff()
    {
        Assert.Equal("Diff", WorkingTreePaneRenderer.DiffTitle(null, null));
        Assert.Equal("Diff", WorkingTreePaneRenderer.DiffTitle(Working(PaneKind.WorkingUnstaged).Tree, new DiffPaneModel(DiffDocument.Message("Loading..."), 0, 0, false)));
    }

    [Fact]
    public void GivenAnAsciiGlyphSet_WhenComposing_ThenTheDiffBoxHasNoUnicodeFrame()
    {
        var frame = Frame(Working(PaneKind.WorkingUnstagedDiff), 90, 16, GraphGlyphSet.Ascii);

        Assert.Contains("- Diff  unstaged  change 1/1 ", frame, StringComparison.Ordinal);
        Assert.Contains("[stage]", frame, StringComparison.Ordinal);
        Assert.DoesNotContain('─', frame);
    }

    [Theory]
    [InlineData(PaneKind.WorkingUnstagedDiff, 0, 0)]
    [InlineData(PaneKind.WorkingStagedDiff, 1, 1)]
    [InlineData(PaneKind.WorkingUnstagedDiff, 20, 6)]
    [InlineData(PaneKind.WorkingStagedDiff, 40, 8)]
    [InlineData(PaneKind.WorkingUnstagedDiff, 64, 12)]
    [InlineData(PaneKind.WorkingStagedDiff, 79, 24)]
    [InlineData(PaneKind.WorkingUnstagedDiff, 80, 24)]
    [InlineData(PaneKind.WorkingStagedDiff, 80, 8)]
    [InlineData(PaneKind.WorkingUnstagedDiff, 120, 36)]
    [InlineData(PaneKind.WorkingStagedDiff, 200, 60)]
    public void GivenAnySize_WhenComposingWithTheDiffFocused_ThenNoLineIsWiderThanTheScreenAndNothingThrows(PaneKind focused, int width, int height)
    {
        var lines = ScreenComposer.ComposeWorkingTree(Model(focused), Working(focused, WorkingPanes.SectionOf(focused)), width, height, GraphGlyphSet.Unicode);

        Assert.True(lines.Count <= Math.Max(height, 0));
        Assert.All(lines, line => Assert.True(TextWidth.Of(line.Text) <= width, $"{width}x{height}: '{line.Text}'"));
    }

    [Fact]
    public void GivenAnUnstagedDiffOnAWideScreen_WhenComposing_ThenTheTitleRowEndsInTheStagingAndDiffButtons()
    {
        var frame = Frame(Working(PaneKind.WorkingUnstagedDiff), 160, 16);

        var title = frame.Split('\n')[1];
        Assert.Contains(" stage ", title, StringComparison.Ordinal);
        Assert.Contains(" discard ", title, StringComparison.Ordinal);
        Assert.DoesNotContain(" zoom ", title, StringComparison.Ordinal);
        Assert.Contains(" view ▾ ", title, StringComparison.Ordinal);
        Assert.DoesNotContain(" unstage ", title, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheDiffZoomed_WhenComposing_ThenTheListsAndTheMessageBoxAreGone()
    {
        var working = Working(PaneKind.WorkingUnstagedDiff) with { IsDiffZoomed = true };

        var frame = Frame(working, 90, 16);

        Assert.DoesNotContain("Unstaged", frame, StringComparison.Ordinal);
        Assert.DoesNotContain("Commit message", frame, StringComparison.Ordinal);
        Assert.Contains("@@ -1,3 +1,3 @@", frame, StringComparison.Ordinal);
        Assert.Contains("╭─ Diff  unstaged", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheZoomFlagButAListFocused_WhenComposing_ThenTheLayoutIsNormal()
    {
        var working = Working(PaneKind.WorkingUnstaged) with { IsDiffZoomed = true };

        var frame = Frame(working, 90, 16);

        Assert.Contains("Unstaged", frame, StringComparison.Ordinal);
    }
}
