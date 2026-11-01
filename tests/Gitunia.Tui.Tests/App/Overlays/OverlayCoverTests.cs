using Gitunia.Core.Keymap;
using Gitunia.Tui.App.Conflicts;
using Gitunia.App.Shell;
using Gitunia.Core.Overlays;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.App.Pick;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.App.Reflog;
using Gitunia.Tui.App.Settings;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Overlays;

[Trait("Os", "Independent")]
public sealed class OverlayCoverTests : IDisposable
{
    private readonly TestShell _shell = new();
    private readonly WhichKeyTimer _timer = new(TimeSpan.FromHours(1), action => action());
    private readonly MouseGate _gate = new();
    private readonly OverlayStore _store;
    private readonly OverlayCover _cover;

    public OverlayCoverTests()
    {
        var host = _shell.Host;
        _store = new OverlayStore(host, _shell.Statuses, _timer, new TestFocusedView(() => "Graph"));
        var settings = new SettingsScreenInput(host, GraphGlyphSet.Unicode);
        var others = new OtherOverlays(
            _store,
            new ReflogInput(host, new NoSelection(), GraphGlyphSet.Unicode),
            settings,
            new PickActions(host, new NoPicks()),
            new PromptDisplays([]));
        var editors = new EditorOverlays(
            new RebaseEditorInput(host, new NoSelection(), others, GraphGlyphSet.Unicode),
            new ConflictEditorInput(host, GraphGlyphSet.Unicode),
            settings);
        _cover = new OverlayCover(_store, others, editors, host, _gate);
    }

    public void Dispose() => _timer.Dispose();

    [Fact]
    public void GivenNoOverlay_WhenAsking_ThenNothingIsOpenAndTheMouseIsFree()
    {
        Assert.False(_cover.IsAnyOverlayOpen());
        Assert.False(_gate.IsSuppressed);
    }

    [Fact]
    public void GivenAConfirmDialogOpens_WhenTheStateChanges_ThenTheMouseGateClosesUntilItIsAnswered()
    {
        _shell.Host.Confirm(new ConfirmPrompt("Delete", ["Delete branch x?"], "Cannot be undone.", "Delete"), () => { });

        Assert.True(_cover.IsAnyOverlayOpen());
        Assert.True(_gate.IsSuppressed);
        Assert.Equal(DefaultKeymap.ConfirmView, _cover.ViewName());

        _shell.Decline();

        Assert.False(_cover.IsAnyOverlayOpen());
        Assert.False(_gate.IsSuppressed);
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
