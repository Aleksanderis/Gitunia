using Gitunia.App;
using Gitunia.Core.Commits;
using Gitunia.Core.Editing;
using Gitunia.Core.Running;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>The message box port over the dispatcher's state.</summary>
/// <param name="host">The state.</param>
public sealed class StateMessageBoxPort(Dispatcher host) : IMessageBoxPort
{
    public CommitDraft? Draft => host.Workspace.ActiveTab?.Commit.Draft;

    public bool ShowsCursor => CommitRules.ShowsCursor(host.State);

    public bool IsCommitting => host.Workspace.ActiveTab?.Running.IsRunning(RunningOperationKind.Commit) == true;
}
