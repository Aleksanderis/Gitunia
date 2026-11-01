using Gitunia.Core.Input;
using Gitunia.Core.Keymap;
using Gitunia.Core.Overlays;
using Gitunia.Core.Tests.Fixtures;
using Xunit;

namespace Gitunia.Core.Tests.Input;

[Trait("Os", "Independent")]
public class InputReducerTests
{
    [Fact]
    public void GivenAnActionFromAKey_WhenReduced_ThenTheStateIsTheSameAndOneEffectNamesTheHandlersInOrder()
    {
        var focus = new InputFocus(OverlayKind.None, true);

        var update = AppReducer.Reduce(AppState.Empty, new ActionInvoked(ActionIds.ListDown, 3, focus));

        Assert.Same(AppState.Empty, update.State);
        var effect = Assert.IsType<RouteActionEffect>(Assert.Single(update.Effects));
        Assert.Equal(ActionIds.ListDown, effect.Action);
        Assert.Equal(3, effect.Count);
        Assert.Equal(InputRouting.Handlers(ActionIds.ListDown, AppState.Empty.Shell, focus), effect.Handlers);
    }

    [Fact]
    public void GivenAnOpenPalette_WhenSettingsAreRequested_ThenTheEffectOffersThemToNobody()
    {
        var focus = new InputFocus(OverlayKind.Palette, true);

        var update = AppReducer.Reduce(AppState.Empty, new ActionInvoked(ActionIds.SettingsOpen, null, focus));

        Assert.Empty(Assert.IsType<RouteActionEffect>(Assert.Single(update.Effects)).Handlers);
    }

    [Fact]
    public void GivenEveryInputCommand_WhenReduced_ThenNoneFallsThroughTheSwitch()
    {
        var commands = ConcreteSubtypes.InstancesOf<InputCommand>();
        Assert.NotEmpty(commands);

        foreach (var command in commands)
        {
            try
            {
                InputReducer.Reduce(AppState.Empty, command);
            }
            catch (UnhandledCommandException)
            {
                Assert.Fail($"{command.GetType().Name} has no case in InputReducer");
            }
            catch (Exception)
            {
                // The empty command was handled by its case and tripped over its empty fields.
            }
        }
    }
}
