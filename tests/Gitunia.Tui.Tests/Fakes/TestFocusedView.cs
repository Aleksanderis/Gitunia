using Gitunia.Tui.Input;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>The focused keymap view, read from a test's own field on each ask.</summary>
/// <param name="name">Returns the view name of the moment.</param>
public sealed class TestFocusedView(Func<string> name) : IFocusedView
{
    public string Name => name();
}
