using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Running;

/// <summary>
/// The operations in flight for one tab. Busy refusals and tab eviction read this instead of a gate object. The root
/// reducer is the only writer: <see cref="Apply"/> after every feature command.
/// </summary>
/// <param name="Items">The operations in flight, oldest first.</param>
/// <param name="LastId">The id of the operation started last.</param>
public sealed record RunningOperations(ImmutableArray<RunningOperation> Items, long LastId)
{
    /// <summary>Nothing runs.</summary>
    public static RunningOperations Empty { get; } = new([], 0);

    /// <summary>Some operation runs, so the tab must not be evicted.</summary>
    public bool IsBusy => !Items.IsEmpty;

    /// <summary>An operation of the kind runs, so a second one is refused.</summary>
    public bool IsRunning(RunningOperationKind kind) => Items.Any(item => item.Kind == kind);

    /// <summary>
    /// The state after a command: a result command ends its operation, then every operation effect the reducer
    /// returned for this tab starts one (a result may start the next step).
    /// </summary>
    public RunningOperations Apply(TabId tabId, AppCommand command, ImmutableArray<AppEffect> effects)
    {
        var next = this;
        if (command is IOperationResult result)
        {
            next = next.End(result.Kind);
        }

        foreach (var effect in effects)
        {
            if (effect is OperationEffect operation && operation.TabId == tabId)
            {
                next = next.Begin(operation.Kind);
            }
        }

        return next;
    }

    private RunningOperations Begin(RunningOperationKind kind)
    {
        var id = LastId + 1;
        return new RunningOperations(Items.Add(new RunningOperation(id, kind)), id);
    }

    private RunningOperations End(RunningOperationKind kind)
    {
        for (var index = 0; index < Items.Length; index++)
        {
            if (Items[index].Kind == kind)
            {
                return this with { Items = Items.RemoveAt(index) };
            }
        }

        return this;
    }
}
