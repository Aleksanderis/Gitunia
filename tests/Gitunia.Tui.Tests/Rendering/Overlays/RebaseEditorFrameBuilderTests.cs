using Gitunia.Core.Mouse;
using Gitunia.Core.Rebase;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Rebase;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

public class RebaseEditorFrameBuilderTests
{
    [Fact]
    public void GivenActions_WhenRenderingAt80x24_ThenEachRowShowsActionHashAndSubjectAndTheHighlightIsMarked()
    {
        var state = State(3).MoveTo(1).SetAction(RebaseTodoAction.Squash).MoveTo(2).SetAction(RebaseTodoAction.Drop);

        var frame = RebaseEditorFrameBuilder.Build(state.MoveTo(1), 80, 24, GraphGlyphSet.Unicode);

        Assert.NotNull(frame);
        var lines = frame.Lines.Select(line => line.Text).ToList();
        Assert.Contains("Interactive rebase: topic onto main", lines[0], StringComparison.Ordinal);
        Assert.Contains("oldest first", lines[1], StringComparison.Ordinal);
        Assert.Contains("pick   1111111 subject 0", lines[2], StringComparison.Ordinal);
        Assert.Contains("squash 2222222 subject 1", lines[3], StringComparison.Ordinal);
        Assert.Contains("drop   3333333 subject 2", lines[4], StringComparison.Ordinal);
        Assert.Contains("2/3  2 kept  1 dropped  edited", lines[^2], StringComparison.Ordinal);
        Assert.Single(frame.Lines, line => line.IsSelected);
        Assert.True(frame.Lines[3].IsSelected);
        Assert.All(frame.Lines, line => Assert.Equal(frame.Width, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenAReword_WhenRendering_ThenTheRowShowsTheNewSubject()
    {
        var state = State(2).BeginEdit("old");
        state.Edit!.Editor.SetText("brand new subject\n\nbody");

        var frame = RebaseEditorFrameBuilder.Build(state.SaveEdit(), 80, 24, GraphGlyphSet.Unicode);

        Assert.Contains("reword 1111111 brand new subject", frame!.Lines[2].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("body", frame.Lines[2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAMeldingFirstCommit_WhenRendering_ThenTheFooterNamesTheProblem()
    {
        var state = State(2).SetAction(RebaseTodoAction.Fixup);

        var frame = RebaseEditorFrameBuilder.Build(state, 100, 24, GraphGlyphSet.Unicode);

        Assert.Contains("cannot be a squash or a fixup", frame!.Lines[^2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAPendingDiscard_WhenRendering_ThenTheFooterAsksForASecondEsc()
    {
        var state = State(2).MoveRow(1).AskDiscard();

        var frame = RebaseEditorFrameBuilder.Build(state, 80, 24, GraphGlyphSet.Unicode);

        Assert.Contains("Esc again discards the changes", frame!.Lines[^2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheChipRows_WhenRendering_ThenBothRowsListTheirChipsAsButtons()
    {
        var frame = RebaseEditorFrameBuilder.Build(State(2), 80, 24, GraphGlyphSet.Unicode);

        var chipRows = frame!.Lines.Skip(frame.Height - 4).Take(2).ToList();
        const string firstRow = "[p pick]  [r reword]  [s squash]  [f fixup]  [d drop]";
        const string secondRow = "[K move up]  [J move down]  [Enter start]  [Esc cancel]";
        Assert.Contains(firstRow, chipRows[0].Text, StringComparison.Ordinal);
        Assert.Contains(secondRow, chipRows[1].Text, StringComparison.Ordinal);
        Assert.Contains(
            chipRows[0].Segments,
            segment => segment.Role == SegmentRole.Button && segment.Text == "[r reword]");
    }

    [Fact]
    public void GivenEachChip_WhenClickingItsColumn_ThenItsMouseViewIsReturnedAndTheGapsAreNone()
    {
        var state = State(2);
        var chipLine = 2 + RebaseEditorFrameBuilder.VisibleRows(state, 24);
        var frame = RebaseEditorFrameBuilder.Build(state, 80, 24, GraphGlyphSet.Unicode)!;
        var firstRow = frame.Lines[chipLine].Text;
        var secondRow = frame.Lines[chipLine + 1].Text;

        Assert.Equal(DefaultMouseMap.RebasePickButton, ChipAt(state, chipLine, firstRow, "[p pick]", 0));
        Assert.Equal(DefaultMouseMap.RebaseDropButton, ChipAt(state, chipLine, firstRow, "[d drop]", 3));
        Assert.Null(ChipAt(state, chipLine, firstRow, "[p pick]", "[p pick]".Length));
        Assert.Equal(DefaultMouseMap.RebaseCancelButton, ChipAt(state, chipLine + 1, secondRow, "[Esc cancel]", 0));
        Assert.Equal(DefaultMouseMap.RebaseStartButton, ChipAt(state, chipLine + 1, secondRow, "[Enter start]", 0));
        Assert.Null(RebaseEditorFrameBuilder.ChipAt(state, 1, 5, 24));
    }

    [Fact]
    public void GivenMoreCommitsThanRows_WhenTheSelectionIsFarDown_ThenTheWindowScrollsAndRowsMapBackToIndexes()
    {
        var state = State(60).MoveTo(40);

        var frame = RebaseEditorFrameBuilder.Build(state, 80, 14, GraphGlyphSet.Unicode);

        Assert.Equal(6, RebaseEditorFrameBuilder.VisibleRows(state, 14));
        var selectedLine = Array.FindIndex([.. frame!.Lines], line => line.IsSelected);
        Assert.Contains("subject 40", frame.Lines[selectedLine].Text, StringComparison.Ordinal);
        Assert.Equal(40, RebaseEditorFrameBuilder.EntryIndexAt(state, selectedLine, 14));
        Assert.Null(RebaseEditorFrameBuilder.EntryIndexAt(state, 0, 14));
        Assert.Null(RebaseEditorFrameBuilder.EntryIndexAt(state, 2 + 6, 14));
    }

    [Fact]
    public void GivenTinyScreens_WhenRendering_ThenNothingThrowsAndTheFrameIsNullBelowTheMinimum()
    {
        var state = State(5);

        Assert.Null(RebaseEditorFrameBuilder.Build(state, 80, 8, GraphGlyphSet.Unicode));
        Assert.Null(RebaseEditorFrameBuilder.Build(state, 17, 24, GraphGlyphSet.Unicode));
        Assert.NotNull(RebaseEditorFrameBuilder.Build(state, 20, 9, GraphGlyphSet.Ascii));
        Assert.Null(RebaseEditorFrameBuilder.Build(state.BeginEdit("x"), 80, 4, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenTheAsciiGlyphSet_WhenRendering_ThenEveryLineKeepsTheFrameWidth()
    {
        var frame = RebaseEditorFrameBuilder.Build(State(3), 40, 24, GraphGlyphSet.Ascii);

        Assert.All(frame!.Lines, line => Assert.Equal(frame.Width, TextWidth.Of(line.Text)));
        Assert.Equal(38, frame.Width);
    }

    [Fact]
    public void GivenWideCharactersInASubject_WhenRendering_ThenTheRowIsCutWithoutSplittingThem()
    {
        var commits = new[] { new RebaseTodoCommit(new string('a', 40), new string('漢', 60)) };
        var state = RebaseEditorState.Create(new TabId(1), "topic", "main", RebaseBase.OntoCommit("x"), "tip", commits);

        var frame = RebaseEditorFrameBuilder.Build(state, 40, 24, GraphGlyphSet.Unicode);

        Assert.All(frame!.Lines, line => Assert.Equal(frame.Width, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenTheMessageEditor_WhenRendering_ThenItShowsTheMessageWithACursorAndTheKeys()
    {
        var state = State(2).BeginEdit("subject line\n\nbody line");

        var frame = RebaseEditorFrameBuilder.Build(state, 80, 24, GraphGlyphSet.Unicode);

        var lines = frame!.Lines.Select(line => line.Text).ToList();
        Assert.Contains("Reword 1111111", lines[0], StringComparison.Ordinal);
        Assert.Contains("subject line", lines[1], StringComparison.Ordinal);
        Assert.Contains("body line", lines[3], StringComparison.Ordinal);
        Assert.Contains("Alt-Enter or Ctrl-g keep the message", lines[^2], StringComparison.Ordinal);
        Assert.Contains(frame.Lines.SelectMany(line => line.Segments), segment => segment.Role == SegmentRole.Cursor);
        Assert.Null(RebaseEditorFrameBuilder.ChipAt(state, 5, 5, 24));
        Assert.Null(RebaseEditorFrameBuilder.EntryIndexAt(state, 2, 24));
    }

    [Fact]
    public void GivenABlankMessage_WhenRenderingTheEditor_ThenTheFooterSaysARewordNeedsAMessage()
    {
        var state = State(2).BeginEdit(string.Empty);

        var frame = RebaseEditorFrameBuilder.Build(state, 80, 24, GraphGlyphSet.Unicode);

        Assert.Contains("A reword needs a message", frame!.Lines[^2].Text, StringComparison.Ordinal);
    }

    private static string? ChipAt(RebaseEditorState state, int line, string text, string chip, int offset) =>
        RebaseEditorFrameBuilder.ChipAt(state, line, text.IndexOf(chip, StringComparison.Ordinal) + offset, 24);

    private static RebaseEditorState State(int count) =>
        RebaseEditorState.Create(
            new TabId(1),
            "topic",
            "main",
            RebaseBase.OntoCommit("base"),
            "tip",
            [.. Enumerable.Range(0, count).Select(index =>
                new RebaseTodoCommit(new string((char)('1' + (index % 9)), 40), $"subject {index}"))]);
}
