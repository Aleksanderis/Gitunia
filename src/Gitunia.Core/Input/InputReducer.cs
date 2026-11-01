namespace Gitunia.Core.Input;

/// <summary>Turns an input command into the handlers that may take it. The state does not change.</summary>
public static class InputReducer
{
    /// <summary>Applies a command.</summary>
    public static AppUpdate Reduce(AppState state, InputCommand command) => command switch
    {
        ActionInvoked invoked => Route(state, invoked),
        _ => throw new UnhandledCommandException(command),
    };

    private static AppUpdate Route(AppState state, ActionInvoked invoked)
    {
        var handlers = InputRouting.Handlers(invoked.Action, state.Shell, invoked.Focus);

        return new AppUpdate(state, [new RouteActionEffect(invoked.Action, invoked.Count, handlers)]);
    }
}
