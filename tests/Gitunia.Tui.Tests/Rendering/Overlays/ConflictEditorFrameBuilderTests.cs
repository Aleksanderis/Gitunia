using System.Globalization;
using System.Text;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Keymap.Models;
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
    // Rows: 0 top, 1 two, 2 one, 3-4 the first conflict (ours 2 lines, theirs 1), 5 after, 6 the second conflict.
    private const string Text =
        "top\ntwo\none\n<<<<<<< HEAD\nours 1\nours 2\n=======\ntheirs 1\n>>>>>>> feature\nafter\n"
        + "<<<<<<< HEAD\nA\n=======\nB\n>>>>>>> feature\n";

    private const string LongPad = "long long long long long long long long long long long long long long long ";

    private const string LongText =
        "top\n<<<<<<< HEAD\n" + "ours 1 " + LongPad + "tail-end\n=======\n" + "theirs 1 " + LongPad + "tail-end\n>>>>>>> feature\n"
        + "common " + LongPad + "tail-end\n";

    private const string WordMarkText =
        "top\n<<<<<<< HEAD\n" + LongPad + "the red fox\n||||||| base\n" + LongPad + "the fox\n=======\n"
        + LongPad + "the blue fox\n>>>>>>> feature\nend\n";

    private const string Diff3Text =
        "top\n<<<<<<< HEAD\nthe red fox\n||||||| base\nthe fox\n=======\nthe blue fox\n>>>>>>> feature\nend\n";

    [Fact]
    public void GivenAWideScreen_WhenRendering_ThenTheFrameFillsTheScreenAboveTheStatusBar()
    {
        var frame = ConflictEditorFrameBuilder.Build(State(Text), 120, 35, GraphGlyphSet.Unicode);

        Assert.NotNull(frame);
        Assert.Equal(0, frame.Column);
        Assert.Equal(0, frame.Row);
        Assert.Equal(35, frame.Height);
        Assert.Equal(120, frame.Width);
        Assert.All(frame.Lines, line => Assert.Equal(120, TextWidth.Of(line.Text)));
        Assert.StartsWith("╭", frame.Lines[0].Text, StringComparison.Ordinal);
        Assert.StartsWith("╰", frame.Lines[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheFile_WhenRendering_ThenTheTopBorderNamesTheFileAndThePosition()
    {
        var frame = Build(State(Text));

        var title = frame.Lines[0].Text;
        Assert.Contains("Conflict: file.txt", title, StringComparison.Ordinal);
        Assert.Contains("conflict 1 of 2", title, StringComparison.Ordinal);
        Assert.Contains("0 resolved  2 unresolved", title, StringComparison.Ordinal);
        var picked = Build(State(Text).Take(ConflictPick.Ours));
        Assert.Contains("1 resolved  1 unresolved", picked.Lines[0].Text, StringComparison.Ordinal);
        var asking = Build(State(Text).Take(ConflictPick.Ours).AskDiscard());
        Assert.Contains("Esc again discards", asking.Lines[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAWideScreen_WhenRendering_ThenBaseOursAndTheirsAreSideBySideWithTheirLabels()
    {
        var frame = Build(State(Text));

        var header = frame.Lines[1].Text;
        Assert.True(IsBefore(header, "base", "ours  HEAD"));
        Assert.True(IsBefore(header, "ours", "theirs  feature"));
        var row = RowsContaining(frame, "ours 1")[0];
        Assert.Contains("theirs 1", row, StringComparison.Ordinal);
        Assert.True(IsBefore(row, "ours 1", "theirs 1"));
    }

    [Fact]
    public void GivenCommonText_WhenRendering_ThenTheSameLineSitsInEveryPaneOnTheSameRow()
    {
        var frame = Build(State(Text));

        var row = RowsContaining(frame, "top").First(line => line.Count(character => character == 't') >= 3);
        Assert.Equal(3, CountOf(row, "top"));
    }

    [Fact]
    public void GivenAShorterVersion_WhenRendering_ThenItsMissingRowIsBlank()
    {
        var frame = Build(State(Text));

        var row = RowsContaining(frame, "ours 2").First();
        Assert.DoesNotContain("theirs", row, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheWholeFile_WhenRendering_ThenTheResultHoldsEveryLineWithItsMarkers()
    {
        var frame = Build(State(Text), height: 40);

        var result = ResultLines(frame);
        Assert.Contains(result, line => line.Contains("top", StringComparison.Ordinal));
        Assert.Contains(result, line => line.Contains("<<<<<<< HEAD", StringComparison.Ordinal));
        Assert.Contains(result, line => line.Contains(">>>>>>> feature", StringComparison.Ordinal));
        Assert.Contains(result, line => line.Contains("after", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAPick_WhenRendering_ThenTheResultShowsTheTakenLinesAndOnlyTheTitleSaysWhereTheyCameFrom()
    {
        var frame = Build(State(Text).Take(ConflictPick.Theirs), height: 40);

        var result = ResultLines(frame);
        Assert.DoesNotContain(result, line => line.Contains("ours 1", StringComparison.Ordinal));
        Assert.Contains("taken from theirs", frame.Lines[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("taken", frame.Lines[1].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("taken", TextOfLineWith(frame, " result "), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ConflictPick.Ours, "✓", "·", "·")]
    [InlineData(ConflictPick.Theirs, "·", "✓", "·")]
    [InlineData(ConflictPick.Both, "✓", "✓", "·")]
    [InlineData(ConflictPick.Lines, "~", "~", "~")]
    public void GivenAPick_WhenRendering_ThenTheMarksOfTheConflictRowsSayWhichVersionWasTaken(
        ConflictPick pick,
        string oursMark,
        string theirsMark,
        string baseMark)
    {
        var state = State(Diff3Text);
        state = pick == ConflictPick.Lines ? state.ToggleVisual().TakeSelectedLines() : state.Take(pick);

        var frame = Build(state, height: 40);

        var row = LineWith(frame, "the red fox", "the blue fox");
        var marks = row.Segments
            .Where(segment => segment.Text is "✓" or "·" or "~" or "?")
            .Select(segment => segment.Text)
            .ToList();
        Assert.Equal([baseMark, oursMark, theirsMark], marks);
    }

    [Fact]
    public void GivenAnUnresolvedConflict_WhenRendering_ThenEveryVersionPaneMarksItsRowsUnresolved()
    {
        var frame = Build(State(Diff3Text), height: 40);

        var row = LineWith(frame, "the red fox", "the blue fox");
        Assert.Equal(3, row.Segments.Count(segment => segment.Text == "?" && segment.Role == SegmentRole.Error));
        Assert.Contains("unresolved, still holds its markers", frame.Lines[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnUnresolvedAndASettledConflict_WhenRendering_ThenTheirMarksAndColoursDiffer()
    {
        var frame = Build(State(Text).Take(ConflictPick.Ours).NextConflict(), height: 40);

        var settledRow = LineWith(frame, "ours 1", "✓");
        Assert.Contains(settledRow.Segments, segment => segment.Text == "✓" && segment.Role == SegmentRole.DiffAdded);
        var unresolvedRow = frame.Lines.First(line => line.Text.Contains("<<<<<<< HEAD", StringComparison.Ordinal));
        Assert.Contains(unresolvedRow.Segments, segment => segment.Text == "?" && segment.Role == SegmentRole.Error);
        Assert.Contains(
            unresolvedRow.Segments,
            segment => segment.Text.Contains("<<<<<<<", StringComparison.Ordinal) && segment.Role == SegmentRole.Error);
    }

    [Fact]
    public void GivenTheCurrentConflict_WhenRendering_ThenItsRowsCarryTheBarAndTheGroupTintAndOthersDoNot()
    {
        var frame = Build(State(Text).NextConflict());

        var current = LineWith(frame, "│?", "A");
        Assert.Contains(current.Segments, segment => segment.Role == SegmentRole.DiffGroupBar && segment.InGroup);
        var first = frame.Lines.First(line => line.Text.Contains("ours 1", StringComparison.Ordinal));
        Assert.DoesNotContain(first.Segments, segment => segment.InGroup);
    }

    [Fact]
    public void GivenTheCursor_WhenRendering_ThenItsRowIsSelectedInTheActivePaneAndDimInTheOthers()
    {
        var frame = Build(State(Text).FocusPane(ConflictPane.Theirs));

        var row = frame.Lines.First(line => line.Text.Contains("theirs 1", StringComparison.Ordinal));
        Assert.Contains(row.Segments, segment => segment.Selection == SegmentSelection.Focused);
        Assert.Contains(row.Segments, segment => segment.Selection == SegmentSelection.Dim);
    }

    [Fact]
    public void GivenAVisualSelection_WhenRendering_ThenTheSelectedRowsHaveTheRangeTintAndTheHeaderCountsThem()
    {
        var frame = Build(State(Text).ToggleVisual().MoveCursor(1));

        var rows = frame.Lines
            .Where(line => line.Segments.Any(segment => segment.Selection == SegmentSelection.Range))
            .ToList();
        Assert.Equal(2, rows.Count);
        Assert.Contains("VISUAL 2", frame.Lines[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenADiff3Conflict_WhenRendering_ThenTheChangedWordsAreMarkedAgainstTheBase()
    {
        var frame = Build(State(Diff3Text));

        var ours = frame.Lines.First(line => line.Text.Contains("the red fox", StringComparison.Ordinal));
        Assert.Contains(
            ours.Segments,
            segment => segment.Role == SegmentRole.DiffRemovedWord && segment.Text.Contains("red", StringComparison.Ordinal));
        var theirs = frame.Lines.First(line => line.Text.Contains("the blue fox", StringComparison.Ordinal));
        Assert.Contains(
            theirs.Segments,
            segment => segment.Role == SegmentRole.DiffAddedWord && segment.Text.Contains("blue", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAConflictWithoutABase_WhenRendering_ThenTheBasePaneSaysHowToGetOne()
    {
        var frame = Build(State(Text));

        Assert.Contains(frame.Lines, line => line.Text.Contains("no base: set diff3", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenTheBaseCursor_WhenRenderingNarrow_ThenTheBasePaneReplacesTheirsOnTwoPanes()
    {
        var versions = State(Text);

        var ours = ConflictEditorFrameBuilder.Build(versions, 80, 30, GraphGlyphSet.Unicode)!;
        var baseState = versions.FocusPane(ConflictPane.Base);
        var baseFirst = ConflictEditorFrameBuilder.Build(baseState, 80, 30, GraphGlyphSet.Unicode)!;

        Assert.Contains("theirs", ours.Lines[1].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("base", ours.Lines[1].Text, StringComparison.Ordinal);
        Assert.Contains("base", baseFirst.Lines[1].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("theirs", baseFirst.Lines[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAVeryNarrowScreen_WhenRendering_ThenOnePaneShowsAndTabChangesIt()
    {
        var versions = State(Text);

        var ours = ConflictEditorFrameBuilder.Build(versions, 50, 30, GraphGlyphSet.Unicode)!;
        var theirs = ConflictEditorFrameBuilder.Build(versions.NextPane(), 50, 30, GraphGlyphSet.Unicode)!;

        Assert.Contains("ours", ours.Lines[1].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("theirs", ours.Lines[1].Text, StringComparison.Ordinal);
        Assert.Contains("theirs", theirs.Lines[1].Text, StringComparison.Ordinal);
        Assert.All(ours.Lines, line => Assert.Equal(50, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenTheResultPane_WhenRendering_ThenItsHeaderIsActiveAndNamesTheMode()
    {
        var normal = Build(State(Text).FocusPane(ConflictPane.Result));
        var typing = Build(State(Text).BeginEdit());

        var header = normal.Lines.First(line => line.Text.Contains("result", StringComparison.Ordinal));
        Assert.Contains(header.Segments, segment => segment.Text == "result" && segment.Role == SegmentRole.ActiveTab);
        Assert.Contains("NORMAL", header.Text, StringComparison.Ordinal);
        Assert.Contains(
            "INSERT",
            typing.Lines.First(line => line.Text.Contains("result", StringComparison.Ordinal)).Text,
            StringComparison.Ordinal);
        Assert.Contains(typing.Lines, line => line.Segments.Any(segment => segment.Role == SegmentRole.Cursor));
    }

    [Fact]
    public void GivenTypedText_WhenRendering_ThenTheResultShowsIt()
    {
        var typed = State(Text).BeginEdit().TypeKey(new KeyEvent("Q")).EndEdit();

        var frame = Build(typed, height: 40);

        Assert.Contains(ResultLines(frame), line => line.Contains("Q<<<<<<< HEAD", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAnAsciiGlyphSet_WhenRendering_ThenEveryCharacterIsAscii()
    {
        var state = State(Text).Take(ConflictPick.Ours);

        var frame = ConflictEditorFrameBuilder.Build(state, 120, 35, GraphGlyphSet.Ascii)!;

        Assert.All(frame.Lines, line => Assert.True(line.Text.All(character => character < 128), line.Text));
        Assert.Contains(frame.Lines, line => line.Text.Contains('v', StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(39, 30)]
    [InlineData(120, 10)]
    [InlineData(0, 0)]
    [InlineData(10, 3)]
    public void GivenAScreenTooSmall_WhenRendering_ThenNothingIsDrawnAndNothingThrows(int width, int height)
    {
        Assert.Null(ConflictEditorFrameBuilder.Build(State(Text), width, height, GraphGlyphSet.Unicode));
        Assert.Null(ConflictEditorFrameBuilder.ChipAt(State(Text), 1, 1, width, height));
    }

    [Fact]
    public void GivenManySizesAndStates_WhenRendering_ThenEveryFrameHasTheScreensWidthAndHeight()
    {
        ConflictEditorState[] states =
        [
            State(Text),
            State(Text).ToggleVisual().MoveCursor(1),
            State(Text).FocusPane(ConflictPane.Result),
            State(Text).BeginEdit().TypeKey(new KeyEvent("é")),
            State(Text).FocusPane(ConflictPane.Base).Take(ConflictPick.Both),
        ];
        foreach (var state in states)
        {
            for (var width = 40; width <= 200; width += 17)
            {
                for (var height = 11; height <= 50; height += 7)
                {
                    var frame = ConflictEditorFrameBuilder.Build(state, width, height, GraphGlyphSet.Unicode);

                    Assert.NotNull(frame);
                    Assert.Equal(height, frame.Height);
                    Assert.All(frame.Lines, line => Assert.Equal(width, TextWidth.Of(line.Text)));
                }
            }
        }
    }

    [Fact]
    public void GivenWideCharacters_WhenRendering_ThenNoLineIsSplitOrOverlong()
    {
        var wide = "日本語のテキスト\n<<<<<<< HEAD\n日本語日本語日本語日本語日本語日本語\n=======\n😀😀😀😀😀😀😀😀😀😀\n>>>>>>> other\nend\n";

        var frame = ConflictEditorFrameBuilder.Build(State(wide), 60, 20, GraphGlyphSet.Unicode)!;

        Assert.All(frame.Lines, line => Assert.Equal(60, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenTheChips_WhenClickingThem_ThenTheirMouseViewIsReturned()
    {
        var state = State(Text);
        var layout = ConflictEditorFrameBuilder.LayoutFor(state, 120, 35)!;
        var chipLine = layout.FirstChipLine;

        var hits = Enumerable.Range(0, 120)
            .Select(column => ConflictEditorFrameBuilder.ChipAt(state, chipLine, column, 120, 35))
            .Where(view => view is not null)
            .Distinct()
            .ToList();

        Assert.Contains(DefaultMouseMap.ConflictNextButton, hits);
        Assert.Contains(DefaultMouseMap.ConflictOursButton, hits);
        Assert.Contains(DefaultMouseMap.ConflictTakeButton, hits);
        Assert.Null(ConflictEditorFrameBuilder.ChipAt(state, 1, 5, 120, 35));
    }

    [Fact]
    public void GivenAClickInTheVersionPanes_WhenLookingItUp_ThenThePaneAndAlignedRowAreReturned()
    {
        var state = State(Text);
        var layout = ConflictEditorFrameBuilder.LayoutFor(state, 120, 35)!;
        var line = ConflictEditorLayout.VersionStartLine + 4;

        var ours = ConflictEditorFrameBuilder.TargetAt(state, line, 1 + 39 + 5, 120, 35)!;
        var theirs = ConflictEditorFrameBuilder.TargetAt(state, line, 1 + 78 + 5, 120, 35)!;
        var baseTarget = ConflictEditorFrameBuilder.TargetAt(state, line, 1 + 5, 120, 35)!;

        Assert.Equal((ConflictTargetKind.VersionRow, ConflictPane.Ours, 4), (ours.Kind, ours.Pane, ours.Index));
        Assert.Equal((ConflictTargetKind.VersionRow, ConflictPane.Theirs, 4), (theirs.Kind, theirs.Pane, theirs.Index));
        Assert.Equal(ConflictPane.Base, baseTarget.Pane);
        Assert.True(layout.VersionRows > 4);
    }

    [Fact]
    public void GivenAScrolledPane_WhenLookingUpARow_ThenTheScrollIsAdded()
    {
        var state = State(ManyLines()).ClickVersionRow(ConflictPane.Ours, 3) with { TopScroll = 3 };

        var target = ConflictEditorFrameBuilder.TargetAt(state, ConflictEditorLayout.VersionStartLine, 50, 120, 35)!;

        Assert.Equal(3, target.Index);
    }

    [Fact]
    public void GivenAStaleScrollAfterAResize_WhenLookingUpARow_ThenTheRowIsTheOneDrawnUnderThePointer()
    {
        var state = ConflictEditorState.Create(new TabId(1), "file.txt", ConflictParser.Parse(ManyLines()).Parts!)
            .MoveToBottom();
        var stale = state with { TopScroll = 0 };
        var layout = ConflictEditorFrameBuilder.LayoutFor(stale, 120, 35)!;
        var drawn = stale.Follow(layout.VersionRows, layout.ResultRows);
        Assert.NotEqual(0, drawn.TopScroll);

        var target = ConflictEditorFrameBuilder.TargetAt(stale, ConflictEditorLayout.VersionStartLine, 50, 120, 35)!;

        Assert.Equal(drawn.TopScroll, target.Index);
    }

    [Fact]
    public void GivenAStateAndAWidth_WhenRendering_ThenANarrowTitleCutsTheStateBeforeTheCounts()
    {
        var title = Build(State(Text), width: 70).Lines[0].Text;

        Assert.Contains("0 resolved", title, StringComparison.Ordinal);
        var wide = Build(State(Text)).Lines[0].Text;
        Assert.True(IsBefore(wide, "0 resolved  2 unresolved", "still holds its markers"));
    }

    [Fact]
    public void GivenTheHeaders_WhenLookingThemUp_ThenEachNamesItsPane()
    {
        var state = State(Text);
        var layout = ConflictEditorFrameBuilder.LayoutFor(state, 120, 35)!;

        var oursHeader = ConflictEditorFrameBuilder.TargetAt(state, 1, 50, 120, 35)!;
        var resultHeader = ConflictEditorFrameBuilder.TargetAt(
            state,
            layout.ResultStartLine - 1,
            10,
            120,
            35)!;

        Assert.Equal((ConflictTargetKind.Header, ConflictPane.Ours), (oursHeader.Kind, oursHeader.Pane));
        Assert.Equal((ConflictTargetKind.Header, ConflictPane.Result), (resultHeader.Kind, resultHeader.Pane));
    }

    [Fact]
    public void GivenTheResult_WhenLookingUpALine_ThenTheResultScrollIsAdded()
    {
        var state = State(ManyLines()).ClickResultLine(2) with { ResultScroll = 2 };
        var layout = ConflictEditorFrameBuilder.LayoutFor(state, 120, 35)!;

        var target = ConflictEditorFrameBuilder.TargetAt(state, layout.ResultStartLine + 1, 30, 120, 35)!;

        Assert.Equal((ConflictTargetKind.ResultLine, 3), (target.Kind, target.Index));
    }

    [Fact]
    public void GivenTheBordersDividersAndRules_WhenLookingThemUp_ThenNothingIsHit()
    {
        var state = State(Text);
        var layout = ConflictEditorFrameBuilder.LayoutFor(state, 120, 35)!;
        var line = ConflictEditorLayout.VersionStartLine + 1;

        Assert.Null(ConflictEditorFrameBuilder.TargetAt(state, 0, 10, 120, 35));
        Assert.Null(ConflictEditorFrameBuilder.TargetAt(state, line, 0, 120, 35));
        Assert.Null(ConflictEditorFrameBuilder.TargetAt(state, line, 119, 120, 35));
        Assert.Null(ConflictEditorFrameBuilder.TargetAt(state, line, 1 + 38, 120, 35));
        Assert.Null(ConflictEditorFrameBuilder.TargetAt(state, layout.ResultStartLine - 2, 10, 120, 35));
    }

    [Fact]
    public void GivenAChip_WhenLookingItUp_ThenTheTargetIsTheChipWithItsView()
    {
        var state = State(Text);
        var layout = ConflictEditorFrameBuilder.LayoutFor(state, 120, 35)!;

        var hits = Enumerable.Range(0, 120)
            .Select(column => ConflictEditorFrameBuilder.TargetAt(state, layout.FirstChipLine, column, 120, 35))
            .Where(target => target is { Kind: ConflictTargetKind.Chip })
            .Select(target => target!.View)
            .ToList();

        Assert.Contains(DefaultMouseMap.ConflictUntakeButton, hits);
    }

    [Fact]
    public void GivenTwoVersionPanes_WhenLookingUpTheRightOne_ThenItIsTheTheirsPane()
    {
        var state = State(Text);

        var target = ConflictEditorFrameBuilder.TargetAt(state, ConflictEditorLayout.VersionStartLine, 75, 80, 35)!;

        Assert.Equal(ConflictPane.Theirs, target.Pane);
    }

    [Fact]
    public void GivenEachMode_WhenRendering_ThenTheChipsMatchWhatTheKeysCanDo()
    {
        var versions = ChipText(State(Text));
        var result = ChipText(State(Text).FocusPane(ConflictPane.Result));
        var visual = ChipText(State(Text).ToggleVisual());
        var typing = ChipText(State(Text).BeginEdit());

        Assert.Contains("<space>a take", versions, StringComparison.Ordinal);
        Assert.Contains("o ours", versions, StringComparison.Ordinal);
        Assert.DoesNotContain("o ours", result, StringComparison.Ordinal);
        Assert.Contains("i type", result, StringComparison.Ordinal);
        Assert.Contains("<space>u untake", result, StringComparison.Ordinal);
        Assert.Contains("<space>u untake", versions, StringComparison.Ordinal);
        Assert.Contains("e edit in result", versions, StringComparison.Ordinal);
        Assert.DoesNotContain("reset", versions + result, StringComparison.Ordinal);
        Assert.Contains("take lines", visual, StringComparison.Ordinal);
        Assert.Contains("<space>u untake lines", visual, StringComparison.Ordinal);
        Assert.Contains("Ctrl-g keep", typing, StringComparison.Ordinal);
        Assert.DoesNotContain("Enter save", typing, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAFileOfFiveThousandLines_WhenRenderingTheEnd_ThenOnlyTheVisibleRowsAreDrawn()
    {
        var builder = new StringBuilder();
        for (var index = 0; index < 100; index++)
        {
            for (var line = 0; line < 45; line++)
            {
                builder.Append(CultureInfo.InvariantCulture, $"common {index} line {line} with some text\n");
            }

            builder.Append(CultureInfo.InvariantCulture, $"<<<<<<< HEAD\nours {index} word\n");
            builder.Append(CultureInfo.InvariantCulture, $"=======\ntheirs {index} word\n>>>>>>> feature\n");
        }

        var state = State(builder.ToString()).MoveToBottom().Follow(20, 15);

        var frame = ConflictEditorFrameBuilder.Build(state, 160, 50, GraphGlyphSet.Unicode)!;

        Assert.Equal(50, frame.Height);
        Assert.Contains(frame.Lines, line => line.Text.Contains("ours 99 word", StringComparison.Ordinal));
        Assert.DoesNotContain(frame.Lines, line => line.Text.Contains("common 0 line 0", StringComparison.Ordinal));
        Assert.DoesNotContain(frame.Lines, line => line.Text.Contains("ours 50 word", StringComparison.Ordinal));
        Assert.Contains("conflict 100 of 100", frame.Lines[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenALongLine_WhenScrolledSideways_ThenAllThreePanesShowTheTextFromTheColumn()
    {
        var state = State(LongText);

        var start = Build(state);
        var scrolled = Build(state.ScrollSideways(ConflictEditorState.SidewaysStep * 8));

        var startPanes = VersionLines(start);
        var scrolledPanes = VersionLines(scrolled);
        Assert.DoesNotContain(startPanes, line => line.Contains("tail-end", StringComparison.Ordinal));
        Assert.Contains(scrolledPanes, line => CountOf(line, "tail-end") == 3);
        Assert.Contains(startPanes, line => line.Contains("ours 1", StringComparison.Ordinal));
        Assert.DoesNotContain(scrolledPanes, line => line.Contains("ours 1", StringComparison.Ordinal));
        Assert.All(scrolled.Lines, line => Assert.Equal(120, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenAScrolledPane_WhenRendering_ThenTheMarginAndTheLineNumberStay()
    {
        var scrolled = Build(State(LongText).ScrollSideways(ConflictEditorState.SidewaysStep * 8));

        var row = VersionLines(scrolled).First(line => CountOf(line, "tail-end") == 3);

        // After the frame's border: two margin columns, then the line number of the pane's own file.
        Assert.Matches(@"^.{2}\s*\d+ ", row[1..]);
    }

    [Fact]
    public void GivenAScrolledPane_WhenRendering_ThenTheHeadersSayWhichColumnTheTextStartsAt()
    {
        var state = State(LongText);

        Assert.DoesNotContain("col ", Build(state).Lines[1].Text, StringComparison.Ordinal);
        Assert.Contains("col 9", Build(state.ScrollSideways(ConflictEditorState.SidewaysStep)).Lines[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAWordMarkPartlyScrolledOut_WhenRendering_ThenTheMarkFollowsTheVisiblePart()
    {
        var state = State(WordMarkText).ScrollSideways(LongPad.Length + 5);

        var frame = Build(state);

        var marked = frame.Lines.SelectMany(line => line.Segments)
            .Where(segment => segment.Role is SegmentRole.DiffRemovedWord or SegmentRole.DiffAddedWord)
            .Select(segment => segment.Text)
            .ToList();
        Assert.Contains(marked, text => text.Contains("ed", StringComparison.Ordinal) && !text.Contains("red", StringComparison.Ordinal));
        Assert.Contains(marked, text => text.Contains("lue", StringComparison.Ordinal) && !text.Contains("blue", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAWideCharacterAtTheCut_WhenScrolled_ThenItsHalfBecomesABlankAndRowsKeepTheirWidth()
    {
        var state = State("ab\u4f60\u597dcd tail\n<<<<<<< HEAD\nA\n=======\nB\n>>>>>>> feature\n");

        var cutInside = Build(state.ScrollSideways(3));
        var cutBefore = Build(state.ScrollSideways(2));

        Assert.Contains(" \u597dcd tail", TextOfLineWith(cutInside, "cd tail"), StringComparison.Ordinal);
        Assert.Contains("\u4f60\u597dcd tail", TextOfLineWith(cutBefore, "cd tail"), StringComparison.Ordinal);
        Assert.All(cutInside.Lines, line => Assert.Equal(120, TextWidth.Of(line.Text)));
        Assert.All(cutBefore.Lines, line => Assert.Equal(120, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenAScrolledLineShorterThanTheScroll_WhenRendering_ThenItsTextIsBlankNotCrashing()
    {
        var state = State(LongText).ScrollSideways(ConflictEditorState.SidewaysStep * 8);

        var frame = Build(state);

        Assert.DoesNotContain(VersionLines(frame), line => line.Contains("top", StringComparison.Ordinal));
        Assert.All(frame.Lines, line => Assert.Equal(120, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenTheScrollLimits_WhenScrollingFarOrBackPastTheStart_ThenItStopsAtTheWidestLineAndAtZero()
    {
        var state = State(LongText);

        var far = state.ScrollSideways(100000);
        var back = far.ScrollSideways(-100000);

        Assert.Equal(state.MaxHorizontalScroll, far.HorizontalScroll);
        Assert.True(far.HorizontalScroll > 0);
        Assert.Equal(0, back.HorizontalScroll);
        Assert.Same(state, state.ScrollSideways(-5));
    }

    [Fact]
    public void GivenTabsInTheWidestLine_WhenMeasuring_ThenEachTabCountsAsFourColumns()
    {
        var state = State("\t\tx\n<<<<<<< HEAD\nA\n=======\nB\n>>>>>>> feature\n");

        Assert.Equal(9, state.Alignment.WidestLineColumns);
    }

    [Fact]
    public void GivenTheVersionPanes_WhenListingChips_ThenScrollChipsAreOffered()
    {
        var text = ChipText(State(Text));

        Assert.Contains("zh scroll left", text, StringComparison.Ordinal);
        Assert.Contains("zl scroll right", text, StringComparison.Ordinal);
    }

    private static OverlayFrame Build(ConflictEditorState state, int width = 120, int height = 35) =>
        ConflictEditorFrameBuilder.Build(state, width, height, GraphGlyphSet.Unicode)!;

    private static List<string> RowsContaining(OverlayFrame frame, string text) =>
        [.. frame.Lines.Select(line => line.Text).Where(line => line.Contains(text, StringComparison.Ordinal))];

    private static bool IsBefore(string text, string first, string second) =>
        text.IndexOf(first, StringComparison.Ordinal) < text.IndexOf(second, StringComparison.Ordinal);

    private static StyledLine LineWith(OverlayFrame frame, string first, string second) =>
        frame.Lines.First(line =>
            line.Text.Contains(first, StringComparison.Ordinal) && line.Text.Contains(second, StringComparison.Ordinal));

    private static string TextOfLineWith(OverlayFrame frame, string text) =>
        frame.Lines.Select(line => line.Text).First(line => line.Contains(text, StringComparison.Ordinal));

    private static int CountOf(string row, string value) => row.Split(value).Length - 1;

    // The lines of the version panes: everything between the pane headers and the result header.
    private static List<string> VersionLines(OverlayFrame frame)
    {
        var lines = frame.Lines.Select(line => line.Text).ToList();
        var header = lines.FindIndex(line => line.Contains(" result ", StringComparison.Ordinal));

        return lines[2..header];
    }

    // The lines of the result pane: everything after the line that carries the result header.
    private static List<string> ResultLines(OverlayFrame frame)
    {
        var lines = frame.Lines.Select(line => line.Text).ToList();
        var header = lines.FindIndex(line => line.Contains(" result ", StringComparison.Ordinal));

        return lines[(header + 1)..];
    }

    private static string ChipText(ConflictEditorState state)
    {
        var layout = ConflictEditorFrameBuilder.LayoutFor(state, 200, 40)!;

        return string.Join(" ", layout.ChipRows.SelectMany(row => row).Select(chip => chip.Text));
    }

    private static string ManyLines() =>
        string.Concat(Enumerable.Range(0, 80).Select(line => $"line {line}\n"))
        + "<<<<<<< HEAD\nA\n=======\nB\n>>>>>>> feature\n";

    private static ConflictEditorState State(string text) =>
        ConflictEditorState.Create(new TabId(1), "file.txt", ConflictParser.Parse(text).Parts!);
}
