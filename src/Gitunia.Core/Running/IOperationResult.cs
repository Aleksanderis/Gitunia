using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Running;

/// <summary>
/// A result command that ends an operation (success, failure or cancel). The root reducer clears the running mark of
/// <see cref="Kind"/> for <see cref="TabId"/> after the feature reducer has seen the command, so the reducer still
/// reads the busy flag of the operation that just ended.
/// </summary>
public interface IOperationResult
{
    /// <summary>The tab the operation ran for.</summary>
    TabId TabId { get; }

    /// <summary>What the operation was.</summary>
    RunningOperationKind Kind { get; }
}
