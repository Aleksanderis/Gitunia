using Gitunia.Core.Keymap;
using Gitunia.Core.Lists;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App;

/// <summary>
/// Handles the actions that concern pane focus and the sidebar: moving between panes, the sidebar's own keys, the
/// name filter and jumping from a ref to its commit in the graph. Everything it changes goes through commands.
/// </summary>
public sealed class SidebarActionHandler(
    WorkspaceHost host,
    SidebarListSource source,
    Func<TabSession?> activeSession,
    Func<int> pageHeight,
    Func<bool> isSidebarVisible,
    Func<bool> isDetailVisible,
    Action<int> selectGraphItem,
    Action<KeyMode> setMode)
{
    private bool isEditingFilter;
    private TabId editingTabId;

    /// <summary>The graph is shown (it is hidden while the detail or diff pane is zoomed).</summary>
    public Func<bool> IsGraphVisible { get; set; } = () => true;

    /// <summary>The diff pane is shown.</summary>
    public Func<bool> IsDiffVisible { get; set; } = () => false;

    /// <summary>A short notice for the status bar from the last action, empty when none.</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>The filter is being typed (the keymap is in Insert mode).</summary>
    public bool IsEditingFilter => isEditingFilter;

    /// <summary>The sidebar state the surrounding views draw: the filter line and the focus highlight.</summary>
    public SidebarChrome Chrome => host.Workspace.ActiveTab is { } tab
        ? new SidebarChrome(tab.Sidebar.FilterText, isEditingFilter, tab.View.FocusedPane == PaneKind.Sidebar)
        : SidebarChrome.Idle;

    /// <summary>Returns true when the action was handled here and the graph view must not act on it.</summary>
    public bool TryHandle(ActionId action, int? count)
    {
        Message = string.Empty;
        if (host.Workspace.ActiveTab is not { } tab)
        {
            return false;
        }

        if (isEditingFilter && editingTabId != tab.Id)
        {
            // The active tab changed while typing: the filter belongs to the old tab.
            StopEditing();
        }

        if (TryHandlePaneAction(tab, action))
        {
            return true;
        }

        if (isEditingFilter)
        {
            return HandleFilterEditing(action);
        }

        return tab.View.FocusedPane == PaneKind.Sidebar && HandleSidebarAction(tab, action, count);
    }

    /// <summary>Edits the filter with a key typed in Insert mode: printable text is appended, Backspace removes the last character.</summary>
    public void OnKey(KeyEvent key)
    {
        if (!isEditingFilter || host.Workspace.ActiveTab is not { } tab || tab.Id != editingTabId)
        {
            return;
        }

        var text = tab.Sidebar.FilterText;
        if (key.IsPrintable)
        {
            SetFilter(tab, text + key.Name);
        }
        else if (key.Name == KeyNames.Backspace && text.Length > 0)
        {
            var length = text.Length > 1 && char.IsLowSurrogate(text[^1]) ? 2 : 1;
            SetFilter(tab, text[..^length]);
        }
    }

    /// <summary>Selects the commit of the row under the cursor in the graph (after a click or <c>Enter</c>).</summary>
    public void ActivateCursor()
    {
        Message = string.Empty;
        var rows = source.Rows;
        if (host.Workspace.ActiveTab is not { } tab || rows.IsEmpty)
        {
            return;
        }

        var row = rows[SidebarBuilder.ClampCursor(tab.Sidebar.Cursor, rows.Length)];
        if (row.IsSection)
        {
            Send(tab, new ToggleSidebarSectionCommand());
            return;
        }

        JumpToCommit(row);
    }

    private bool TryHandlePaneAction(RepoState tab, ActionId action)
    {
        var focused = tab.View.FocusedPane;
        var available = OpenPanes();
        if (action == ActionIds.PaneNext || action == ActionIds.PanePrevious)
        {
            FocusPane(tab, PaneNavigator.Cycle(focused, action == ActionIds.PaneNext, available));
            return true;
        }

        if (action == ActionIds.PaneFocusLeft || action == ActionIds.PaneFocusRight)
        {
            FocusPane(tab, PaneNavigator.Step(focused, action == ActionIds.PaneFocusRight, available));
            return true;
        }

        if (action == ActionIds.PaneFocusUp || action == ActionIds.PaneFocusDown)
        {
            FocusPane(tab, PaneNavigator.StepVertically(focused, action == ActionIds.PaneFocusDown, available));
            return true;
        }

        return false;
    }

    private List<PaneKind> OpenPanes()
    {
        var panes = new List<PaneKind>();
        if (IsGraphVisible())
        {
            panes.Add(PaneKind.Graph);
        }
        if (isSidebarVisible())
        {
            panes.Insert(0, PaneKind.Sidebar);
        }

        if (isDetailVisible())
        {
            panes.Add(PaneKind.Detail);
        }

        if (IsDiffVisible())
        {
            panes.Add(PaneKind.Diff);
        }

        return panes;
    }

    private bool HandleFilterEditing(ActionId action)
    {
        if (action == ActionIds.InputAccept)
        {
            StopEditing();
            return true;
        }

        if (action == ActionIds.Cancel)
        {
            StopEditing();
            if (host.Workspace.ActiveTab is { } tab)
            {
                SetFilter(tab, string.Empty);
            }

            return true;
        }

        return false;
    }

    private bool HandleSidebarAction(RepoState tab, ActionId action, int? count)
    {
        if (ListMotion.TryMove(action, count, 0, 1, 1, out _))
        {
            Send(tab, new MoveSidebarCursorCommand(action, count, pageHeight()));
        }
        else if (action == ActionIds.SidebarExpand)
        {
            Send(tab, new ExpandSidebarCommand());
        }
        else if (action == ActionIds.SidebarCollapse)
        {
            Send(tab, new CollapseSidebarCommand());
        }
        else if (action == ActionIds.SidebarActivate)
        {
            ActivateCursor();
        }
        else if (action == ActionIds.SidebarFilter)
        {
            isEditingFilter = true;
            editingTabId = tab.Id;
            setMode(KeyMode.Insert);
        }
        else if (action == ActionIds.NavigateBack)
        {
            FocusPane(tab, PaneKind.Graph);
        }
        else if (action == ActionIds.Cancel)
        {
            if (tab.Sidebar.HasFilter)
            {
                SetFilter(tab, string.Empty);
            }
            else
            {
                FocusPane(tab, PaneKind.Graph);
            }
        }
        else
        {
            return false;
        }

        return true;
    }

    private void JumpToCommit(SidebarRow row)
    {
        var session = activeSession();
        if (row.Kind == SidebarRowKind.Stash)
        {
            Message = "Stash commits are not shown in the graph";
        }
        else if (row.CommitHash is null || !ObjectId.TryParse(row.CommitHash, out var commit))
        {
            Message = row.Label + " has no commits yet";
        }
        else
        {
            var index = session?.Graph.IndexOf(commit) ?? -1;
            if (index < 0)
            {
                Message = session is { IsLoading: true } ? row.Label + " is not loaded yet" : row.Label + " is not in the graph";
            }
            else
            {
                selectGraphItem(index + ScreenLayout.ReservedLeadingRows);
            }
        }
    }

    private void StopEditing()
    {
        isEditingFilter = false;
        setMode(KeyMode.Normal);
    }

    private void SetFilter(RepoState tab, string text) => Send(tab, new SetSidebarFilterCommand(text));

    private void FocusPane(RepoState tab, PaneKind pane) => Send(tab, new FocusPaneCommand(pane));

    private void Send(RepoState tab, RepoCommand command) => host.Dispatch(new TabCommand(tab.Id, command));
}
