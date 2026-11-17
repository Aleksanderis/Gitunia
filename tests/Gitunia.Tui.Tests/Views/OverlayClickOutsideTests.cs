using System.Drawing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Views;
using Gitunia.Tui.App.Overlays.Clicks;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Views;

// ux-mouse.md: a click outside an open overlay closes it, like Esc, wherever it lands (a pane, a title row, the banner,
// the status bar), and a click on a hint of the frame runs the action of its key.
public sealed class OverlayClickOutsideTests
{
    private const int StatusWidth = 80;
    private const int StatusHeight = 24;

    [Fact]
    public void GivenAnOpenOverlay_WhenATitleRowTheBannerOrTheStatusBarIsClicked_ThenEachRaisesTheBackdropClick()
    {
        using var fixture = new MainViewFixture();
        var gestures = new List<(string View, MouseGesture Gesture)>();
        fixture.View.Gestured += (view, gesture, _) => gestures.Add((view, gesture));
        fixture.Gate.Set(true);

        foreach (var line in fixture.Panes.Window.SubViews.OfType<LineView>())
        {
            line.NewMouseEvent(new Mouse
            {
                Flags = MouseFlags.LeftButtonClicked,
                Position = new Point(1, 0),
                ScreenPosition = new Point(1, 0),
            });
        }

        Assert.NotEmpty(gestures);
        Assert.All(gestures, gesture => Assert.Equal((DefaultMouseMap.BackdropView, MouseGesture.Click), gesture));
    }

    [Fact]
    public void GivenNoOverlay_WhenAPaneTitleRowIsClicked_ThenNoBackdropClickIsRaised()
    {
        using var fixture = new MainViewFixture();
        var gestures = new List<string>();
        fixture.View.Gestured += (view, _, _) => gestures.Add(view);

        foreach (var line in fixture.Panes.Window.SubViews.OfType<LineView>())
        {
            line.NewMouseEvent(new Mouse
            {
                Flags = MouseFlags.LeftButtonClicked,
                Position = new Point(1, 0),
                ScreenPosition = new Point(1, 0),
            });
        }

        Assert.DoesNotContain(DefaultMouseMap.BackdropView, gestures);
    }

    [Fact]
    public void GivenAFrameWithAClickableHint_WhenTheHintIsClicked_ThenTheClickRunsItsActionAndAClickElsewhereIsNotTaken()
    {
        using var fixture = new MainViewFixture();
        var frame = MainlineFrameBuilder.Build(
            "Revert merge",
            ["aaaaaaa main", "bbbbbbb topic"],
            100,
            24,
            GraphGlyphSet.Unicode,
            2)!;
        fixture.Panes.Overlay.Show(frame);
        var footer = frame.Lines.Count - 2;
        var column = frame.Lines[footer].Text.IndexOf("Esc", StringComparison.Ordinal);
        var target = new FrameClickTarget(fixture.Panes.Overlay);

        var hint = target.ClickAt(footer, column);
        var elsewhere = target.ClickAt(1, column);

        Assert.Equal(ActionIds.Cancel, hint?.Action);
        Assert.Null(elsewhere);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GivenAHintOfTheStatusBar_WhenItIsClicked_ThenTheViewRaisesItsActionAlsoWhileAnOverlayIsOpen(
        bool isOverlayOpen)
    {
        using var fixture = new MainViewFixture();
        var actions = new List<ActionId>();
        var backdrops = 0;
        fixture.View.HintClicked += actions.Add;
        fixture.View.Gestured += (view, _, _) => backdrops += view == DefaultMouseMap.BackdropView ? 1 : 0;
        fixture.Gate.Set(isOverlayOpen);
        fixture.Panes.Window.Width = StatusWidth;
        fixture.Panes.Window.Height = StatusHeight;
        fixture.Panes.Window.Layout();
        var status = fixture.Panes.Window.SubViews.OfType<LineView>().Last();

        var column = StatusColumnOfHelp();

        status.NewMouseEvent(new Mouse
        {
            Flags = MouseFlags.LeftButtonClicked,
            Position = new Point(column, 0),
            ScreenPosition = new Point(column, 0),
        });

        Assert.Equal([ActionIds.HelpShow], actions);
        Assert.Equal(0, backdrops);
    }

    private static int StatusColumnOfHelp()
    {
        var model = new ScreenModel("repository", null, 1, 1, "Normal", string.Empty, 0, 0, false, string.Empty);
        var text = string.Concat(
            ChromeBuilder.Status(model, StatusWidth, GraphGlyphSet.Unicode).Segments.Select(segment => segment.Text));

        return text.IndexOf("? help", StringComparison.Ordinal);
    }
}
