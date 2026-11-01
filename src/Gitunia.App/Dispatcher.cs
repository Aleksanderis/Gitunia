using Gitunia.Core;
using Gitunia.Core.Input;
using Gitunia.Core.Workspaces;

namespace Gitunia.App;

/// <summary>
/// Holds the current <see cref="AppState"/> and applies commands through the pure <see cref="AppReducer"/>. The effects
/// of a command are handed to whoever subscribed to <see cref="EffectRaised"/> (the effect runner). <see
/// cref="Dispatch"/>
/// is for the UI thread; <see cref="Post"/> accepts a command from any thread and reduces it on the UI thread.
/// </summary>
/// <param name="initial">The state to start from.</param>
/// <param name="ui">Moves a posted command to the UI thread; null runs it at once (tests, single-threaded
/// callers).</param>
/// <param name="clock">The time source commands are stamped with (the reducers read no clock).</param>
public sealed class Dispatcher(AppState initial, IUiInvoker? ui = null, TimeProvider? clock = null) : IDispatcher
{
    /// <summary>The clock for the time a command carries.</summary>
    public TimeProvider Clock { get; } = clock ?? TimeProvider.System;

    public event Action<AppEffect>? EffectRaised;

    /// <summary>Raised after every command, once the new state is in place.</summary>
    public event Action? Changed;

    public AppState State { get; private set; } = initial;

    public Workspace Workspace => State.Workspace;

    /// <summary>Replaces the workspace with a restored one (the saved session) and raises its effects, such as the
    /// load of every restored tab.</summary>
    public void Restore(WorkspaceUpdate update)
    {
        State = State with { Workspace = update.Workspace };
        foreach (var effect in update.Effects)
        {
            EffectRaised?.Invoke(effect);
        }

        Changed?.Invoke();
    }

    /// <summary>Reduces the command now. UI thread only.</summary>
    public void Dispatch(AppCommand command)
    {
        var update = AppReducer.Reduce(State, command);
        var previous = State;
        State = update.State;
        foreach (var effect in update.Effects)
        {
            EffectRaised?.Invoke(effect);
        }

        // An input command only picks handlers, which dispatch their own commands; a key that changes nothing in the
        // state must not redraw every subscriber of the state.
        if (command is InputCommand && ReferenceEquals(previous, update.State))
        {
            return;
        }

        Changed?.Invoke();
    }

    public void Post(AppCommand command)
    {
        if (ui is null)
        {
            Dispatch(command);
            return;
        }

        ui.Post(() => Dispatch(command));
    }
}
