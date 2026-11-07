using System.Drawing;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Views;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Views;

public sealed class WorkingTreeDividerDragTests
{
    private const int StartColumn = 41;
    private const int Row = 5;

    [Fact]
    public void GivenTheDividerOfTheChangesView_WhenItIsDraggedWithTheButtonHeld_ThenTheMainViewRaisesAListsEdgeDrag()
    {
        using var fixture = new MainViewFixture();
        var drags = new List<(PaneEdge Edge, int Column, int Row)>();
        fixture.View.EdgeDragged += (edge, column, row) => drags.Add((edge, column, row));
        fixture.Working.SetVisibility(true, false);
        var divider = fixture.Working.FrameViews.OfType<FrameStripView>().First();

        Send(divider, MouseFlags.LeftButtonPressed, StartColumn);
        Send(divider, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport, StartColumn + 3);
        Send(divider, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport, StartColumn + 6);
        Send(divider, MouseFlags.LeftButtonReleased, StartColumn + 6);

        Assert.NotEmpty(drags);
        Assert.All(drags, drag => Assert.Equal(PaneEdge.WorkingLists, drag.Edge));
        Assert.Equal((PaneEdge.WorkingLists, StartColumn + 6, Row), drags[^1]);
    }

    private static void Send(FrameStripView view, MouseFlags flags, int column) =>
        view.NewMouseEvent(new Mouse
        {
            Flags = flags,
            Position = new Point(0, 0),
            ScreenPosition = new Point(column, Row),
        });
}
