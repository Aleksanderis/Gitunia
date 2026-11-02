using Gitunia.App;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Tui.App.Clipboard;

/// <summary>Reads the working tree of the shown tab from the workspace.</summary>
/// <param name="host">Holds the workspace.</param>
public sealed class ActiveWorkingTree(Dispatcher host) : IActiveWorkingTree
{
    /// <inheritdoc />
    public WorkingTreeState? WorkingTree() => host.Workspace.ActiveTab?.WorkingTree;
}
