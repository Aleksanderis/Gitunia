using System.Diagnostics;
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
        Assert.True(header.IndexOf("base", StringComparison.Ordinal) < header.IndexOf("ours  HEAD", StringComparison.Ordinal));
        Assert.True(header.IndexOf("ours", StringComparison.Ordinal) < header.IndexOf("theirs  feature", StringComparison.Ordinal));
        var row = RowsContaining(frame, "ours 1")[0];
        Assert.Contains("theirs 1", row, StringComparison.Ordinal);
        Assert.True(row.IndexOf("ours 1", StringComparison.Ordinal) < row.IndexOf("theirs 1", StringComparison.Ordinal));
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
    public void GivenAPick_WhenRendering_ThenTheResultShowsTheTakenLinesAndTheHeaderSaysWhere()
    {
        var frame = Build(State(Text).Take(ConflictPick.Theirs), height: 40);

        var result = ResultLines(frame);
        Assert.DoesNotContain(result, line => line.Contains("<<<<<<< HEAD", StringComparison.Ordinal) && line.Contains("two", StringComparison.Ordinal));
        Assert.Contains("taken from theirs", frame.Lines.Select(line => line.Text).First(line => line.Contains("result", StringComparison.Ordinal)), StringComparison.Ordinal);
        var header = frame.Lines[1].Text;
        Assert.Contains("[taken]", header[header.IndexOf("theirs", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnUnresolvedAndASettledConflict_WhenRendering_ThenTheirMarksAndColoursDiffer()
    {
        var frame = Build(State(Text).Take(ConflictPick.Ours).NextConflict(), height: 40);

        var settledRow = frame.Lines.First(line => line.Text.Contains("ours 1", StringComparison.Ordinal) && line.Text.Contains('✓', StringComparison.Ordinal));
        Assert.Contains(settledRow.Segments, segment => segment.Text == "✓" && segment.Role == SegmentRole.DiffAdded);
        var unresolvedRow = frame.Lines.First(line => line.Text.Contains("<<<<<<< HEAD", StringComparison.Ordinal));
        Assert.Contains(unresolvedRow.Segments, segment => segment.Text == "?" && segment.Role == SegmentRole.Error);
        Assert.Contains(unresolvedRow.Segments, segment => segment.Text.Contains("<<<<<<<", StringComparison.Ordinal) && segment.Role == SegmentRole.Error);
    }

    [Fact]
    public void GivenTheCurrentConflict_WhenRendering_ThenItsRowsCarryTheBarAndTheGroupTintAndOthersDoNot()
    {
        var frame = Build(State(Text).NextConflict());

        var current = frame.Lines.First(line => line.Text.Contains("│?", StringComparison.Ordinal) && line.Text.Contains('A', StringComparison.Ordinal));
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

        var rows = frame.Lines.Where(line => line.Segments.Any(segment => segment.Selection == SegmentSelection.Range)).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Contains("VISUAL 2", frame.Lines[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenADiff3Conflict_WhenRendering_ThenTheChangedWordsAreMarkedAgainstTheBase()
    {
        var frame = Build(State(Diff3Text));

        var ours = frame.Lines.First(line => line.Text.Contains("the red fox", StringComparison.Ordinal));
        Assert.Contains(ours.Segments, segment => segment.Role == SegmentRole.DiffRemovedWord && segment.Text.Contains("red", StringComparison.Ordinal));
        var theirs = frame.Lines.First(line => line.Text.Contains("the blue fox", StringComparison.Ordinal));
        Assert.Contains(theirs.Segments, segment => segment.Role == SegmentRole.DiffAddedWord && segment.Text.Contains("blue", StringComparison.Ordinal));
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
        var baseFirst = ConflictEditorFrameBuilder.Build(versions.FocusPane(ConflictPane.Base), 80, 30, GraphGlyphSet.Unicode)!;

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
    public void GivenEachMode_WhenRendering_ThenTheChipsMatchWhatTheKeysCanDo()
    {
        var versions = ChipText(State(Text));
        var result = ChipText(State(Text).FocusPane(ConflictPane.Result));
        var visual = ChipText(State(Text).ToggleVisual());
        var typing = ChipText(State(Text).BeginEdit());

        Assert.Contains("<space>a take", versions, StringComparison.Ordinal);
        Assert.Contains("o ours", versions, StringComparison.Ordinal);
        Assert.DoesNotContain("o ours", result, StringComparison.Ordinal);
        Assert.Contains("i edit", result, StringComparison.Ordinal);
        Assert.Contains("take lines", visual, StringComparison.Ordinal);
        Assert.Contains("Ctrl-g keep", typing, StringComparison.Ordinal);
        Assert.DoesNotContain("Enter save", typing, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAFileOfFiveThousandLines_WhenRenderingAfterAScroll_ThenTheFrameIsFast()
    {
        var builder = new StringBuilder();
        for (var index = 0; index < 100; index++)
        {
            for (var line = 0; line < 45; line++)
            {
                builder.Append(CultureInfo.InvariantCulture, $"common {index} line {line} with some text\n");
            }

            builder.Append(CultureInfo.InvariantCulture, $"<<<<<<< HEAD\nours {index} word\n=======\ntheirs {index} word\n>>>>>>> feature\n");
        }

        var state = State(builder.ToString()).MoveToBottom().Follow(20, 15);
        _ = ConflictEditorFrameBuilder.Build(state, 160, 50, GraphGlyphSet.Unicode);
        var watch = Stopwatch.StartNew();
        for (var round = 0; round < 20; round++)
        {
            state = state.MoveCursor(-3).Follow(20, 15);
            Assert.NotNull(ConflictEditorFrameBuilder.Build(state, 160, 50, GraphGlyphSet.Unicode));
        }

        Assert.True(watch.ElapsedMilliseconds / 20 < 50, $"{watch.ElapsedMilliseconds / 20} ms per frame");
    }

    private static OverlayFrame Build(ConflictEditorState state, int width = 120, int height = 35) =>
        ConflictEditorFrameBuilder.Build(state, width, height, GraphGlyphSet.Unicode)!;

    private static List<string> RowsContaining(OverlayFrame frame, string text) =>
        [.. frame.Lines.Select(line => line.Text).Where(line => line.Contains(text, StringComparison.Ordinal))];

    private static int CountOf(string row, string value) => row.Split(value).Length - 1;

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

    private static ConflictEditorState State(string text) =>
        ConflictEditorState.Create(new TabId(1), "file.txt", ConflictParser.Parse(text).Parts!);
}
