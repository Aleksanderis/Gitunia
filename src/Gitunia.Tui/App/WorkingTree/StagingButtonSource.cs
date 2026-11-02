using Gitunia.App;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.WorkingTree;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>The staging buttons of the shown tab's selected file section.</summary>
/// <param name="host">Holds the workspace.</param>
public sealed class StagingButtonSource(Dispatcher host) : IGroupButtonSource
{
    /// <inheritdoc />
    public WorkingTreeButtons Buttons() =>
        WorkingTreePaneRenderer.StagingButtons(host.Workspace.ActiveTab?.WorkingTree);
}
