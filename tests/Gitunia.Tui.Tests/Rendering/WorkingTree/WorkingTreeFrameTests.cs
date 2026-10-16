using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.Rendering.WorkingTree;

public class WorkingTreeFrameTests
{
    private static ScreenModel Model(PaneKind focused = PaneKind.WorkingUnstaged) =>
        new("repo", "main", 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty, Hints: PaneHints.For(focused));

    private static WorkingTreeState MixedTree() => Tree(
        Modified("src/app.cs"),
        Untracked("notes.txt"),
        Conflict("src/merge.cs"),
        StagedModified("README.md"),
        StagedRename("old name.txt", "new name.txt"));

    private static string Frame(WorkingTreeState? tree, PaneKind focused, int width, int height, GraphGlyphSet? glyphs = null) =>
        string.Join(
            '\n',
            ScreenComposer.ComposeWorkingTree(Model(focused), new WorkingTreeModel(tree, focused), width, height, glyphs ?? GraphGlyphSet.Unicode)
                .Select(line => line.Text.TrimEnd()));

    [Fact]
    public void GivenMixedChanges_WhenComposingAt90x16_ThenListsCountsAndAnEmptyDiffBoxShow()
    {
        var frame = Frame(MixedTree(), PaneKind.WorkingUnstaged, 90, 16);

        Assert.Equal(
            string.Join(
                '\n',
                "   1 repo",
                "╭─ Unstaged 3 ────────────────┬─ Diff ───────────────────────────────────────────────────╮",
                "│ ! src/merge.cs  both modif… │                                                          │",
                "│ M src/app.cs                │                                                          │",
                "│ ? notes.txt                 │                                                          │",
                "│                             │                                                          │",
                "│                             │                                                          │",
                "│                             │                                                          │",
                "├─ Staged 2 ──────────────────┼─ Commit message ─────────────────────────────────────────┤",
                "│ M README.md                 │ The commit message box is not implemented yet            │",
                "│ R old name.txt -> new name… │                                                          │",
                "│                             │                                                          │",
                "│                             │                                                          │",
                "│                             │                                                          │",
                "╰─────────────────────────────┴──────────────────────────────────────────────────────────╯",
                " NORMAL  1/1  ? help  : commands  q quit  j/k move  <space>a stage  <space>A stage all  l…"),
            frame);
    }

    [Fact]
    public void GivenMixedChangesWithStagedFocused_WhenComposingAt64x12_ThenOnlyTheStagedListShows()
    {
        var frame = Frame(MixedTree(), PaneKind.WorkingStaged, 64, 12);

        Assert.Equal(
            string.Join(
                '\n',
                "   1 repo",
                "╭─ Staged 2 ───────────────────────────────────────────────────╮",
                "│ M README.md                                                  │",
                "│ R old name.txt -> new name.txt                               │",
                "│                                                              │",
                "│                                                              │",
                "│                                                              │",
                "│                                                              │",
                "│                                                              │",
                "│                                                              │",
                "╰──────────────────────────────────────────────────────────────╯",
                " NORMAL  1/1  ? help  : commands  q quit  j/k move  <space>u un…"),
            frame);
    }

