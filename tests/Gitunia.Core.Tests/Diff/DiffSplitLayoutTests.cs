using Gitunia.Core.Diff;
using Xunit;

namespace Gitunia.Core.Tests.Diff;

public class DiffSplitLayoutTests
{
    [Fact]
    public void GivenARemovedRunAndALongerAddedRun_WhenArranging_ThenTheyPairUpAndTheRestStandsAlone()
    {
        var document = Document(
            Row(DiffRowKind.FileHeader),
            Row(DiffRowKind.HunkHeader),
            Row(DiffRowKind.Context),
            Row(DiffRowKind.Removed),
            Row(DiffRowKind.Removed),
            Row(DiffRowKind.Added),
            Row(DiffRowKind.Added),
            Row(DiffRowKind.Added),
            Row(DiffRowKind.Context));

        var split = DiffSplitLayout.Build(document);

        Assert.Equal(7, split.Lines.Count);
        Assert.Equal((3, 5), (split.Lines[3].LeftRow, split.Lines[3].RightRow));
        Assert.Equal((4, 6), (split.Lines[4].LeftRow, split.Lines[4].RightRow));
        Assert.Equal((-1, 7), (split.Lines[5].LeftRow, split.Lines[5].RightRow));
        Assert.Equal([0, 1, 2, 3, 4, 3, 4, 5, 6], split.LineOfRow);
    }

    [Fact]
    public void GivenHeadersAndContext_WhenArranging_ThenHeadersSpanBothSidesAndContextShowsOnBoth()
    {
        var document = Document(Row(DiffRowKind.FileHeader), Row(DiffRowKind.Note), Row(DiffRowKind.Context));

        var split = DiffSplitLayout.Build(document);

        Assert.True(split.Lines[0].IsFull);
        Assert.True(split.Lines[1].IsFull);
        Assert.False(split.Lines[2].IsFull);
        Assert.Equal((2, 2), (split.Lines[2].LeftRow, split.Lines[2].RightRow));
    }

    [Fact]
    public void GivenAnAddedRunWithoutRemovals_WhenArranging_ThenTheOldSideStaysEmpty()
    {
        var split = DiffSplitLayout.Build(Document(Row(DiffRowKind.Added), Row(DiffRowKind.Added)));

        Assert.All(split.Lines, line => Assert.Equal(-1, line.LeftRow));
        Assert.Equal([0, 1], split.Lines.Select(line => line.FirstRow));
    }

    [Fact]
    public void GivenAnEmptyDocument_WhenArranging_ThenThereAreNoLines()
    {
        var split = DiffSplitLayout.Build(DiffDocument.Empty);

        Assert.Empty(split.Lines);
        Assert.Empty(split.LineOfRow);
    }

    private static DiffRow Row(DiffRowKind kind) => new(kind, kind.ToString(), 1, 1, 0);

    private static DiffDocument Document(params DiffRow[] rows) => new(rows, [], [], [], 1);
}
