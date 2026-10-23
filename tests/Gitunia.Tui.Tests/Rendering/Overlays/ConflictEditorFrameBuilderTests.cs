using Gitunia.Core.Conflicts;
using Gitunia.Core.Mouse;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Conflicts;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

[Trait("Os", "Independent")]
public class ConflictEditorFrameBuilderTests
{
    private const string Text =
        "top\ntwo\none\n<<<<<<< HEAD\nours 1\nours 2\n=======\ntheirs 1\n>>>>>>> feature\nafter\n"
        + "<<<<<<< HEAD\nA\n=======\nB\n>>>>>>> feature\n";

    [Fact]
    public void GivenAWideScreen_WhenRendering_ThenTheSidesAreNextToEachOtherWithLabelsAndTheResultBelow()
    {
        var frame = ConflictEditorFrameBuilder.Build(State(Text), 120, 30, GraphGlyphSet.Unicode);

        Assert.NotNull(frame);
        var lines = frame.Lines.Select(line => line.Text).ToList();
        Assert.Contains("Conflict: file.txt  hunk 1/2", lines[0], StringComparison.Ordinal);
        Assert.Contains("[1 -]", lines[1], StringComparison.Ordinal);
        Assert.Contains("2 -", lines[1], StringComparison.Ordinal);
        Assert.Contains("ours  HEAD", lines[2], StringComparison.Ordinal);
        Assert.Contains("theirs  feature", lines[2], StringComparison.Ordinal);
        var firstRow = lines[3];
        Assert.Contains("ours 1", firstRow, StringComparison.Ordinal);
        Assert.Contains("theirs 1", firstRow, StringComparison.Ordinal);
        var oursColumn = firstRow.IndexOf("ours 1", StringComparison.Ordinal);
        Assert.True(oursColumn < firstRow.IndexOf("theirs 1", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("result  unresolved", StringComparison.Ordinal));
        Assert.All(frame.Lines, line => Assert.Equal(frame.Width, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenAPick_WhenRendering_ThenTheResultShowsThePickedLinesBetweenItsContextAndTheStripMarksIt()
    {
        var state = State(Text).Pick(ConflictPick.Both);

        var frame = ConflictEditorFrameBuilder.Build(state, 120, 30, GraphGlyphSet.Unicode);

        var lines = frame!.Lines.Select(line => line.Text).ToList();
        Assert.Contains("[1 both]", lines[1], StringComparison.Ordinal);
        var header = lines.FindIndex(line => line.Contains("result  both sides", StringComparison.Ordinal));
        Assert.True(header > 0);
        Assert.Contains("two", lines[header + 1], StringComparison.Ordinal);
        Assert.Contains("one", lines[header + 2], StringComparison.Ordinal);
        Assert.Contains("ours 1", lines[header + 3], StringComparison.Ordinal);
        Assert.Contains("ours 2", lines[header + 4], StringComparison.Ordinal);
        Assert.Contains("theirs 1", lines[header + 5], StringComparison.Ordinal);
        Assert.Contains("after", lines[header + 6], StringComparison.Ordinal);
        Assert.DoesNotContain(lines, line => line.Contains("<<<<<<<", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenNoPick_WhenRendering_ThenTheResultShowsTheMarkersThatWillStayInTheFile()
    {
        var frame = ConflictEditorFrameBuilder.Build(State(Text), 120, 30, GraphGlyphSet.Unicode);

        var lines = frame!.Lines.Select(line => line.Text).ToList();
        var header = lines.FindIndex(line => line.Contains("result  unresolved", StringComparison.Ordinal));
        Assert.Contains("<<<<<<< HEAD", lines[header + 3], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenANarrowScreen_WhenRendering_ThenTheSidesAreStackedAboveTheResult()
    {
        var frame = ConflictEditorFrameBuilder.Build(State(Text), 70, 30, GraphGlyphSet.Unicode);

        Assert.NotNull(frame);
        var lines = frame.Lines.Select(line => line.Text).ToList();
        var ours = lines.FindIndex(line => line.Contains("ours  HEAD", StringComparison.Ordinal));
        var theirs = lines.FindIndex(line => line.Contains("theirs  feature", StringComparison.Ordinal));
        var result = lines.FindIndex(line => line.Contains("result  ", StringComparison.Ordinal));
        Assert.True(ours < theirs && theirs < result);
        Assert.All(frame.Lines, line => Assert.Equal(frame.Width, TextWidth.Of(line.Text)));
    }

    [Theory]
    [InlineData(17, 30)]
    [InlineData(120, 8)]
    [InlineData(70, 10)]
    public void GivenAScreenTooSmall_WhenRendering_ThenNothingIsDrawn(int width, int height)
    {
        Assert.Null(ConflictEditorFrameBuilder.Build(State(Text), width, height, GraphGlyphSet.Unicode));
    }

    [Theory]
    [InlineData(120, 14)]
    [InlineData(70, 20)]
    [InlineData(40, 40)]
    [InlineData(200, 60)]
    public void GivenSeveralSizes_WhenRendering_ThenEveryRowHasTheFrameWidthAndTheFrameFitsTheScreen(
        int width,
        int height)
    {
        var frame = ConflictEditorFrameBuilder.Build(State(Text), width, height, GraphGlyphSet.Ascii);

        Assert.NotNull(frame);
        Assert.All(frame.Lines, line => Assert.Equal(frame.Width, TextWidth.Of(line.Text)));
        Assert.True(frame.Height + frame.Row <= height);
        Assert.True(frame.Width + frame.Column <= width);
    }

    [Fact]
    public void GivenLongAndWideText_WhenRendering_ThenRowsAreCutWithoutSplittingACharacter()
    {
        var text = $"<<<<<<< a\n{new string('漢', 80)}\n=======\n{new string('x', 300)}\t\n>>>>>>> b\n";

        var frame = ConflictEditorFrameBuilder.Build(State(text), 80, 30, GraphGlyphSet.Unicode);

        Assert.All(frame!.Lines, line => Assert.Equal(frame.Width, TextWidth.Of(line.Text)));
        Assert.DoesNotContain(frame.Lines, line => line.Text.Contains('\t', StringComparison.Ordinal));
    }

    [Fact]
    public void GivenMoreLinesThanRows_WhenRendering_ThenTheLastRowCountsTheHiddenOnes()
    {
        var many = string.Concat(Enumerable.Range(1, 40).Select(number => $"line {number}\n"));
        var text = $"<<<<<<< a\n{many}=======\nt\n>>>>>>> b\n";

        var frame = ConflictEditorFrameBuilder.Build(State(text), 120, 20, GraphGlyphSet.Unicode);

        Assert.Contains(frame!.Lines, line => line.Text.Contains("more lines", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAnEmptySide_WhenRendering_ThenItSaysNothing()
    {
        var frame = ConflictEditorFrameBuilder.Build(
            State("<<<<<<< a\n=======\nt\n>>>>>>> b\n"),
            120,
            30,
            GraphGlyphSet.Unicode);

        Assert.Contains(frame!.Lines, line => line.Text.Contains("(nothing)", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAPendingDiscardOrACompleteFile_WhenRendering_ThenTheFooterSaysSo()
    {
        var pending = State(Text).Pick(ConflictPick.Ours).AskDiscard();
        var complete = State(Text).Pick(ConflictPick.Ours).Next().Pick(ConflictPick.Theirs);

        var pendingFrame = ConflictEditorFrameBuilder.Build(pending, 120, 30, GraphGlyphSet.Unicode);
        var completeFrame = ConflictEditorFrameBuilder.Build(complete, 120, 30, GraphGlyphSet.Unicode);

        Assert.Contains("Esc again discards", pendingFrame!.Lines[^2].Text, StringComparison.Ordinal);
        Assert.Contains("every hunk is resolved", completeFrame!.Lines[^2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheChipRow_WhenHitTesting_ThenEachChipReturnsItsMouseViewAndOtherCellsNone()
    {
        var frame = ConflictEditorFrameBuilder.Build(State(Text), 120, 30, GraphGlyphSet.Unicode)!;
        var chipLine = frame.Lines.ToList().FindIndex(line => line.Text.Contains("[o ours]", StringComparison.Ordinal));
        var text = frame.Lines[chipLine].Text;

        Assert.Equal(
            DefaultMouseMap.ConflictOursButton,
            ConflictEditorFrameBuilder.ChipAt(chipLine, text.IndexOf("[o ours]", StringComparison.Ordinal), 120, 30));
        var saveColumn = text.IndexOf("[Enter save]", StringComparison.Ordinal) + 3;
        Assert.Equal(
            DefaultMouseMap.ConflictSaveButton,
            ConflictEditorFrameBuilder.ChipAt(chipLine, saveColumn, 120, 30));
        Assert.Null(ConflictEditorFrameBuilder.ChipAt(chipLine, 1, 120, 30));
        Assert.Null(ConflictEditorFrameBuilder.ChipAt(chipLine - 1, 4, 120, 30));
    }

    private static ConflictEditorState State(string text) =>
        ConflictEditorState.Create(new TabId(1), "file.txt", ConflictParser.Parse(text).Parts!);
}
