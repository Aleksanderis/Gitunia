using Gitunia.Core.Keymap;
using Gitunia.Core.Lists;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Sidebar;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App;

/// <summary>
/// Maps the action ids the keymap produces to what they do in the graph view. Actions the first shell does not
/// implement yet are ignored; an action's effects go through the <see cref="WorkspaceHost"/> as commands.
/// </summary>
public sealed class ActionRouter(WorkspaceHost host, ListView graphList, ListView detailList, Func<TabSession?> activeSession, Action quit, SidebarActionHandler sidebar, StatusChannel statuses, ClipboardCopier clipboard)
{
    /// <summary>The notice shown when <c>G</c> stops at the last loaded commit because the log is still streaming in.</summary>
    public const string StillLoadingNotice = "Still loading: at the last loaded commit";

    private readonly StatusActionHandler _statusActions = new(statuses, activeSession);
    private readonly DetailPathCopier _pathCopier = new(clipboard);
    private string _message = string.Empty;

    /// <summary>A short notice for the status bar from the last action, empty when none.</summary>
    public string Message
    {
        get => _message == StillLoadingNotice && activeSession()?.IsLoading != true ? string.Empty : _message;
        private set => _message = value;
    }

    /// <summary>Refreshes the active tab; false when it cannot be refreshed yet (still loading). Set by the host.</summary>
    public Func<bool>? Refresh { get; set; }

    /// <summary>Modal prompts (search box, jump-to-ref) that get the first try at every action.</summary>
    public IReadOnlyList<IPromptHandler> Prompts { get; set; } = [];

    /// <summary>Visual mode (range selection), which sees the actions before every pane handler.</summary>
    public VisualRangeController? Visual { get; set; }

    /// <summary>The pane resize keys.</summary>
    public PaneSizeController? PaneSizes { get; set; }

    /// <summary>The bottom pane tab keys.</summary>
    public DetailTabController? DetailTabs { get; set; }

    /// <summary>The diff pane's controller, which gets the actions after the sidebar and before the graph and detail lists.</summary>
    public DiffPaneController? Diff { get; set; }

    /// <summary>The keymap view of the focused pane (<c>Graph</c>, <c>Detail</c>, <c>Sidebar</c> or <c>Diff</c>).</summary>
    public string ViewName => host.Workspace.ActiveTab?.View.FocusedPane switch
    {
        PaneKind.Sidebar => DefaultKeymap.SidebarView,
        PaneKind.Diff => DefaultKeymap.DiffView,
        _ => FocusedPane == PaneKind.Detail ? DefaultKeymap.DetailView : DefaultKeymap.GraphView,
    };

    public void Execute(ActionId action, int? count)
    {
        Message = string.Empty;
        if (_statusActions.TryHandle(action))
        {
            Message = _statusActions.Message;
            return;
        }

        if (action == ActionIds.RepositoryRefresh)
        {
            Message = Refresh?.Invoke() == true ? "refreshing..." : "not loaded yet";
            return;
        }

        if (Visual?.TryExecute(action, count) == true)
        {
            Message = Visual.Notice;
            return;
        }

        if (PaneSizes?.TryExecute(action, count) == true)
        {
            Message = PaneSizes.Notice;
            return;
        }

        if (DetailTabs?.TryExecute(action) == true)
        {
            return;
        }

        if (sidebar.TryHandle(action, count))
        {
            Message = sidebar.Message;
            return;
        }

        if (Diff?.TryExecute(action, count) == true)
        {
            Message = Diff.Notice;
            return;
        }

        var list = FocusedPane == PaneKind.Detail ? detailList : graphList;
        if (OffersToPrompts(action))
        {
            foreach (var prompt in Prompts)
            {
                if (prompt.TryExecute(action, count))
                {
                    Message = prompt.Notice;
                    return;
                }
            }
        }

        var itemCount = list.Source?.Count ?? 0;
        if (ListMotion.TryMove(action, count, list.SelectedItem ?? 0, itemCount, list.Viewport.Height, out var target))
        {
            Select(list, target);
            // A count targets a line, so only a plain G (jump to the end) is hinted; the hint ends with the load.
            if (action == ActionIds.ListBottom && count is null && FocusedPane == PaneKind.Graph && activeSession()?.IsLoading == true)
            {
                Message = StillLoadingNotice;
            }
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
        else if (!_statusActions.TryHandleFallback(action))
        {
            // Esc that nothing else claimed was handled above: it clears the status message.
            Message = $"{ActionCatalog.Describe(action)}: not implemented yet";
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

        if (pane == PaneKind.Detail && list.Source is DetailListSource { Content: { Tab: BottomTab.Changes, Files.Count: > 0 } } source)
        {
            // Land on the first file: the row above it only counts them.
            Select(list, source.Content.FirstFileRow);
        }

        list.SetFocus();
        list.SetNeedsDraw();
        graphList.SetNeedsDraw();
    }

    // Search and jump-to-ref work on the graph; in other panes only the accept/cancel actions of an open prompt apply.
    private bool OffersToPrompts(ActionId action) =>
        FocusedPane == PaneKind.Graph || action == ActionIds.InputAccept || action == ActionIds.Cancel || action == ActionIds.TabRename;

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
            Message = "No commit selected";
            return;
        }

        var hash = session.Graph[commitIndex].Id.ToString();
        clipboard.Copy(hash, $"commit hash {hash[..Math.Min(7, hash.Length)]}");
    }

    private void CopySelectedPath() =>
        Message = _pathCopier.Copy(activeSession()?.DetailSource, detailList.SelectedItem);

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
