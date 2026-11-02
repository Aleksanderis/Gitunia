using Gitunia.Tui.Views;
using Gitunia.App.Shell;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Overlays;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Conflicts;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Pick;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.App.Reflog;
using Gitunia.Tui.App.Settings;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Tests.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Overlays;

[Trait("Os", "Independent")]
public sealed class GestureRouterTests : IDisposable, IActionRoute
{
    private readonly TestShell _shell = new();
    private readonly WhichKeyTimer _timer = new(TimeSpan.FromHours(1), InlineUi.Instance);
    private readonly MainViewFixture _screen = new();
    private readonly OverlayClickGuard _guard = new();
    private readonly List<ActionId> _routed = [];
    private readonly GestureRouter _router;

    public GestureRouterTests()
    {
        var host = _shell.Host;
        var store = new OverlayStore(host, _shell.Statuses, _timer, new TestFocusedView(() => DefaultKeymap.GraphView));
        var settings = new SettingsScreenInput(host, GraphGlyphSet.Unicode);
        var others = new OtherOverlays(
            store,
            new ReflogInput(host, new NoSelection(), GraphGlyphSet.Unicode),
            settings,
            new PickActions(host, new NoPicks()),
            new PromptDisplays([]));
        var editors = new EditorOverlays(
            new RebaseEditorInput(host, new NoSelection(), others, GraphGlyphSet.Unicode),
            new ConflictEditorInput(host, GraphGlyphSet.Unicode),
            settings);
        var cover = new OverlayCover(store, others, editors, host, new MouseGate());
        var visual = new VisualRangeInput(host, _screen.Panes);
        _router = new GestureRouter(
            new MouseController(DefaultMouseMap.Create(), this, new MouseGate()),
            visual,
            new GestureServices(_guard, cover, editors, _screen.Panes));
    }

    public void Dispose()
    {
        _timer.Dispose();
        _screen.Dispose();
    }

    void IActionRoute.Route(ActionId action, int? count) => _routed.Add(action);

    [Fact]
    public void GivenNoOverlay_WhenDoubleClickingTheGraph_ThenItRunsTheDoubleClickAction()
    {
        _router.OnGestured(DefaultKeymap.GraphView, MouseGesture.DoubleClick, null);

        Assert.Equal([ActionIds.MenuOpenForRow], _routed);
    }

    [Fact]
    public void GivenAClickJustUsedAnOverlay_WhenDoubleClickingAPane_ThenTheGestureIsIgnored()
    {
        _guard.Mark();

        _router.OnGestured(DefaultKeymap.GraphView, MouseGesture.DoubleClick, null);

        Assert.Empty(_routed);
    }

    [Fact]
    public void GivenAnOpenConfirmDialog_WhenClickingAPaneBehindIt_ThenTheClickIsTheBackdropClickAndStartsTheDoubleClickGuard()
    {
        _shell.Host.Confirm(new ConfirmPrompt("Delete", ["Delete branch x?"], "Cannot be undone.", "Delete"), () => { });

        _router.OnGestured(DefaultKeymap.GraphView, MouseGesture.Click, null);

        Assert.True(_guard.IsGuarding);
        Assert.DoesNotContain(ActionIds.MenuOpenForRow, _routed);
    }

    private sealed class NoSelection : IBranchSelectionSource
    {
        public BranchSelection Current() => new(null, null, null);
    }

    private sealed class NoPicks : IPickSelectionSource
    {
        public PickSelection Current() => new(null, []);

        public void EndRange()
        {
        }
    }
}
