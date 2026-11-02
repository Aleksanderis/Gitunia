using Gitunia.Core.Input;

namespace Gitunia.App;

/// <summary>The routed key action.</summary>
public interface IActionRouteEffects
{
    /// <summary>Offers an action to its handlers in order (the terminal layer owns the lists they read).</summary>
    void RouteAction(RouteActionEffect effect);
}
