using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Integration;

/// <summary>The facts of the root the reset, merge and operation reducers read.</summary>
/// <param name="TabId">The tab the command is for.</param>
/// <param name="Location">Where the tab runs its commands.</param>
/// <param name="Head">What HEAD points at, null while the tab has not read it.</param>
/// <param name="Operation">The merge, rebase, cherry-pick or revert that is stopped, or null.</param>
/// <param name="ConflictCount">How many files of the working tree are conflicted.</param>
/// <param name="IsBusy">A reset, merge or operation call already runs for the tab.</param>
public sealed record IntegrationContext(
    TabId TabId,
    RepositoryLocation Location,
    HeadState? Head,
    OperationInfo? Operation,
    int ConflictCount,
    bool IsBusy);
