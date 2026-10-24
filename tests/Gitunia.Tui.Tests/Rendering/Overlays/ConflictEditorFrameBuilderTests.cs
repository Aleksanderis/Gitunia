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
    [InlineData(21, 30)]
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
    public void GivenAWideScreen_WhenRendering_ThenARuleWithAJunctionSeparatesTheSidesFromTheResult()
    {
        var frame = ConflictEditorFrameBuilder.Build(State(Text), 120, 30, GraphGlyphSet.Unicode)!;

        var lines = frame.Lines.Select(line => line.Text).ToList();
        var header = lines.FindIndex(line => line.Contains("result  ", StringComparison.Ordinal));
        var rule = lines[header - 1];
        Assert.StartsWith("├─", rule, StringComparison.Ordinal);
        Assert.EndsWith("─┤", rule, StringComparison.Ordinal);
        Assert.Equal(lines[3].IndexOf('│', 1), rule.IndexOf('┴', StringComparison.Ordinal));
    }

    [Fact]
    public void GivenANarrowScreen_WhenRendering_ThenTheRuleHasNoJunction()
    {
        var frame = ConflictEditorFrameBuilder.Build(State(Text), 70, 30, GraphGlyphSet.Ascii)!;

        var lines = frame.Lines.Select(line => line.Text).ToList();
        var header = lines.FindIndex(line => line.Contains("result  ", StringComparison.Ordinal));
        Assert.Single(lines[header - 1][1..^1].Distinct());
    }

    [Fact]
    public void GivenAHunk_WhenRendering_ThenEveryPaneHasLineNumbersOfTheResultFile()
    {
        var frame = ConflictEditorFrameBuilder.Build(State(Text), 120, 30, GraphGlyphSet.Unicode)!;

        var lines = frame.Lines.Select(line => line.Text).ToList();
        Assert.Contains(" 4-ours 1", lines[3], StringComparison.Ordinal);
        Assert.Contains(" 4+theirs 1", lines[3], StringComparison.Ordinal);
        Assert.Contains(" 5-ours 2", lines[4], StringComparison.Ordinal);
        var header = lines.FindIndex(line => line.Contains("result  ", StringComparison.Ordinal));
        Assert.Contains(" 2 two", lines[header + 1], StringComparison.Ordinal);
        Assert.Contains(" 3 one", lines[header + 2], StringComparison.Ordinal);
        Assert.Contains(" 4 <<<<<<< HEAD", lines[header + 3], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAScrolledSide_WhenRendering_ThenBothSidesShowTheSameLinesAndTheHeadersSayWhichOnes()
    {
        var ours = string.Concat(Enumerable.Range(1, 30).Select(number => $"mine {number}\n"));
        var theirs = string.Concat(Enumerable.Range(1, 30).Select(number => $"yours {number}\n"));
        var state = State($"<<<<<<< a\n{ours}=======\n{theirs}>>>>>>> b\n");
        var maximum = ConflictEditorFrameBuilder.MaxSideScroll(state, 120, 24);
        var rows = ConflictEditorFrameBuilder.SideRowCount(state, 120, 24);

        var frame = ConflictEditorFrameBuilder.Build(state.ScrollSides(5, maximum), 120, 24, GraphGlyphSet.Unicode)!;

        Assert.True(maximum > 5);
        var lines = frame.Lines.Select(line => line.Text).ToList();
        Assert.Contains($"(lines 6-{5 + rows} of 30)", lines[2], StringComparison.Ordinal);
        Assert.Contains(" 6-mine 6", lines[3], StringComparison.Ordinal);
        Assert.Contains(" 6+yours 6", lines[3], StringComparison.Ordinal);
        Assert.DoesNotContain(
            lines.Skip(3).Take(rows),
            line => line.Contains("mine 1 ", StringComparison.Ordinal));
        Assert.All(frame.Lines, line => Assert.Equal(frame.Width, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenAScrollBeyondTheEnd_WhenRendering_ThenTheLastLinesShowAndNoRowIsBlankAtTheTop()
    {
        var many = string.Concat(Enumerable.Range(1, 30).Select(number => $"mine {number}\n"));
        var state = State($"<<<<<<< a\n{many}=======\nt\n>>>>>>> b\n").ScrollSides(1000, 1000);

        var frame = ConflictEditorFrameBuilder.Build(state, 120, 24, GraphGlyphSet.Unicode)!;

        Assert.Contains(frame.Lines, line => line.Text.Contains("mine 30", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenTheKeyHints_WhenRendering_ThenTheKeyIsBoldAndTheDescriptionFollowsWithoutBrackets()
    {
        var frame = ConflictEditorFrameBuilder.Build(State(Text), 120, 30, GraphGlyphSet.Unicode)!;

        var line = frame.Lines[ChipLine(frame, "o ours")];
        Assert.DoesNotContain("[o ours]", line.Text, StringComparison.Ordinal);
        var keys = line.Segments.First(segment => segment.Text == "o");
        Assert.Equal(SegmentRole.StatusKey, keys.Role);
        Assert.Contains(line.Segments, segment => segment.Text == " ours" && segment.Role == SegmentRole.Status);
    }

    [Fact]
    public void GivenTheNavigationHints_WhenHitTesting_ThenEachReturnsItsMouseView()
    {
        var state = State(Text);
        var frame = ConflictEditorFrameBuilder.Build(state, 200, 30, GraphGlyphSet.Unicode)!;

        AssertChip(state, frame, "]c next diff", DefaultMouseMap.ConflictNextDiffButton, 200);
        AssertChip(state, frame, "[c previous diff", DefaultMouseMap.ConflictPreviousDiffButton, 200);
        AssertChip(state, frame, "Ctrl-d scroll down", DefaultMouseMap.ConflictScrollDownButton, 200);
        AssertChip(state, frame, "Ctrl-u scroll up", DefaultMouseMap.ConflictScrollUpButton, 200);
        AssertChip(state, frame, "n next conflict", DefaultMouseMap.ConflictNextButton, 200);
        AssertChip(state, frame, "N previous conflict", DefaultMouseMap.ConflictPreviousButton, 200);
    }

    [Fact]
    public void GivenLinesThatDifferAndLinesThatDoNot_WhenRendering_ThenOnlyTheDifferingOnesAreMarkedAndColoured()
    {
        const string text = "<<<<<<< a\nsame\nmine\n=======\nsame\nyours\n>>>>>>> b\n";

        var frame = ConflictEditorFrameBuilder.Build(State(text), 120, 30, GraphGlyphSet.Unicode)!;

        var lines = frame.Lines.Select(line => line.Text).ToList();
        Assert.Contains(" 1 same", lines[3], StringComparison.Ordinal);
        Assert.Contains(" 2-mine", lines[4], StringComparison.Ordinal);
        Assert.Contains(" 2+yours", lines[4], StringComparison.Ordinal);
        var segments = frame.Lines[4].Segments;
        Assert.Contains(segments, segment => segment.Text == "mine" && segment.Role == SegmentRole.DiffRemoved);
        Assert.Contains(segments, segment => segment.Text == "yours" && segment.Role == SegmentRole.DiffAdded);
        Assert.DoesNotContain(
            frame.Lines[3].Segments,
            segment => segment.Role is SegmentRole.DiffRemoved or SegmentRole.DiffAdded);
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
        var state = State(Text);
        var frame = ConflictEditorFrameBuilder.Build(state, 120, 30, GraphGlyphSet.Unicode)!;

        AssertChip(state, frame, "o ours", DefaultMouseMap.ConflictOursButton);
        AssertChip(state, frame, "Enter save", DefaultMouseMap.ConflictSaveButton);
        AssertChip(state, frame, "e edit", DefaultMouseMap.ConflictEditButton);
        AssertChip(state, frame, "r reset", DefaultMouseMap.ConflictResetButton);
        AssertChip(state, frame, "Ctrl-r redo", DefaultMouseMap.ConflictRedoButton);
        AssertChip(state, frame, "B base", DefaultMouseMap.ConflictBaseButton);
        AssertChip(state, frame, "R re-create", DefaultMouseMap.ConflictRecreateButton);
        var chipLine = ChipLine(frame, "o ours");
        Assert.Null(ConflictEditorFrameBuilder.ChipAt(state, chipLine, 1, 120, 30));
        Assert.Null(ConflictEditorFrameBuilder.ChipAt(state, chipLine - 1, 4, 120, 30));
    }

    [Fact]
    public void GivenAnEdit_WhenRendering_ThenTheResultShowsTheTextWithACursorAndOnlyTheKeepChipIsLeft()
    {
        var state = State(Text).Pick(ConflictPick.Ours).BeginEdit();
        state.Edit!.Editor.PlaceCursor(0, 0);
        state.Edit.Editor.Insert("typed ");

        var frame = ConflictEditorFrameBuilder.Build(state, 120, 30, GraphGlyphSet.Unicode)!;

        var lines = frame.Lines.Select(line => line.Text).ToList();
        Assert.Contains(lines, line => line.Contains("result  editing", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("typed ours 1", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Esc or Alt-Enter keeps the text", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("o ours", StringComparison.Ordinal));
        AssertChip(state, frame, "Alt-Enter keep", DefaultMouseMap.ConflictEditDoneButton);
        Assert.Contains(
            frame.Lines.SelectMany(line => line.Segments),
            segment => segment.Role == SegmentRole.Cursor);
    }

    [Fact]
    public void GivenAnEditedHunk_WhenRendering_ThenTheStripAndTheResultHeaderSayEdited()
    {
        var state = State(Text).BeginEdit();
        state.Edit!.Editor.Insert("x");

        var frame = ConflictEditorFrameBuilder.Build(state.AcceptEdit(), 120, 30, GraphGlyphSet.Unicode)!;

        var lines = frame.Lines.Select(line => line.Text).ToList();
        Assert.Contains("[1 edited]", lines[1], StringComparison.Ordinal);
        Assert.Contains(lines, line => line.Contains("result  edited by you", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("<<<<<<<", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenTheBaseToggledOnADiffThreeFile_WhenRendering_ThenTheBaseLinesShowBetweenTheSidesAndTheResult()
    {
        const string diffThree = "<<<<<<< HEAD\nmine\n||||||| base\nancestor\n=======\ntheirs\n>>>>>>> feature\n";
        var state = State(diffThree).ToggleBase();

        var frame = ConflictEditorFrameBuilder.Build(state, 120, 30, GraphGlyphSet.Unicode)!;

        var lines = frame.Lines.Select(line => line.Text).ToList();
        var header = lines.FindIndex(line => line.Contains("base  common ancestor", StringComparison.Ordinal));
        Assert.True(header > 0);
        Assert.Contains("ancestor", lines[header + 1], StringComparison.Ordinal);
        Assert.True(header < lines.FindIndex(line => line.Contains("result  ", StringComparison.Ordinal)));
        Assert.All(frame.Lines, line => Assert.Equal(frame.Width, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenTheBaseToggledOnAMergeStyleFile_WhenRendering_ThenItSaysHowToGetABase()
    {
        var frame = ConflictEditorFrameBuilder.Build(State(Text).ToggleBase(), 120, 30, GraphGlyphSet.Unicode)!;

        Assert.Contains(
            frame.Lines,
            line => line.Text.Contains("merge.conflictStyle=diff3", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenNoRoomForTheBase_WhenRendering_ThenTheBaseIsLeftOutAndTheResultKeepsItsRow()
    {
        const string diffThree = "<<<<<<< HEAD\nmine\n||||||| base\nancestor\n=======\ntheirs\n>>>>>>> feature\n";
        var state = State(diffThree).ToggleBase();

        foreach (var height in new[] { 12, 13, 14, 16 })
        {
            var frame = ConflictEditorFrameBuilder.Build(state, 120, height, GraphGlyphSet.Unicode);

            if (frame is not null)
            {
                Assert.Contains(frame.Lines, line => line.Text.Contains("result  ", StringComparison.Ordinal));
                Assert.True(frame.Height <= height);
            }
        }
    }

    private static int ChipLine(OverlayFrame frame, string chip) =>
        frame.Lines.ToList().FindIndex(line => line.Text.Contains(chip, StringComparison.Ordinal));

    private static void AssertChip(
        ConflictEditorState state,
        OverlayFrame frame,
        string chip,
        string view,
        int screenWidth = 120)
    {
        var line = ChipLine(frame, chip);
        Assert.True(line >= 0, chip);
        var column = frame.Lines[line].Text.IndexOf(chip, StringComparison.Ordinal) + 1;
        Assert.Equal(view, ConflictEditorFrameBuilder.ChipAt(state, line, column, screenWidth, 30));
    }

    private static ConflictEditorState State(string text) =>
        ConflictEditorState.Create(new TabId(1), "file.txt", ConflictParser.Parse(text).Parts!);
}
