using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Tui.App.Blame;

/// <summary>Finds the file the blame action stands for.</summary>
public interface IBlameTargets
{
    /// <summary>What to blame in the active tab, or why there is nothing.</summary>
    /// <param name="tab">The active tab.</param>
    BlameTarget Resolve(RepositoryState tab);
}
