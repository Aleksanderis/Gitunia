using Gitunia.App.Tests.Picker;
using Gitunia.Core.Input;
using Gitunia.Core.Keymap;
using Gitunia.Core.Overlays;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Xunit;

namespace Gitunia.App.Tests.Shell;

// A key through the real dispatcher and runner: the terminal layer only gets the handlers in order.
public class RouteActionEffectTests
{
    private static readonly InputFocus GraphFocus = new(OverlayKind.None, true);

    [Fact]
    public void GivenAnAction_WhenInvoked_ThenTheShellGetsItWithTheHandlersTheReducerChose()
    {
        using var pipeline = PickerPipeline.Create();

        pipeline.Dispatch(new ActionInvoked(ActionIds.ListDown, 4, GraphFocus));

        var routed = Assert.Single(pipeline.Shell.Routed);
        Assert.Equal(ActionIds.ListDown, routed.Action);
        Assert.Equal(4, routed.Count);
        Assert.Equal(InputRouting.Handlers(ActionIds.ListDown, pipeline.Host.State.Shell, GraphFocus), routed.Handlers);
    }

    [Fact]
    public void GivenAnActionThatChangesNoState_WhenInvoked_ThenNobodyIsToldTheStateChanged()
    {
        using var pipeline = PickerPipeline.Create();
        var changes = 0;
        pipeline.Host.Changed += () => changes++;

        pipeline.Dispatch(new ActionInvoked(ActionIds.ListDown, null, GraphFocus));

        Assert.Equal(0, changes);
    }

    [Fact]
    public void GivenAnyOtherCommand_WhenDispatched_ThenTheStateChangeIsStillAnnounced()
    {
        using var pipeline = PickerPipeline.Create();
        var changes = 0;
        pipeline.Host.Changed += () => changes++;

        pipeline.Dispatch(new StatusPostedCommand("hello", StatusSeverity.Info, pipeline.Host.Clock.GetUtcNow()));

        Assert.Equal(1, changes);
    }
}
