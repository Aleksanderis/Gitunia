using System.Drawing;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Views.Lists;

public class PaneListViewTests
{
    [Fact]
    public void GivenAPaneThatIgnoresBlankDoubleClicks_WhenDoubleClickingBelowTheLastRow_ThenNoGestureIsRaised()
    {
        var gestures = DoubleClickAt(ignoresBlank: true, row: 5);

        Assert.Empty(gestures);
    }

    [Fact]
    public void GivenAPaneThatIgnoresBlankDoubleClicks_WhenDoubleClickingOnTheRow_ThenTheDoubleClickIsRaised()
    {
        var gestures = DoubleClickAt(ignoresBlank: true, row: 0);

        Assert.Equal([MouseGesture.DoubleClick], gestures);
    }

    [Fact]
    public void GivenAPaneThatKeepsBlankDoubleClicks_WhenDoubleClickingBelowTheLastRow_ThenTheDoubleClickIsRaised()
    {
        var gestures = DoubleClickAt(ignoresBlank: false, row: 5);

        Assert.Equal([MouseGesture.DoubleClick], gestures);
    }

    private static List<MouseGesture> DoubleClickAt(bool ignoresBlank, int row)
    {
        using var view = new PaneListView
        {
            Width = 20,
            Height = 10,
            IgnoresBlankDoubleClick = ignoresBlank,
            Source = new PlaceholderListSource("note", GraphGlyphSet.Unicode),
        };
        view.Layout();
        var gestures = new List<MouseGesture>();
        view.GestureRaised += (gesture, _) => gestures.Add(gesture);
        view.NewMouseEvent(new Mouse { Flags = MouseFlags.LeftButtonDoubleClicked, Position = new Point(3, row) });

        return gestures;
    }
}
