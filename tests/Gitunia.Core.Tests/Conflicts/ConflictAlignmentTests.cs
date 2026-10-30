using Gitunia.Core.Conflicts;
using Gitunia.Git.Conflicts;
using Xunit;

namespace Gitunia.Core.Tests.Conflicts;

[Trait("Os", "Independent")]
public class ConflictAlignmentTests
{
    // Rows: 0 top | 1-3 the conflict (ours 3 lines, theirs 2, base 1) | 4 end.
    private const string Text =
        "top\n<<<<<<< HEAD\na\nb\nc\n||||||| base\no\n=======\nx\ny\n>>>>>>> other\nend\n";

    [Fact]
    public void GivenAConflictWithVersionsOfDifferentLengths_WhenAligning_ThenTheShorterOnesAreFillerRows()
    {
        var alignment = Alignment(Text);

        Assert.Equal(5, alignment.RowCount);
        Assert.Equal(1, alignment.FirstRowOf(0));
        Assert.Equal(3, alignment.RowCountOf(0));
        Assert.Equal("c", alignment.TextAt(3, ConflictPane.Ours));
        Assert.Null(alignment.TextAt(3, ConflictPane.Theirs));
        Assert.Null(alignment.TextAt(2, ConflictPane.Base));
        Assert.Equal("o", alignment.TextAt(1, ConflictPane.Base));
        Assert.Equal("top", alignment.TextAt(0, ConflictPane.Base));
        Assert.Equal(-1, alignment.HunkAt(0));
        Assert.Equal(0, alignment.HunkAt(2));
    }

    [Fact]
    public void GivenEachVersion_WhenNumberingRows_ThenTheNumbersAreThoseOfThatVersionsFile()
    {
        var alignment = Alignment(Text);

        Assert.Equal(2, alignment.NumberAt(1, ConflictPane.Ours));
        Assert.Equal(4, alignment.NumberAt(3, ConflictPane.Ours));
        Assert.Equal(3, alignment.NumberAt(2, ConflictPane.Theirs));
        Assert.Null(alignment.NumberAt(3, ConflictPane.Theirs));
        Assert.Equal(4, alignment.NumberAt(4, ConflictPane.Theirs));
        Assert.Equal(5, alignment.NumberAt(4, ConflictPane.Ours));
        Assert.Equal(5, alignment.LastOursNumber);
        Assert.Equal(3, alignment.LastBaseNumber);
    }

    [Fact]
    public void GivenAConflictWithNoLinesAtAll_WhenAligning_ThenItStillTakesOneRowSoItCanBeSelected()
    {
        var alignment = Alignment("<<<<<<< HEAD\n=======\n>>>>>>> other\n");

        Assert.Equal(1, alignment.RowCount);
        Assert.Equal(0, alignment.HunkAt(0));
        Assert.Null(alignment.TextAt(0, ConflictPane.Ours));
    }

    [Fact]
    public void GivenRegionsInTheResult_WhenMappingRowsToLinesAndBack_ThenTheyAreBesideEachOther()
    {
        var alignment = Alignment(Text);
        ConflictRegion[] regions = [new(1, 11, ConflictPick.None)];

        Assert.Equal(0, alignment.ResultLineOf(0, regions, 13));
        Assert.Equal(1, alignment.ResultLineOf(1, regions, 13));
        Assert.Equal(3, alignment.ResultLineOf(3, regions, 13));
        Assert.Equal(12, alignment.ResultLineOf(4, regions, 13));
        Assert.Equal(0, alignment.RowOf(0, regions));
        Assert.Equal(3, alignment.RowOf(3, regions));
        Assert.Equal(3, alignment.RowOf(11, regions));
        Assert.Equal(4, alignment.RowOf(12, regions));
    }

    [Fact]
    public void GivenAResolvedRegionShorterThanItsRows_WhenMapping_ThenTheLastLineStandsForTheRest()
    {
        var alignment = Alignment(Text);
        ConflictRegion[] regions = [new(1, 1, ConflictPick.Ours)];

        Assert.Equal(1, alignment.ResultLineOf(3, regions, 3));
        Assert.Equal(2, alignment.ResultLineOf(4, regions, 3));
        Assert.Equal(1, alignment.RowOf(1, regions));
        Assert.Equal(4, alignment.RowOf(2, regions));
    }

    [Fact]
    public void GivenAnEmptiedRegion_WhenMapping_ThenRowsStillResolveAndNoLineBelongsToIt()
    {
        var alignment = Alignment(Text);
        ConflictRegion[] regions = [new(1, 0, ConflictPick.Edited)];

        Assert.Equal(1, alignment.ResultLineOf(2, regions, 2));
        Assert.Equal(4, alignment.RowOf(1, regions));
    }

    private static ConflictAlignment Alignment(string text) =>
        ConflictAlignment.Create(ConflictParser.Parse(text).Parts!);
}
