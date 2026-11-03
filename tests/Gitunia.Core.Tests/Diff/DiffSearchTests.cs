using Gitunia.Core.Diff;
using Gitunia.Core.Editing;
using Xunit;

namespace Gitunia.Core.Tests.Diff;

public class DiffSearchTests
{
    [Fact]
    public void GivenOnlyTheCaretMoves_WhenEditing_ThenMatchesAndTextStayAndTheCaretIsDrawnInside()
    {
        var document = Document("Alpha", "beta");
        var state = Typed(document, "alp");

        var moved = DiffSearch.Edit(state, "alp", 1, document).State;

        Assert.Equal(state.Matches, moved.Matches);
        Assert.Equal(1, moved.Caret);
        Assert.Equal("/a|lp  1/1", moved.Describe(0));
    }

    [Fact]
    public void GivenTypedText_WhenEditing_ThenMatchesIgnoreCaseAndStayHighlighted()
    {
        var document = Document("Alpha", "beta", "ALPHABET");

        var state = Typed(document, "alpha");

        Assert.Equal(DiffSearchPhase.Editing, state.Phase);
        Assert.Equal([0, 2], state.Matches);
        Assert.Equal("alpha", state.Highlight);
        Assert.Equal("/alpha_  -/2", state.Describe(1));
        Assert.Equal("/alpha_  1/2", state.Describe(0));
    }

    [Fact]
    public void GivenMatches_WhenAccepting_ThenTheFirstMatchAtOrAfterTheCursorIsRevealed()
    {
        var document = Document("x", "foo", "x", "foo");

        var update = DiffSearch.Accept(Typed(document, "foo"), document, row: 2);

        Assert.Equal(3, update.RevealRow);
        Assert.Equal(DiffSearchPhase.Active, update.State.Phase);
    }

    [Fact]
    public void GivenNoMatchAfterTheCursor_WhenAccepting_ThenTheSearchWrapsToTheFirstMatch()
    {
        var document = Document("foo", "x");

        Assert.Equal(0, DiffSearch.Accept(Typed(document, "foo"), document, row: 1).RevealRow);
    }

    [Fact]
    public void GivenNoMatch_WhenAccepting_ThenTheSearchEndsWithANotice()
    {
        var document = Document("a");

        var update = DiffSearch.Accept(Typed(document, "zzz"), document, row: 0);

        Assert.Null(update.RevealRow);
        Assert.False(update.State.IsOpen);
        Assert.Equal("No match: zzz", update.State.Notice);
    }

    [Fact]
    public void GivenAnEmptyQuery_WhenAccepting_ThenTheSearchEnds()
    {
        var document = Document("a");

        Assert.False(DiffSearch.Accept(Typed(document, string.Empty), document, row: 0).State.IsOpen);
    }

    [Fact]
    public void GivenAnAcceptedSearch_WhenStepping_ThenMatchesAreVisitedAndWrap()
    {
        var document = Document("foo", "x", "foo", "foo");
        var state = DiffSearch.Accept(Typed(document, "foo"), document, row: 0).State;

        var next = DiffSearch.Step(state, forward: true, count: 1, row: 0);
        var wrapped = DiffSearch.Step(state, forward: true, count: 1, row: 3);
        var backwards = DiffSearch.Step(state, forward: false, count: 1, row: 0);
        var counted = DiffSearch.Step(state, forward: true, count: 2, row: 0);

        Assert.Equal(2, next.RevealRow);
        Assert.Equal(0, wrapped.RevealRow);
        Assert.Equal("search wrapped", wrapped.State.Notice);
        Assert.Equal(3, backwards.RevealRow);
        Assert.Equal(3, counted.RevealRow);
    }

    [Fact]
    public void GivenNoAcceptedSearch_WhenStepping_ThenNothingMoves()
    {
        var update = DiffSearch.Step(DiffSearchState.Idle, forward: true, count: 1, row: 0);

        Assert.Null(update.RevealRow);
        Assert.Equal("No search", update.State.Notice);
    }

    [Fact]
    public void GivenAnOpenSearch_WhenCancelling_ThenItIsIdle()
    {
        Assert.Equal(DiffSearchState.Idle, DiffSearch.Cancel().State);
    }

    private static DiffDocument Document(params string[] texts) => new(
        [.. texts.Select(text => new DiffRow(DiffRowKind.Context, text, 1, 1, 0))],
        [],
        [],
        [],
        1);

    private static DiffSearchState Typed(DiffDocument document, string text) =>
        DiffSearch.Edit(DiffSearch.Open().State, text, PromptCaret.End, document).State;
}
