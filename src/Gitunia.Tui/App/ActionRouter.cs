using Gitunia.App.Status;
using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Compare;
using Gitunia.Tui.App.Creation;
using Gitunia.Tui.App.Details;
using Gitunia.Tui.App.ExternalTools;
using Gitunia.Tui.App.Merge;
using Gitunia.Tui.App.Operations;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Pick;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.App.Remote;
using Gitunia.Tui.App.Reset;
using Gitunia.Tui.App.Sidebar;
using Gitunia.Tui.App.Stash;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.Tags;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.App.Worktrees;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App;

/// <summary>
/// Maps the action ids the keymap produces to what they do in the graph view. Actions the first shell does not
/// implement yet are ignored; an action's effects go through the <see cref="Dispatcher"/> as commands.
/// </summary>
public sealed class ActionRouter(
    Dispatcher host,
    ListView graphList,
    ListView detailList,
    Func<TerminalTabSession?> activeSession,
    Action quit,
    SidebarInput sidebar,
    StatusReporter statuses,
    ClipboardCopier clipboard)
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

    /// <summary>The screen height in rows; null means no limit. Screens under 14 rows have no details pane.</summary>
    public Func<int>? ScreenHeight { get; set; }

    /// <summary>Refreshes the active tab; false when it cannot be refreshed yet (still loading). Set by the host.</summary>
    public Func<bool>? Refresh { get; set; }

    /// <summary>The external tool hand-offs (difftool, mergetool, editor).</summary>
    public ExternalToolInput? ExternalTools { get; set; }

    /// <summary>Modal prompts (search box, jump-to-ref) that get the first try at every action.</summary>
    public IReadOnlyList<IPromptHandler> Prompts { get; set; } = [];

    /// <summary>The quick jump prompt, which sees every action first: it works in every pane that has it and owns Esc and Enter while open.</summary>
    public IPromptHandler? QuickJump { get; set; }

    /// <summary>The prompt that lives in state (#971): it sees Enter and Esc before every other handler.</summary>
    public IPromptHandler? OpenPrompt { get; set; }

    /// <summary>Visual mode (range selection), which sees the actions before every pane handler.</summary>
    public VisualRangeInput? Visual { get; set; }

    /// <summary>The branch actions (create, switch, rename, delete), which own their prompts in every pane.</summary>
    public BranchActions? Branches { get; set; }

    /// <summary>The stash actions (push, apply, pop, drop, restore a file), which own their prompt and dialog.</summary>
    public StashActions? Stash { get; set; }

    /// <summary>The tag actions (create, delete, push, remote delete), which own their prompts in every pane.</summary>
    public TagActions? Tags { get; set; }

    /// <summary>The remote management actions (add, remove, rename, set URL); they own their prompts.</summary>
    public RemoteManagementActions? RemoteManagement { get; set; }

    /// <summary>Fetch, pull, push, the push cases that ask first and the remote branch delete, and the cancel of the running
    /// one; Esc is only its once the prompts passed.</summary>
    public SyncActions? Sync { get; set; }

    /// <summary>Clone and init from the picker; Esc is only its once everything else passed.</summary>
    public CreationInput? Creation { get; set; }

    /// <summary>Merging the selected commit or branch into the current branch (merge, no-ff, squash).</summary>
    public MergeActions? Merge { get; set; }

    /// <summary>Cherry-picking or reverting the selected commits onto the current branch.</summary>
    public PickActions? Pick { get; set; }

    /// <summary>Marks a commit and compares it with another (#535); also clears the mark with Esc.</summary>
    public CompareInput? Compare { get; set; }

    /// <summary>Resetting the current branch to a commit, in the graph and the sidebar.</summary>
    public ResetActions? Reset { get; set; }

    /// <summary>Rebasing the current branch onto the selected commit or ref (#39).</summary>
    public RebaseActions? Rebase { get; set; }

    /// <summary>The Changes view's actions, which see the actions before every history pane handler.</summary>
    public WorkingTreeActionHandler? WorkingTree { get; set; }

    /// <summary>The worktree operations, ahead of the sidebar's own keys.</summary>
    public WorktreeActions? Worktrees { get; set; }

    /// <summary>Continue, abort and skip of the operation in progress (works in every view).</summary>
    public OperationActions? Operations { get; set; }

    /// <summary>The pane resize keys.</summary>
    public PaneSizeInput? PaneSizes { get; set; }

    /// <summary>The keys of the Files tab (open, close, filter).</summary>
    public FileTreeInput? FileTree { get; set; }

    /// <summary>Opening the tracker link of a commit message line.</summary>
    public IssueLinkInput? IssueLinks { get; set; }

    /// <summary>The bottom pane tab keys.</summary>
    public DetailTabInput? DetailTabs { get; set; }

    /// <summary>The diff pane's controller, which gets the actions after the sidebar and before the graph and detail lists.</summary>
    public DiffPaneInput? Diff { get; set; }

    /// <summary>The keymap view of the focused pane (<c>Graph</c>, <c>Detail</c>, <c>Sidebar</c> or <c>Diff</c>).</summary>
    public string ViewName => host.Workspace.ActiveTab?.View.FocusedPane switch
    {
        PaneKind.Sidebar => DefaultKeymap.SidebarView,
        PaneKind.Diff => DefaultKeymap.DiffView,
        PaneKind.WorkingUnstaged => DefaultKeymap.WorkingUnstagedView,
        PaneKind.WorkingStaged => DefaultKeymap.WorkingStagedView,
        PaneKind.WorkingUnstagedDiff => DefaultKeymap.WorkingUnstagedDiffView,
        PaneKind.WorkingStagedDiff => DefaultKeymap.WorkingStagedDiffView,
        PaneKind.WorkingMessage => DefaultKeymap.WorkingMessageView,
        _ => FocusedPane == PaneKind.Detail ? DetailViewName() : DefaultKeymap.GraphView,
    };

    private string DetailViewName() => host.Workspace.ActiveTab?.View.BottomTab switch
    {
        BottomTab.Changes => DefaultKeymap.DetailChangesView,
        BottomTab.Files => DefaultKeymap.DetailFilesView,
        _ => DefaultKeymap.DetailView,
    };

    // A refusal ("Busy", "is checked out in ...") is a warning: it gets the warning colour and joins the history.
    private void Refuse(string notice)
    {
        if (notice.Length > 0)
        {
            statuses.Warning(notice);
        }
    }

    public void Execute(ActionId action, int? count)
    {
        Message = string.Empty;
        if (OpenPrompt?.TryExecute(action, count) == true)
        {
            return;
        }

        if (QuickJump?.TryExecute(action, count) == true)
        {
            return;
        }

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

        if (Branches?.TryExecute(action, count) == true)
        {
            return;
        }

        if (Stash?.TryExecute(action, count) == true)
        {
            return;
        }

        if (Tags?.TryExecute(action, count) == true)
        {
            return;
        }

        if (action != ActionIds.Cancel && Sync?.TryExecute(action, count) == true)
        {
            return;
        }

        if (RemoteManagement?.TryExecute(action, count) == true)
        {
            return;
        }

        if (Merge?.TryExecute(action, count) == true)
        {
            return;
        }

        if (Pick?.TryExecute(action, count) == true)
        {
            Refuse(Pick.Notice);
            return;
        }

        if (Reset?.TryExecute(action, count) == true)
        {
            return;
        }

        if (Rebase?.TryExecute(action, count) == true)
        {
            return;
        }

        if (WorkingTree?.TryExecute(action, count) == true)
        {
            Message = WorkingTree.Message;
            return;
        }

        if (ExternalTools?.TryExecute(action) == true)
        {
            return;
        }

        if (Visual?.TryExecute(action, count) == true)
        {
            Message = string.Empty;
            return;
        }

        if (PaneSizes?.TryExecute(action, count) == true)
        {
            return;
        }

        if (IssueLinks?.TryExecute(action, count) == true)
        {
            return;
        }

        if (FileTree?.TryExecute(action, count) == true)
        {
            return;
        }

        if (DetailTabs?.TryExecute(action) == true)
        {
            return;
        }

        if (Operations?.TryExecute(action, count) == true)
        {
            return;
        }

        if (Worktrees?.TryExecute(action) == true)
        {
            return;
        }

        if (sidebar.TryHandle(action, count))
        {
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

        if (Sync?.TryExecute(action, count) == true)
        {
            return;
        }

        if (Compare?.TryExecute(action) == true)
        {
            return;
        }

        if (Creation?.TryCancelWithEscape(action) == true)
        {
            return;
        }

        var itemCount = list.Source?.Count ?? 0;
        if (ListMotion.TryMove(action, count, list.SelectedItem ?? 0, itemCount, list.Viewport.Height, out var target))
        {
            Select(list, target);
            // A count targets a line, so only a plain G (jump to the end) is hinted; the hint ends with the load.
            if (action == ActionIds.ListBottom && count is null && FocusedPane == PaneKind.Graph)
            {
                // Past the soft row limit the end is not loaded: G starts loading the rest (#515).
                activeSession()?.RequestExpand(true);
                if (activeSession()?.IsLoading == true)
                {
                    Message = StillLoadingNotice;
                }
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
            if (ScreenHeight?.Invoke() is { } height && DetailLayout.PaneHeight(height) == 0)
            {
                Message = "The details pane needs a taller screen";
                return;
            }

            FocusPane(PaneKind.Detail, detailList);
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
        FocusedPane == PaneKind.Graph || action == ActionIds.InputAccept || action == ActionIds.Cancel;

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
            // Below the soft row limit HEAD may still come: the rest loads now and the next try finds it.
            session.RequestExpand(true);
            Message = session.IsGraphComplete ? "HEAD is not in the graph" : "HEAD is not loaded yet";
            return;
        }

        Select(graphList, index + ScreenLayout.ReservedLeadingRows);
    }
}
