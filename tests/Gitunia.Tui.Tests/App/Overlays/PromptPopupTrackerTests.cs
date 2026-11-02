using Gitunia.Core.Overlays;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.Input;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Overlays;

[Trait("Os", "Independent")]
public class PromptPopupTrackerTests : IPromptDisplay
{
    private PromptPopup? _popup;
    private readonly FakePaneChrome _chrome = new(new ScreenSize(120, 36));
    private int _shows;
    private bool _overlayOpen;
    private PromptPopupTracker? _tracker;

    [Fact]
    public void GivenNoPopup_WhenSyncing_ThenNothingIsDrawn()
    {
        var tracker = Tracker();

        tracker.Sync();

        Assert.Equal(0, _shows);
    }

    [Fact]
    public void GivenAPopupOpens_WhenSyncing_ThenItIsDrawnOnceUntilItChanges()
    {
        var tracker = Tracker();
        _popup = PromptPopup.ForText("T", "C", "a", "ok");

        tracker.Sync();
        tracker.Sync();
        _popup = PromptPopup.ForText("T", "C", "ab", "ok");
        tracker.Sync();

        Assert.Equal(2, _shows);
    }

    [Fact]
    public void GivenAShownPopup_WhenTheScreenIsResized_ThenItIsDrawnAgain()
    {
        var tracker = Tracker();
        _popup = PromptPopup.ForText("T", "C", "a", "ok");
        tracker.Sync();

        _chrome.ScreenSize = new ScreenSize(50, 20);
        tracker.Sync();
        tracker.Sync();

        Assert.Equal(2, _shows);
    }

    [Fact]
    public void GivenAnotherOverlayIsShown_WhenTheScreenIsResized_ThenItIsDrawnAgain()
    {
        var tracker = Tracker();
        _overlayOpen = true;
        tracker.MarkShown(true);

        _chrome.ScreenSize = new ScreenSize(50, 20);
        tracker.Sync();
        tracker.Sync();

        Assert.Equal(1, _shows);
    }

    [Fact]
    public void GivenNoOverlayIsShown_WhenTheScreenIsResized_ThenNothingIsDrawn()
    {
        var tracker = Tracker();
        tracker.MarkShown(false);

        _chrome.ScreenSize = new ScreenSize(50, 20);
        tracker.Sync();

        Assert.Equal(0, _shows);
    }

    [Fact]
    public void GivenAShownPopup_WhenItCloses_ThenTheLayerIsRedrawn()
    {
        var tracker = Tracker();
        _popup = PromptPopup.ForText("T", "C", "a", "ok");
        tracker.Sync();

        _popup = null;
        tracker.Sync();

        Assert.Equal(2, _shows);
    }

    string IPromptDisplay.StatusLine => string.Empty;

    PromptPopup? IPromptDisplay.Popup => _popup;

    private PromptPopupTracker Tracker()
    {
        _tracker = new PromptPopupTracker(new PromptDisplays([this]), _chrome);
        _tracker.RedrawRequested += Show;

        return _tracker;
    }

    private void Show()
    {
        _shows++;
        _tracker!.MarkShown(_overlayOpen);
    }
}
