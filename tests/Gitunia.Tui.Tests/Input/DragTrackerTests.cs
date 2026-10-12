using Gitunia.Tui.Input;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Input;

public class DragTrackerTests
{
    private const MouseFlags Held = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport;

    [Fact]
    public void GivenAPressAndARelease_WhenFed_ThenItIsAClickNotADrag()
    {
        var tracker = new DragTracker();

        Assert.Equal(DragPhase.Press, tracker.Feed(MouseFlags.LeftButtonPressed, 5, 5));
        Assert.Equal(DragPhase.None, tracker.Feed(MouseFlags.LeftButtonReleased, 5, 5));
        Assert.Equal(DragPhase.None, tracker.Feed(MouseFlags.LeftButtonClicked, 5, 5));
    }

    [Fact]
    public void GivenAPositionReportOnTheSameCell_WhenFed_ThenTheDragHasNotStarted()
    {
        var tracker = new DragTracker();
        tracker.Feed(MouseFlags.LeftButtonPressed, 5, 5);

        Assert.Equal(DragPhase.None, tracker.Feed(Held, 5, 5));
    }

    [Fact]
    public void GivenTheButtonHeld_WhenThePointerMoves_ThenTheDragStartsOnceAndThenMoves()
    {
        var tracker = new DragTracker();
        tracker.Feed(MouseFlags.LeftButtonPressed, 5, 5);

        Assert.Equal(DragPhase.Start, tracker.Feed(Held, 5, 6));
        Assert.Equal(DragPhase.Move, tracker.Feed(Held, 5, 7));
        Assert.Equal(DragPhase.Move, tracker.Feed(Held, 5, 5));
        Assert.Equal((5, 5), tracker.Origin);
    }

    [Fact]
    public void GivenADrag_WhenTheButtonIsReleased_ThenTheDragEndsAndTheFollowingClickIsSwallowed()
    {
        var tracker = new DragTracker();
        tracker.Feed(MouseFlags.LeftButtonPressed, 5, 5);
        tracker.Feed(Held, 5, 8);

        Assert.Equal(DragPhase.End, tracker.Feed(MouseFlags.LeftButtonReleased, 5, 8));
        Assert.Equal(DragPhase.SwallowClick, tracker.Feed(MouseFlags.LeftButtonClicked, 5, 8));
        Assert.Equal(DragPhase.None, tracker.Feed(MouseFlags.LeftButtonClicked, 5, 8));
    }

    [Fact]
    public void GivenADragWhoseClickWentElsewhere_WhenPressingAgain_ThenTheNextClickCountsAgain()
    {
        var tracker = new DragTracker();
        tracker.Feed(MouseFlags.LeftButtonPressed, 5, 5);
        tracker.Feed(Held, 5, 8);
        tracker.Feed(MouseFlags.LeftButtonReleased, 5, 8);

        tracker.Feed(MouseFlags.LeftButtonPressed, 2, 2);
        tracker.Feed(MouseFlags.LeftButtonReleased, 2, 2);

        Assert.Equal(DragPhase.None, tracker.Feed(MouseFlags.LeftButtonClicked, 2, 2));
    }

    [Fact]
    public void GivenNoButtonPressed_WhenThePointerMoves_ThenNothingHappens()
    {
        var tracker = new DragTracker();

        Assert.Equal(DragPhase.None, tracker.Feed(MouseFlags.PositionReport, 3, 3));
        Assert.Equal(DragPhase.None, tracker.Feed(Held, 4, 4));
    }

    [Fact]
    public void GivenAWheel_WhenFed_ThenItIsNotADragEvent()
    {
        var tracker = new DragTracker();

        Assert.Equal(DragPhase.None, tracker.Feed(MouseFlags.WheeledDown, 3, 3));
    }
}
