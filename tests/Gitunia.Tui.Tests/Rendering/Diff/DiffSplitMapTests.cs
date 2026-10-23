using Gitunia.Core.Diff;
using Gitunia.Tui.Rendering.Diff;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Diff;

[Trait("Os", "Independent")]
public class DiffSplitMapTests
{
    [Fact]
    public void GivenPairedRows_WhenMapping_ThenBothRowsStartOnTheSameLineAndTheLineNamesFirstAndLastRow()
    {
        var document = Document(
            new DiffRow(DiffRowKind.Context, "a", 1, 1, 0),
            new DiffRow(DiffRowKind.Removed, "b", 2, null, 0),
            new DiffRow(DiffRowKind.Added, "c", null, 2, 0),
            new DiffRow(DiffRowKind.Context, "d", 3, 3, 0));
        var map = DiffSplitMap.Build(document, DiffSplitLayout.Build(document), 100, false);

        Assert.Equal(3, map.Count);
        Assert.Equal([0, 1, 1, 2], [map.StartOf(0), map.StartOf(1), map.StartOf(2), map.StartOf(3)]);
        Assert.Equal([0, 1, 3], Enumerable.Range(0, 3).Select(map.RowOf));
        Assert.Equal([0, 2, 3], Enumerable.Range(0, 3).Select(map.LastRowOf));
    }

    [Fact]
    public void GivenWrappedText_WhenMapping_ThenALineTakesAsManyLinesAsItsLongerSideAndResizeRecomputes()
    {
        var document = Document(
            new DiffRow(DiffRowKind.Removed, new string('x', 120), 1, null, 0),
            new DiffRow(DiffRowKind.Added, "short", null, 1, 0),
            new DiffRow(DiffRowKind.Context, "tail", 2, 2, 0));
        var split = DiffSplitLayout.Build(document);

        var wide = DiffSplitMap.Build(document, split, 100, true);
        var narrow = wide.Resize(80);
        var unwrapped = DiffSplitMap.Build(document, split, 100, false);

        Assert.Equal(4, wide.Count);
        Assert.Equal(wide.Count - 1, wide.StartOf(2));
        Assert.True(narrow.Count > wide.Count);
        Assert.Equal(2, unwrapped.Count);
        Assert.Equal(0, wide.RowOf(2));
        Assert.Equal(2, wide.RowOf(3));
    }

    [Fact]
    public void GivenAnEmptyDocument_WhenMapping_ThenNothingThrows()
    {
        var map = DiffSplitMap.Build(DiffDocument.Empty, DiffSplitLayout.Build(DiffDocument.Empty), 100, true);

        Assert.Equal(0, map.Count);
        Assert.Equal(0, map.StartOf(5));
        Assert.Equal(0, map.RowOf(5));
    }

    private static DiffDocument Document(params DiffRow[] rows) => new(rows, [], [], [], 9);
}
