namespace Gitunia.Core;

/// <summary>
/// Anything that can change <see cref="AppState"/>: user intent and the results of effects alike. The root hierarchy
/// has
/// one case per feature (<c>WorkspaceCommand</c>, <c>TagCommand</c>, ...); a feature's own hierarchy is that case.
/// </summary>
public abstract record AppCommand
{
    private protected AppCommand()
    {
    }
}
