using Gitunia.Core.Diff;
using Gitunia.Core.Text;
using Gitunia.Core.Visual;
using Gitunia.Tui.Rendering.Diff;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Diff;

/// <summary>
/// Where the text of wrapped and side-by-side diff rows is on screen, for character-wise selection (#1167).
/// </summary>
[Trait("Os", "Independent")]
public class DiffTextGeometryTests
{
    private const int WrappedWidth = 30;
    private const int SplitWidth = 81;
    private const string LongText = "abcdefghijklmnopqrstuvwxyz0123456789";

    [Fact]
    public void GivenAWrappedRow_WhenThePointerIsOnItsSecondLine_ThenTheIndexContinuesAfterTheFirstLine()
    {
        var geometry = Wrapped(out var prefix, out var columns);

        Assert.Equal(new TextPosition(1, columns + 2), geometry.PositionAt(1, prefix + 2, null).Position);
        Assert.Equal(new TextPosition(1, columns), geometry.PositionAt(1, 0, null).Position);
        Assert.Equal(new TextPosition(0, 3), geometry.PositionAt(0, prefix + 3, null).Position);
    }

    [Fact]
    public void GivenAWrappedRow_WhenThePointerIsPastTheEndOfTheLastLine_ThenTheLastCharacterIsReturned()
    {
        var geometry = Wrapped(out _, out _);

        Assert.Equal(LongText.Length - 1, geometry.PositionAt(1, WrappedWidth - 1, null).Position.Column);
    }

    [Fact]
    public void GivenAWrappedRow_WhenAskingForTheLineOfAColumn_ThenTheLineHoldingItIsReturned()
    {
        var geometry = Wrapped(out _, out var columns);

        Assert.Equal(1, geometry.ItemShowing(new TextPosition(0, columns + 4), false));
        Assert.Equal(0, geometry.ItemShowing(new TextPosition(1, 3), false));
        Assert.Equal(new TextPosition(1, columns), geometry.StartOf(1, false));
    }

    [Fact]
    public void GivenAWrappedRow_WhenAStretchCrossesTheLineBreak_ThenEachLineGetsItsOwnColumns()
    {
        var geometry = Wrapped(out var prefix, out var columns);
        var span = new ColumnSpan(columns - 2, columns + 3);

        var first = geometry.ColumnsOf(0, false, span)!.Value;
        var second = geometry.ColumnsOf(1, false, span)!.Value;

        Assert.Equal(new DiffTextColumns(prefix + columns - 2, prefix + columns), first);
        Assert.Equal(new DiffTextColumns(prefix, prefix + 4), second);
        Assert.Null(geometry.ColumnsOf(2, false, span));
    }

    [Fact]
    public void GivenASelectionInsideOneWrappedRow_WhenConvertedToRows_ThenBothEndsAreInTheSameRow()
    {
        var geometry = Wrapped(out _, out var columns);
        var selection = new TextSelection(new TextPosition(1, columns + 5), new TextPosition(0, 3));

        var rows = geometry.ToRows(selection, false)!.Value;

        Assert.Equal(new TextPosition(0, columns + 5), rows.Anchor);
        Assert.Equal(new TextPosition(0, 3), rows.Cursor);
        Assert.Equal(0, rows.End.Line);
    }

    [Fact]
    public void GivenAWrappedRowWithATab_WhenMappingAColumn_ThenTheTabCountsForItsExpandedColumns()
    {
        var row = new DiffRow(DiffRowKind.Context, "\tabcdefghij", 1, 1, 0);
        var document = new DiffDocument([row], [], [], [["f"]], 1);
        var digits = DiffRowRenderer.GutterDigits(document.MaximumLineNumber);
        var prefix = DiffRowRenderer.PrefixWidth(row, digits);
        var map = DiffWrapMap.Build(document, prefix + 8);
        var geometry = new DiffTextGeometry(document, map, null, prefix + 8, digits);

        Assert.Equal(2, geometry.Count);
        Assert.Equal(0, geometry.PositionAt(0, prefix + 3, null).Position.Column);
        Assert.Equal(1, geometry.PositionAt(0, prefix + 4, null).Position.Column);
        Assert.Equal(5, geometry.PositionAt(1, prefix, null).Position.Column);
    }

    [Fact]
    public void GivenTheSideBySideView_WhenThePointerIsOnEitherHalf_ThenTheSideAndItsRowAreReturned()
    {
        var geometry = Split(out var document);
        var left = geometry.PositionAt(1, 10, null);
        var right = geometry.PositionAt(1, 60, null);

        Assert.True(left.IsOldSide);
        Assert.False(right.IsOldSide);
        Assert.Equal(1, geometry.RowOf(1, left.IsOldSide));
        Assert.Equal(3, geometry.RowOf(1, right.IsOldSide));
        Assert.Null(geometry.RowOf(2, false));
        Assert.Equal(document.Rows[2].Text, geometry.TextOf(geometry.RowOf(2, true)!.Value));
    }

    [Fact]
    public void GivenADragOnTheOldSide_WhenThePointerMovesOverTheNewHalf_ThenItStaysOnTheOldSide()
    {
        var geometry = Split(out _);

        var hit = geometry.PositionAt(1, 70, true);

        Assert.True(hit.IsOldSide);
        Assert.Equal(new TextPosition(1, 3), hit.Position);
    }

