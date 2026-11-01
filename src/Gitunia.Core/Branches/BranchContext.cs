using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Branches;

/// <summary>The facts of the root the branch reducer reads: the tab, its refs and its stopped operation.</summary>
/// <param name="TabId">The tab the command is for.</param>
/// <param name="Location">Where the tab runs its commands.</param>
/// <param name="Refs">The refs the tab shows.</param>
/// <param name="Operation">The merge, rebase, cherry-pick or revert stopped in the tab, null when none.</param>
/// <param name="IsActive">The tab is the active one.</param>
/// <param name="IsBusy">A branch operation already runs for the tab (read from the tab's running operations).</param>
public sealed record BranchContext(
    TabId TabId,
    RepositoryLocation Location,
    ImmutableArray<GitRef> Refs,
    OperationInfo? Operation,
    bool IsActive,
    bool IsBusy);
