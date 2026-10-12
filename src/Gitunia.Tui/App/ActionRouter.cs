using Gitunia.Core.Keymap;
using Gitunia.Core.Lists;
using Gitunia.Git;
using Gitunia.Tui.Rendering;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App;

/// <summary>
/// Maps the action ids the keymap produces to what they do in the graph view. Actions the first shell does not
/// implement yet are ignored; an action's effects go through the <see cref="WorkspaceHost"/> as commands.
/// </summary>
public sealed class ActionRouter(WorkspaceHost host, ListView graphList, Func<TabSession?> activeSession, Action quit)
{
    /// <summary>A short notice for the status bar from the last action, empty when none.</summary>
    public string Message { get; private set; } = string.Empty;

    public void Execute(ActionId action, int? count)
    {
        Message = string.Empty;
        var itemCount = graphList.Source?.Count ?? 0;
        if (ListMotion.TryMove(action, count, graphList.SelectedItem ?? 0, itemCount, graphList.Viewport.Height, out var target))
        {
            Select(target);
        }
        else if (action == ActionIds.GraphJumpToHead)
        {
            JumpToHead();
        }
        else if (action == ActionIds.NavigateBack)
        {
            quit();
        }
        else if (action != ActionIds.Cancel)
        {
            Message = ActionCatalog.Describe(action) + ": not implemented yet";
        }
    }

    private void Select(int item)
    {
        if ((graphList.Source?.Count ?? 0) == 0)
        {
            return;
        }

        graphList.SelectedItem = item;
        graphList.EnsureSelectedItemVisible();
    }

    private void JumpToHead()
    {
        var session = activeSession();
        var headHash = host.Workspace.ActiveTab?.Head?.CommitHash;
        if (session is null || headHash is null || !ObjectId.TryParse(headHash, out var headId))
        {
            Message = "HEAD is not known yet";
            return;
        }

        var index = session.Graph.IndexOf(headId);
        if (index < 0)
        {
            Message = session.IsLoading ? "HEAD is not loaded yet" : "HEAD is not in the graph";
            return;
        }

        Select(index + ScreenLayout.ReservedLeadingRows);
    }
}
