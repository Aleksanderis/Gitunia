using System.Drawing;
using Gitunia.Tui.App.Pick;
using Terminal.Gui.ViewBase;
using Xunit;

namespace Gitunia.Tui.Tests.App.Pick;

public class MainlineOverlayTrackerTests
{
    private bool _prompt = true;
    private bool _otherOpen;
    private int _refreshes;

    [Fact]
    public void GivenTheOverlayIsOpen_WhenTheWindowIsResized_ThenItIsLaidOutAgain()
    {
        using var window = new View();
        window.Width = Dim.Fill();
        window.Height = Dim.Fill();

        var tracker = Tracker();
        tracker.Attach(window);
        window.Layout(new Size(80, 24));
        tracker.Sync();
        Assert.Equal(1, _refreshes);

        window.Layout(new Size(60, 20));

        Assert.Equal(2, _refreshes);
    }

    [Fact]
    public void GivenNoPrompt_WhenTheWindowIsResized_ThenNothingIsShown()
    {
        using var window = new View();
        window.Width = Dim.Fill();
        window.Height = Dim.Fill();
        _prompt = false;

        var tracker = Tracker();
        tracker.Attach(window);
        tracker.Sync();

        window.Layout(new Size(80, 24));
        window.Layout(new Size(60, 20));

        Assert.Equal(0, _refreshes);
    }

    [Fact]
    public void GivenAnotherOverlayOnTop_WhenResized_ThenTheMainlineOverlayIsLeftAlone()
    {
        var tracker = Tracker();
        tracker.Sync();
        _otherOpen = true;

        tracker.OnResized();

        Assert.Equal(1, _refreshes);
    }

    [Fact]
    public void GivenThePromptClosed_WhenSynced_ThenTheOverlayIsRefreshedOnceAndResizesStop()
    {
        var tracker = Tracker();
        tracker.Sync();
        _prompt = false;

        tracker.Sync();
        tracker.Sync();
        tracker.OnResized();

        Assert.Equal(2, _refreshes);
    }

    private MainlineOverlayTracker Tracker() => new(() => _prompt, () => _otherOpen, () => _refreshes++);
}
