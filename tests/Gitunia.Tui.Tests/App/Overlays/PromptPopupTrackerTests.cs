using Gitunia.Core.Overlays;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Overlays;

[Trait("Os", "Independent")]
public class PromptPopupTrackerTests
{
    private PromptPopup? _popup;
    private ScreenSize _size = new(120, 36);
    private int _shows;
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

        _size = new ScreenSize(50, 20);
        tracker.Sync();
        tracker.Sync();

        Assert.Equal(2, _shows);
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

    private PromptPopupTracker Tracker()
    {
        _tracker = new PromptPopupTracker(() => _popup, () => _size, Show);
        return _tracker;
    }

    private void Show()
    {
        _shows++;
        _tracker!.MarkShown();
    }
}
