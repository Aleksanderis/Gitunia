using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.App.Blame;

namespace Gitunia.Tui.Tests.App.Blame;

/// <summary>Finds nothing to blame.</summary>
public sealed class NoBlameTargets : IBlameTargets
{
    public BlameTarget Resolve(RepositoryState tab) => BlameTarget.Refused("nothing");
}
