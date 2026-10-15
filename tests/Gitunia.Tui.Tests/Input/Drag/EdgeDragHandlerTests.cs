using Gitunia.Tui.Input.Drag;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Xunit;

namespace Gitunia.Tui.Tests.Input.Drag;

public class EdgeDragHandlerTests
{
    [Theory]
    [InlineData(MouseFlags.LeftButtonClicked)]
    [InlineData(MouseFlags.LeftButtonDoubleClicked)]
    [InlineData(MouseFlags.LeftButtonTripleClicked)]
    public void GivenAQuickBurstOfClicks_WhenHandling_ThenEveryClickIsReported(MouseFlags flags)
    {
        var handler = new EdgeDragHandler();
        var columns = new List<int>();
        handler.Clicked += columns.Add;
        using var view = new View();

        var consumed = handler.Handle(view, new Mouse { Flags = flags, Position = new System.Drawing.Point(7, 0), ScreenPosition = new System.Drawing.Point(7, 3) });

        Assert.True(consumed);
        Assert.Equal([7], columns);
    }
}
