using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Pick;

/// <summary>The facts of the root the pick reducer reads.</summary>
/// <param name="TabId">The tab the command is for.</param>
/// <param name="Location">Where the tab runs its commands.</param>
/// <param name="Head">What HEAD points at, null until loaded.</param>
/// <param name="Operation">The merge, rebase, cherry-pick or revert stopped in the tab, null when none.</param>
/// <param name="IsActive">The tab is the active one.</param>
/// <param name="IsBusy">A pick operation already runs for the tab (read from the tab's running operations).</param>
/// <param name="Question">The open mainline question, null when none.</param>
public sealed record PickContext(
    TabId TabId,
    RepositoryLocation Location,
    HeadState? Head,
    OperationInfo? Operation,
    bool IsActive,
    bool IsBusy,
    MainlineQuestion? Question);
