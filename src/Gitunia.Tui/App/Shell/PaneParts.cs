using Gitunia.Tui.App.Compare;
using Gitunia.Tui.App.Creation;
using Gitunia.Tui.App.Details;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Selection;
using Gitunia.Tui.App.Sidebar;
using Gitunia.Tui.App.Stash;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Search;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// The third stage: the controllers of the panes and what they select. They know the list widgets and the state, not
/// the overlays or the chrome around the panes.
/// </summary>
public sealed class PaneParts
{
    /// <param name="foundation">The configuration, the state and the widgets.</param>
    /// <param name="route">Turns a key into an action.</param>
    public PaneParts(ShellFoundation foundation, RouteParts route)
    {
        var host = foundation.Host;
        var panes = foundation.Panes;
        Commits = new CommitMessageInput(host, route.ActionInput);
        Visual = new VisualRangeInput(host, panes);
        DiffView = new DiffPaneView(host, foundation.Sessions, panes, foundation.Chrome, foundation.Clipboard);
        DiffPane = new DiffPaneInput(host, foundation.Sessions, panes, DiffView);
        WorkingDiff = new WorkingDiffController(
            host,
            foundation.Sessions,
            panes,
            foundation.Chrome,
            foundation.Clipboard);
        Detail = new DetailPresenter(host, foundation.Sessions, panes, DiffPane, foundation.Config);
        StashView = new StashViewController(host, Detail, foundation.Statuses);
        Graph = new GraphSelection(foundation.Sessions, foundation.Registry, panes, StashView);
        Rows = new RowSelection(host, panes, Graph);
        Picks = new PickSelectionSource(host, foundation.Sessions, Visual, Rows);
        Stashes = new StashSelection(host, foundation.Sessions, panes, StashView, Rows);
        Jump = new JumpTargets(host, foundation.Sessions, panes, DiffPane, WorkingDiff);
        QuickJump = new QuickJumpInput(host, Jump);
        FileTree = new FileTreeInput(host, panes.Detail);
        Sidebar = new SidebarInput(host, new SidebarLayout(panes, foundation.Geometry));
        Compare = new CompareInput(host, Graph);
        WorkspaceActions = new WorkspaceActions(host);
        Creation = new CreationInput(host);
        Picker = new PickerInput(host);
        ListButtons = new WorkingListButtons(foundation.Geometry, panes);
        Anchors = new MenuAnchors(foundation.Geometry, ListButtons, panes);
        Menus = new RowMenuTargets(panes, Anchors, Graph, Rows);
    }

    /// <summary>The commit message box.</summary>
    public CommitMessageInput Commits { get; }

    /// <summary>The Visual ranges.</summary>
    public VisualRangeInput Visual { get; }

    /// <summary>The history diff pane's content.</summary>
    public DiffPaneView DiffView { get; }

    /// <summary>The history diff pane's keys.</summary>
    public DiffPaneInput DiffPane { get; }

    /// <summary>The Changes view diff.</summary>
    public WorkingDiffController WorkingDiff { get; }

    /// <summary>The detail pane.</summary>
    public DetailPresenter Detail { get; }

    /// <summary>The stash view in the detail pane.</summary>
    public StashViewController StashView { get; }

    /// <summary>The commit selected in the graph.</summary>
    public GraphSelection Graph { get; }

    /// <summary>The sidebar row under the cursor.</summary>
    public RowSelection Rows { get; }

    /// <summary>What a cherry-pick or rebase acts on.</summary>
    public PickSelectionSource Picks { get; }

    /// <summary>What a stash action acts on.</summary>
    public StashSelection Stashes { get; }

    /// <summary>The list the quick jump labels.</summary>
    public JumpTargets Jump { get; }

    /// <summary>The quick jump prompt.</summary>
    public QuickJumpInput QuickJump { get; }

    /// <summary>The file tree of the Files tab.</summary>
    public FileTreeInput FileTree { get; }

    /// <summary>The sidebar keys.</summary>
    public SidebarInput Sidebar { get; }

    /// <summary>The commit comparison.</summary>
    public CompareInput Compare { get; }

    /// <summary>The tab actions.</summary>
    public WorkspaceActions WorkspaceActions { get; }

    /// <summary>The clone and init flows.</summary>
    public CreationInput Creation { get; }

    /// <summary>The repository picker.</summary>
    public PickerInput Picker { get; }

    /// <summary>The buttons on the title rows of the file lists.</summary>
    public WorkingListButtons ListButtons { get; }

    /// <summary>Where the view dropdowns hang.</summary>
    public MenuAnchors Anchors { get; }

    /// <summary>The actions menu of a row.</summary>
    public RowMenuTargets Menus { get; }
}