    [Fact]
    public void GivenTheNewHalf_WhenMarking_ThenTheColumnsStartAfterTheOldHalfAndItsOwnPrefix()
    {
        var geometry = Split(out var document);
        var digits = DiffRowRenderer.GutterDigits(document.MaximumLineNumber);
        var start = DiffSplitRenderer.LeftWidth(SplitWidth) + 1 + DiffSplitRenderer.SidePrefixWidth(digits);

        var columns = geometry.ColumnsOf(1, false, new ColumnSpan(1, 2))!.Value;

        Assert.Equal(new DiffTextColumns(start + 1, start + 3), columns);
    }

    [Fact]
    public void GivenASelectionOnTheOldSide_WhenCopied_ThenOnlyOldAndUnchangedRowsAreIncluded()
    {
        var geometry = Split(out var document);
        var selection = new TextSelection(new TextPosition(0, 0), new TextPosition(3, 1));

        Assert.Equal("ctx\nold1\nold2\nen", Copy(geometry, document, selection, true));
    }

    [Fact]
    public void GivenASelectionOnTheNewSide_WhenCopied_ThenOnlyNewAndUnchangedRowsAreIncluded()
    {
        var geometry = Split(out var document);
        var selection = new TextSelection(new TextPosition(0, 0), new TextPosition(3, 1));

        Assert.Equal("ctx\nnew1\nen", Copy(geometry, document, selection, false));
    }

    [Fact]
    public void GivenASelectionThatStartsOnALineWithNoRowOnTheSide_WhenCopied_ThenItStartsAtTheNextRow()
    {
        var geometry = Split(out var document);
        var selection = new TextSelection(new TextPosition(3, 1), new TextPosition(2, 1));

        Assert.Equal("en", Copy(geometry, document, selection, false));
    }

    [Fact]
    public void GivenASelectionOnlyOnLinesWithNoRowOnTheSide_WhenConverted_ThenNothingIsSelected()
    {
        var geometry = Split(out _);

        Assert.Null(geometry.ToRows(new TextSelection(new TextPosition(2, 0), new TextPosition(2, 2)), false));
    }

    [Fact]
    public void GivenAWrappedSideBySideLine_WhenMapping_ThenTheLongerSideDecidesTheLinesAndTheShorterHasNone()
    {
        var rows = new DiffRow[]
        {
            new(DiffRowKind.Removed, "short", 1, null, 0),
            new(DiffRowKind.Added, new string('x', 120), null, 1, 0),
        };
        var document = new DiffDocument(rows, [], [], [["f"]], 1);
        var digits = DiffRowRenderer.GutterDigits(document.MaximumLineNumber);
        var map = DiffSplitMap.Build(document, DiffSplitLayout.Build(document), SplitWidth, true);
        var geometry = new DiffTextGeometry(document, null, map, SplitWidth, digits);

        Assert.True(geometry.Count > 1);
        Assert.Equal(0, geometry.RowOf(0, true));
        Assert.NotNull(geometry.CellOf(0, true));
        Assert.Null(geometry.CellOf(1, true));
        Assert.NotNull(geometry.CellOf(1, false));
        Assert.Equal(1, geometry.RowOf(1, false));
    }

    private static string Copy(
        DiffTextGeometry geometry,
        DiffDocument document,
        TextSelection selection,
        bool isOldSide)
    {
        var rows = geometry.ToRows(selection, isOldSide)!.Value;

        return rows.TextIn(new DiffRowLines(document, geometry, isOldSide));
    }

    private static DiffTextGeometry Wrapped(out int prefix, out int columns)
    {
        var rows = new DiffRow[]
        {
            new(DiffRowKind.Context, LongText, 1, 1, 0),
            new(DiffRowKind.Context, "tail", 2, 2, 0),
        };
        var document = new DiffDocument(rows, [], [], [["f"]], 2);
        var digits = DiffRowRenderer.GutterDigits(document.MaximumLineNumber);
        prefix = DiffRowRenderer.PrefixWidth(rows[0], digits);
        columns = WrappedWidth - prefix;

        return new DiffTextGeometry(document, DiffWrapMap.Build(document, WrappedWidth), null, WrappedWidth, digits);
    }

    // Lines: [ctx | ctx], [old1 | new1], [old2 | -], [end | end].
    private static DiffTextGeometry Split(out DiffDocument document)
    {
        var rows = new DiffRow[]
        {
            new(DiffRowKind.Context, "ctx", 1, 1, 0),
            new(DiffRowKind.Removed, "old1", 2, null, 0),
            new(DiffRowKind.Removed, "old2", 3, null, 0),
            new(DiffRowKind.Added, "new1", null, 2, 0),
            new(DiffRowKind.Context, "end", 4, 3, 0),
        };
        document = new DiffDocument(rows, [], [], [["f"]], 4);
        var digits = DiffRowRenderer.GutterDigits(document.MaximumLineNumber);
        var map = DiffSplitMap.Build(document, DiffSplitLayout.Build(document), SplitWidth, false);

        return new DiffTextGeometry(document, null, map, SplitWidth, digits);
    }
}
