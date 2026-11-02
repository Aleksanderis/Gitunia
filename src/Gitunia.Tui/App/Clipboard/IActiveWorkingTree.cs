using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Tui.App.Clipboard;

/// <summary>The working tree of the shown tab.</summary>
public interface IActiveWorkingTree
{
    /// <summary>The working tree of the shown tab, null when there is no tab or it has none.</summary>
    WorkingTreeState? WorkingTree();
}
