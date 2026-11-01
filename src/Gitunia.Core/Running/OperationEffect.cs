using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Running;

/// <summary>
/// An effect that starts an operation: the root reducer marks <see cref="Kind"/> running for <see cref="TabId"/> when a
/// feature reducer returns one, and the matching <see cref="IOperationResult"/> command clears it again.
/// </summary>
public abstract record OperationEffect : AppEffect
{
    private protected OperationEffect(TabId tabId, RunningOperationKind kind)
    {
        TabId = tabId;
        Kind = kind;
    }

    /// <summary>The tab the operation runs for and the result belongs to.</summary>
    public TabId TabId { get; }

    /// <summary>What the operation does.</summary>
    public RunningOperationKind Kind { get; }
}
