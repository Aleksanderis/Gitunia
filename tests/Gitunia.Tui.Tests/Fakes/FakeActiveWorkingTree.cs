using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.App.Clipboard;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>The shown tab's working tree, read from a test's own value on each ask.</summary>
/// <param name="tree">Returns the working tree of the moment.</param>
public sealed class FakeActiveWorkingTree(Func<WorkingTreeState?> tree) : IActiveWorkingTree
{
    public WorkingTreeState? WorkingTree() => tree();
}