    [Fact]
    public void GivenACleanTree_WhenComposing_ThenTheUnstagedListSaysSo()
    {
        var frame = Frame(Tree(), PaneKind.WorkingUnstaged, 100, 20);

        Assert.Contains("Nothing to commit, working", frame, StringComparison.Ordinal);
        Assert.Contains("tree clean", frame, StringComparison.Ordinal);
        Assert.Contains("Unstaged 0", frame, StringComparison.Ordinal);
        Assert.Contains("Staged 0", frame, StringComparison.Ordinal);
        Assert.Contains(WorkingTreePaneRenderer.NothingStagedText, frame, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenOnlyStagedChanges_WhenComposing_ThenTheUnstagedListSaysThereAreNone()
    {
        var frame = Frame(Tree(StagedAdded("a.txt")), PaneKind.WorkingStaged, 100, 20);

        Assert.Contains(WorkingTreePaneRenderer.NoUnstagedText, frame, StringComparison.Ordinal);
        Assert.DoesNotContain("Nothing to commit", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheStatusIsNotLoaded_WhenComposing_ThenTheListsShowNoCountsAndALoadingNote()
    {
        var frame = Frame(null, PaneKind.WorkingUnstaged, 100, 20);

        Assert.Contains(WorkingTreePaneRenderer.LoadingText, frame, StringComparison.Ordinal);
        Assert.Contains("─ Unstaged ─", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenMixedChanges_WhenComposingWithoutUnicode_ThenTheFrameIsAscii()
    {
        var frame = Frame(MixedTree(), PaneKind.WorkingUnstaged, 90, 16, GraphGlyphSet.Ascii);

        Assert.Contains("+- Unstaged 3 ", frame, StringComparison.Ordinal);
        Assert.DoesNotContain('╭', frame);
        Assert.DoesNotContain('│', frame);
    }

    [Fact]
    public void GivenAPathWithBytesThatAreNotUtf8_WhenComposing_ThenTheFrameHasNoLoneSurrogates()
    {
        var frame = Frame(Tree(Modified($"bad{(char)0xDCE9}name.txt")), PaneKind.WorkingUnstaged, 100, 20);

        Assert.Contains("bad�name.txt", frame, StringComparison.Ordinal);
        Assert.DoesNotContain(frame, char.IsSurrogate);
    }

    [Fact]
    public void GivenAWidePath_WhenComposing_ThenTheRowKeepsItsColumns()
    {
        var tree = Tree(Modified("日本語のファイル名.txt"));

        var lines = ScreenComposer.ComposeWorkingTree(Model(), new WorkingTreeModel(tree, PaneKind.WorkingUnstaged), 90, 16, GraphGlyphSet.Unicode);

        Assert.All(lines, line => Assert.Equal(90, TextWidth.Of(line.Text)));
        Assert.Contains(lines, line => line.Text.Contains("日本語のファイル名.txt", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenTheCursorInStaged_WhenComposing_ThenOnlyThatListHasASelectedRow()
    {
        var tree = WithSelection(MixedTree(), WorkingTreeSection.Staged, 1);
        var model = new WorkingTreeModel(tree, PaneKind.WorkingStaged);

        var lines = ScreenComposer.ComposeWorkingTree(Model(PaneKind.WorkingStaged), model, 90, 16, GraphGlyphSet.Unicode);

        var selected = lines.Where(line => line.Segments.Any(segment => segment.Selection == SegmentSelection.Focused)).ToList();
        Assert.Single(selected);
        Assert.Contains("R old name.txt -> new name", selected[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheCursorBelowTheVisibleRows_WhenComposing_ThenTheListScrollsToShowIt()
    {
        var tree = Tree([.. Enumerable.Range(0, 40).Select(number => Modified($"file{number:D2}.txt"))]);
        tree = WithSelection(tree, WorkingTreeSection.Unstaged, 30);

        var frame = Frame(tree, PaneKind.WorkingUnstaged, 90, 16);

        Assert.Contains("file30.txt", frame, StringComparison.Ordinal);
        Assert.DoesNotContain("file00.txt", frame, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PaneKind.WorkingUnstaged, 0, 0)]
    [InlineData(PaneKind.WorkingUnstaged, 1, 1)]
    [InlineData(PaneKind.WorkingStaged, 5, 3)]
    [InlineData(PaneKind.WorkingUnstaged, 20, 6)]
    [InlineData(PaneKind.WorkingStaged, 40, 8)]
    [InlineData(PaneKind.WorkingUnstaged, 64, 12)]
    [InlineData(PaneKind.WorkingStaged, 79, 24)]
    [InlineData(PaneKind.WorkingUnstaged, 80, 24)]
    [InlineData(PaneKind.WorkingStaged, 80, 8)]
    [InlineData(PaneKind.WorkingUnstaged, 120, 36)]
    [InlineData(PaneKind.WorkingStaged, 200, 60)]
    public void GivenAnySize_WhenComposing_ThenNoLineIsWiderThanTheScreenAndNothingThrows(PaneKind focused, int width, int height)
    {
        var lines = ScreenComposer.ComposeWorkingTree(Model(focused), new WorkingTreeModel(MixedTree(), focused), width, height, GraphGlyphSet.Unicode);

        Assert.True(lines.Count <= Math.Max(height, 0));
        Assert.All(lines, line => Assert.True(TextWidth.Of(line.Text) <= width, $"{width}x{height}: '{line.Text}'"));
    }

    private static WorkingTreeState WithSelection(WorkingTreeState tree, WorkingTreeSection section, int index)
    {
        var list = section == WorkingTreeSection.Staged ? tree.Staged : tree.Unstaged;
        return tree with { Selection = new WorkingTreeSelection(section, list[index].Path, index) };
    }
}
