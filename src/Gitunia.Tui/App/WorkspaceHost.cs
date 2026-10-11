using Gitunia.Core.Workspaces;

namespace Gitunia.Tui.App;

/// <summary>
/// Holds the current <see cref="Workspace"/> and applies commands through the pure reducer. The effects of a
/// command are handed to whoever subscribed to <see cref="EffectRaised"/> (the effect runner). UI thread only.
/// </summary>
public sealed class WorkspaceHost(Workspace initial)
{
    public event Action<WorkspaceEffect>? EffectRaised;

    /// <summary>Raised after every command, once the new state is in place.</summary>
    public event Action? Changed;

    public Workspace Workspace { get; private set; } = initial;

    public void Dispatch(WorkspaceCommand command)
    {
        var update = WorkspaceReducer.Reduce(Workspace, command);
        Workspace = update.Workspace;
        foreach (var effect in update.Effects)
        {
            EffectRaised?.Invoke(effect);
        }

        Changed?.Invoke();
    }
}
