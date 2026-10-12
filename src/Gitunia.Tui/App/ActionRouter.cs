using Gitunia.Core.Keymap;
using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App;

/// <summary>
/// Maps the action ids the keymap produces to what they do in the graph view. Actions the first shell does not
/// implement yet are ignored; an action's effects go through the <see cref="WorkspaceHost"/> as commands.
/// </summary>
public sealed class ActionRouter(WorkspaceHost host, ListView graphList, ListView detailList, Func<TabSession?> activeSession, Action quit)
{
    /// <summary>A short notice for the status bar from the last action, empty when none.</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>The keymap view of the focused pane (<c>Graph</c> or <c>Detail</c>).</summary>
    public string ViewName => FocusedPane == PaneKind.Detail ? DefaultKeymap.DetailView : DefaultKeymap.GraphView;

    public void Execute(ActionId action, int? count)
    {
        Message = string.Empty;
        var list = FocusedPane == PaneKind.Detail ? detailList : graphList;
        var itemCount = list.Source?.Count ?? 0;
        if (ListMotion.TryMove(action, count, list.SelectedItem ?? 0, itemCount, list.Viewport.Height, out var target))
        {
            Select(list, target);
        }
        else if (action == ActionIds.GraphJumpToHead)
        {
            JumpToHead();
        }
        else if (action == ActionIds.GraphOpenDetails)
        {
            FocusPane(PaneKind.Detail, detailList);
        }
        else if (action == ActionIds.NavigateBack)
        {
            if (FocusedPane == PaneKind.Detail)
            {
                FocusPane(PaneKind.Graph, graphList);
            }
            else
            {
                quit();
            }
        }
        else if (action != ActionIds.Cancel)
        {
            Message = ActionCatalog.Describe(action) + ": not implemented yet";
        }
    }

    // The toolkit's focus is the truth (a mouse click moves it without us), the tab's FocusedPane is kept in step by FocusPane.
    private PaneKind FocusedPane => detailList.HasFocus ? PaneKind.Detail : PaneKind.Graph;

    private void FocusPane(PaneKind pane, ListView list)
    {
        if (host.Workspace.ActiveTabId is { } tabId)
        {
            host.Dispatch(new TabCommand(tabId, new FocusPaneCommand(pane)));
        }

        if (pane == PaneKind.Detail && list.Source is DetailListSource { Content.Files.Count: > 0 } source)
        {
            // Land on the first file: the rows above it only describe the commit.
            Select(list, source.Content.FirstFileRow);
        }

        list.SetFocus();
        list.SetNeedsDraw();
        graphList.SetNeedsDraw();
    }

    private static void Select(ListView list, int item)
    {
        if ((list.Source?.Count ?? 0) == 0)
        {
            return;
        }

        list.SelectedItem = item;
        list.EnsureSelectedItemVisible();
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

        Select(graphList, index + ScreenLayout.ReservedLeadingRows);
    }
}
