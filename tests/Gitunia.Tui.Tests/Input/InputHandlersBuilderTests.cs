using Gitunia.Core.Input;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Input;

[Trait("Os", "Independent")]
public class InputHandlersBuilderTests
{
    [Fact]
    public void GivenAnObjectThatTakesNeitherActionsNorKeys_WhenAdding_ThenItIsRefused()
    {
        var builder = new InputHandlersBuilder();

        Assert.Throws<ArgumentException>(() => builder.Add(InputHandler.Diff, new object()));
    }

    [Fact]
    public void GivenACaseWithoutAHandler_WhenBuilding_ThenTheTableFailsAtStartup()
    {
        var builder = new InputHandlersBuilder().Add(InputHandler.Diff, new Both());

        Assert.Throws<InvalidOperationException>(builder.Build);
    }

    [Fact]
    public void GivenATextCaseWithoutAHandler_WhenAskingForTheTextKeyRouter_ThenItFailsAtStartup()
    {
        var builder = new InputHandlersBuilder().Add(InputHandler.Diff, new Both());

        Assert.Throws<InvalidOperationException>(builder.TextKeys);
    }

    [Fact]
    public void GivenEveryCaseRegistered_WhenBuilding_ThenEachCaseFindsItsHandlerAndTheRouterAsksTextHandlersInOrder()
    {
        var asked = new List<InputHandler>();
        var builder = new InputHandlersBuilder();
        foreach (var handler in Enum.GetValues<InputHandler>())
        {
            builder.Add(handler, new Both(handler, asked));
        }

        var table = builder.Build();
        var router = builder.TextKeys();

        Assert.IsType<Both>(table.ActionsOf(InputHandler.Navigation));
        var taken = router.HandleTextKey(new KeyEvent("a", KeyModifiers.None));

        Assert.False(taken);
        Assert.Equal(InputRouting.TextKeyHandlers(), asked);
    }

    private sealed class Both(InputHandler handler = default, List<InputHandler>? asked = null)
        : IActionHandler, ITextKeyHandler
    {
        public bool TryExecute(ActionId action, int? count) => false;

        public bool HandleTextKey(KeyEvent key)
        {
            asked?.Add(handler);

            return false;
        }
    }
}
