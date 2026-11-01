using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App;

/// <summary>
/// The actions of the graph and the detail list: motion, jump to HEAD, copy of a hash or a path, opening the details,
/// stepping back and quitting. It is the last one asked, so it also takes what nobody claimed: Esc clears the status
/// message and any other action says it is not implemented yet.
/// </summary>
/// <param name="host">Holds the state and takes the commands.</param>
/// <param name="graphList">The graph list.</param>
/// <param name="detailList">The detail list.</param>
/// <param name="activeSession">The active tab's session, null when there is none.</param>
/// <param name="quit">Asks the application to quit.</param>
/// <param name="screenHeight">The screen height in rows; screens under 14 rows have no details pane.</param>
/// <param name="statuses">The status actions, whose fallback takes Esc.</param>
/// <param name="clipboard">Copies a commit hash.</param>
public sealed class NavigationActions(
    Dispatcher host,
    ListView graphList,
    ListView detailList,
    Func<TerminalTabSession?> activeSession,
    Action quit,
    Func<int> screenHeight,
    StatusActionHandler statuses,
    ClipboardCopier clipboard) : IActionHandler, IActionNotice
{
    /// <summary>The notice shown when <c>G</c> stops at the last loaded commit while the log is still streaming in.</summary>
    public const string StillLoadingNotice = "Still loading: at the last loaded commit";

    private readonly DetailPathCopier _pathCopier = new(clipboard);

    /// <summary>A short notice for the status bar from the last action, empty when none.</summary>
    public string Notice { get; private set; } = string.Empty;

    /// <summary>Always true: this is the last handler, and what nothing else took ends here.</summary>
    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        var list = FocusedPane == PaneKind.Detail ? detailList : graphList;
        var itemCount = list.Source?.Count ?? 0;
        if (ListMotion.TryMove(action, count, list.SelectedItem ?? 0, itemCount, list.Viewport.Height, out var target))
        {
            Select(list, target);
            MarkLoadingEnd(action, count);
        }
        else if (action == ActionIds.GraphJumpToHead)
        {
            JumpToHead();
        }
        else if (action == ActionIds.GraphCopyHash && FocusedPane == PaneKind.Graph)
        {
            CopySelectedHash();
        }
        else if (action == ActionIds.DetailCopyPath && FocusedPane == PaneKind.Detail)
        {
            CopySelectedPath();
        }
        else if (action == ActionIds.GraphOpenDetails)
        {
            OpenDetails();
        }
        else if (action == ActionIds.AppQuit)
        {
            quit();
        }
        else if (action == ActionIds.NavigateBack)
        {
            // Only the Detail pane steps back here; the graph is the root, and quitting is app.quit (q).
            if (FocusedPane == PaneKind.Detail)
            {
                FocusPane(PaneKind.Graph, graphList);
            }
        }
        else if (action == ActionIds.DetailCollapse || action == ActionIds.DetailExpand)
        {
            // Bare h and l stay inside the pane: with no directory or folder to fold they do nothing.
        }
        else if (!statuses.TryHandleFallback(action))
        {
            // Esc that nothing else claimed was handled above: it clears the status message.
            Notice = $"{ActionCatalog.Describe(action)}: not implemented yet";
        }

        return true;
    }

    // The toolkit's focus is the truth (a mouse click moves it without us), the tab's FocusedPane is kept in step by
    // FocusPane.
    private PaneKind FocusedPane => detailList.HasFocus ? PaneKind.Detail : PaneKind.Graph;

    // A count targets a line, so only a plain G (jump to the end) is hinted; the hint ends with the load.
    private void MarkLoadingEnd(ActionId action, int? count)
    {
        if (action != ActionIds.ListBottom || count is not null || FocusedPane != PaneKind.Graph)
        {
            return;
        }

        // Past the soft row limit the end is not loaded: G starts loading the rest (#515).
        activeSession()?.RequestExpand(true);
        if (activeSession()?.IsLoading == true)
        {
            Notice = StillLoadingNotice;
        }
    }

    private void OpenDetails()
    {
        if (DetailLayout.PaneHeight(screenHeight()) == 0)
        {
            Notice = "The details pane needs a taller screen";

            return;
        }

        FocusPane(PaneKind.Detail, detailList);
    }

    private void FocusPane(PaneKind pane, ListView list)
    {
        if (host.Workspace.ActiveTabId is { } tabId)
        {
            host.Dispatch(new TabCommand(tabId, new FocusPaneCommand(pane)));
        }

        if (pane == PaneKind.Detail
            && list.Source is DetailListSource { Content: { Tab: BottomTab.Changes, Files.Count: > 0 } } source)
        {
            // Land on the first file: the row above it only counts them.
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

    private void CopySelectedHash()
    {
        var session = activeSession();
        var commitIndex = graphList.SelectedItem is { } item ? GraphListSource.CommitIndexOf(item) : -1;
        if (session is null || commitIndex < 0 || commitIndex >= session.Graph.Count)
        {
            Notice = "No commit selected";

            return;
        }

        var hash = session.Graph[commitIndex].Id.ToString();
        clipboard.Copy(hash, $"commit hash {hash[..Math.Min(7, hash.Length)]}");
    }

    private void CopySelectedPath() =>
        Notice = _pathCopier.Copy(activeSession()?.DetailSource, detailList.SelectedItem);

    private void JumpToHead()
    {
        var session = activeSession();
        var headHash = host.Workspace.ActiveTab?.Head?.CommitHash;
        if (session is null || headHash is null || !ObjectId.TryParse(headHash, out var headId))
        {
            Notice = "HEAD is not known yet";

            return;
        }

        var index = session.Graph.IndexOf(headId);
        if (index < 0)
        {
            // Below the soft row limit HEAD may still come: the rest loads now and the next try finds it.
            session.RequestExpand(true);
            Notice = session.IsGraphComplete ? "HEAD is not in the graph" : "HEAD is not loaded yet";

            return;
        }

        Select(graphList, index + ScreenLayout.ReservedLeadingRows);
    }
}
