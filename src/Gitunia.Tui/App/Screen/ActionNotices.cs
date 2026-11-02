namespace Gitunia.Tui.App.Screen;

/// <summary>The short notices of the last workspace action and of the last pane action.</summary>
/// <param name="workspace">The tab actions.</param>
/// <param name="actions">The action executor, which keeps the notice of the pane that took the last action.</param>
public sealed class ActionNotices(WorkspaceActions workspace, ActionExecutor actions)
{
    /// <summary>The notice to show: the workspace's, else the pane's; empty when none.</summary>
    public string Text()
    {
        if (workspace.Message.Length > 0)
        {
            return workspace.Message;
        }

        return actions.Message;
    }
}
