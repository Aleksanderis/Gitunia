using Gitunia.Core.Diff;
using Xunit;

namespace Gitunia.Core.Tests.Diff;

[Trait("Os", "Independent")]
public class DiffWordHighlightsTests
{
    [Fact]
    public void GivenARemovedRowFollowedByAnAddedRow_WhenAskingForChanges_ThenEachSideGetsItsWord()
    {
        var document = Document(Removed("let a = 1;"), Added("let a = 2;"));

        Assert.Equal(new ChangedSpan(8, 1), Assert.Single(DiffWordHighlights.ChangesOf(document, 0)));
        Assert.Equal(new ChangedSpan(8, 1), Assert.Single(DiffWordHighlights.ChangesOf(document, 1)));
    }

    [Fact]
    public void GivenTwoRemovedAndTwoAddedRows_WhenAskingForChanges_ThenTheyPairInOrder()
    {
        var document = Document(Removed("a one"), Removed("b two"), Added("a uno"), Added("b dos"));

        Assert.Equal(2, DiffWordHighlights.PartnerOf(document.Rows, 0));
        Assert.Equal(3, DiffWordHighlights.PartnerOf(document.Rows, 1));
        Assert.Equal(0, DiffWordHighlights.PartnerOf(document.Rows, 2));
        Assert.Equal(1, DiffWordHighlights.PartnerOf(document.Rows, 3));
        Assert.Equal(new ChangedSpan(2, 3), Assert.Single(DiffWordHighlights.ChangesOf(document, 1)));
    }

    [Fact]
    public void GivenMoreRemovedThanAddedRows_WhenAskingForChanges_ThenTheExtraRowHasNoPartner()
    {
        var document = Document(Removed("a one"), Removed("b two"), Added("a uno"));

        Assert.Equal(-1, DiffWordHighlights.PartnerOf(document.Rows, 1));
        Assert.Empty(DiffWordHighlights.ChangesOf(document, 1));
    }

    [Fact]
    public void GivenAnAddedRowAfterContext_WhenAskingForChanges_ThenThereIsNoPartner()
    {
        var document = Document(Context("same"), Added("new line"));

        Assert.Empty(DiffWordHighlights.ChangesOf(document, 0));
        Assert.Empty(DiffWordHighlights.ChangesOf(document, 1));
    }

    [Fact]
    public void GivenANoNewlineMarkerBetweenTheRuns_WhenAskingForChanges_ThenTheRowsAreNotPaired()
    {
        var document = Document(
            Removed("a one"),
            new DiffRow(DiffRowKind.NoNewline, "No newline at end of file", null, null, 0),
            Added("a uno"));

        Assert.Empty(DiffWordHighlights.ChangesOf(document, 0));
        Assert.Empty(DiffWordHighlights.ChangesOf(document, 2));
    }

    [Fact]
    public void GivenARowOutOfRange_WhenAskingForChanges_ThenTheResultIsEmpty()
    {
        var document = Document(Removed("a"), Added("b"));

        Assert.Empty(DiffWordHighlights.ChangesOf(document, -1));
        Assert.Empty(DiffWordHighlights.ChangesOf(document, 2));
    }

    [Fact]
    public void GivenARunLongerThanTheLimit_WhenAskingForChanges_ThenItIsLeftAlone()
    {
        var rows = Enumerable.Range(0, DiffWordHighlights.MaximumRunLength + 10)
            .Select(index => Removed($"line {index}"))
            .Append(Added("line x"))
            .ToArray();
        var document = Document(rows);

        Assert.Empty(DiffWordHighlights.ChangesOf(document, DiffWordHighlights.MaximumRunLength + 5));
    }

    private static DiffRow Removed(string text) => new(DiffRowKind.Removed, text, 1, null, 0);

    private static DiffRow Added(string text) => new(DiffRowKind.Added, text, null, 1, 0);

    private static DiffRow Context(string text) => new(DiffRowKind.Context, text, 1, 1, 0);

    private static DiffDocument Document(params DiffRow[] rows) => new(rows, [], [], [], 20);
}
