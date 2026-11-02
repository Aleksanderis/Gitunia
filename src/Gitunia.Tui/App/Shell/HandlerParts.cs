using Gitunia.Platform.ExternalTools;
using Gitunia.Tui.App.Reflog;
using Gitunia.Core.Input;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Conflicts;
using Gitunia.Tui.App.Creation;
using Gitunia.Tui.App.Details;
using Gitunia.Tui.App.ExternalTools;
using Gitunia.Tui.App.Merge;
using Gitunia.Tui.App.Operations;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.App.Remote;
using Gitunia.Tui.App.Reset;
using Gitunia.Tui.App.Settings;
using Gitunia.Tui.App.Stash;
using Gitunia.Tui.App.Tags;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.App.Worktrees;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// The sixth stage: the action handlers, the table that maps each routing case to its handler and the executor that
/// offers an action to them. The table is built once, when every handler exists.
/// </summary>
public sealed class HandlerParts
{

    /// <param name="foundation">The configuration, the state and the widgets.</param>
    /// <param name="panes">The controllers of the panes.</param>
    /// <param name="overlays">The prompts, editors and screens.</param>
    /// <param name="keys">The key controller and the overlay controller.</param>
    public HandlerParts(
        ShellFoundation foundation,
        PaneParts panes,
        OverlayParts overlays,
        KeyParts keys)
    {
        var host = foundation.Host;
        var pause = new ConsolePause();
        Statuses = new StatusActionHandler(foundation.Statuses, foundation.Sessions);
        Staging = new StagingActions(host);
        Tools = new ExternalToolRunner(new ExternalToolLauncher(), foundation.Terminal, foundation.Statuses, host, pause);
        Retry = new TerminalRetry(
            host,
            foundation.Statuses,
            new TerminalRetryHost(host, overlays.Cover, Tools));
        Reset = new ResetActions(host, panes.Rows);
        ReflogEntries = new ReflogEntryStarter(host, Reset, panes.Graph);
        ConfigEditor = new ConfigEditorLauncher(
            new ExternalToolLauncher(),
            foundation.Terminal,
            foundation.Statuses,
            Environment.GetEnvironmentVariable,
            pause);
        PaneSizes = new PaneSizeInput(host, panes.DiffView, panes.WorkingDiff);
        RangeCopier = new RangeCopier(foundation.Clipboard, foundation.Sessions, new ActiveWorkingTree(host));
        Handlers = Register(foundation, panes, overlays, keys).Build();
        Actions = new ActionExecutor(
            Handlers,
            new RepeatStarter(host, foundation.Panes, keys.Repeat, PaneSizes),
            foundation.Sessions);
    }

    /// <summary>The status actions (dismiss, retry).</summary>
    public StatusActionHandler Statuses { get; }

    /// <summary>The staging actions.</summary>
    public StagingActions Staging { get; }

    /// <summary>Runs git tools on the real terminal.</summary>
    public ExternalToolRunner Tools { get; }

    /// <summary>Offers a git command that needs credentials in the real terminal.</summary>
    public TerminalRetry Retry { get; }

    /// <summary>The reset actions.</summary>
    public ResetActions Reset { get; }

    /// <summary>Starts the flow a reflog entry stands for.</summary>
    public ReflogEntryStarter ReflogEntries { get; }

    /// <summary>Opens the config file in the editor.</summary>
    public ConfigEditorLauncher ConfigEditor { get; }

    /// <summary>The pane resize keys.</summary>
    public PaneSizeInput PaneSizes { get; }

    /// <summary>Copies the rows of a Visual range.</summary>
    public RangeCopier RangeCopier { get; }

    /// <summary>The object behind each routing case.</summary>
    public InputHandlers Handlers { get; }

    /// <summary>Offers a routed action to its handlers.</summary>
    public ActionExecutor Actions { get; }

    private InputHandlersBuilder Register(
        ShellFoundation foundation,
        PaneParts panes,
        OverlayParts overlays,
        KeyParts keys)
    {
        var host = foundation.Host;
        var navigation = new NavigationServices(keys.Quit, foundation.Chrome, Statuses, foundation.Clipboard);

        return overlays.Handlers
            .Add(InputHandler.StatusActions, Statuses)
            .Add(InputHandler.Navigation, new NavigationActions(host, foundation.Panes, foundation.Sessions, navigation))
            .Add(InputHandler.Refresh, new RefreshActions(host, foundation.Sessions))
            .Add(InputHandler.Sidebar, panes.Sidebar)
            .Add(InputHandler.Visual, panes.Visual)
            .Add(
                InputHandler.WorkingTree,
                new WorkingTreeActionHandler(
                    host,
                    foundation.Panes,
                    foundation.Clipboard,
                    panes.Commits,
                    new StagingContext(Staging, panes.WorkingDiff, panes.Visual)))
            .Add(InputHandler.Branches, new BranchActions(host, panes.Rows))
            .Add(InputHandler.Stash, new StashActions(host, panes.Stashes))
            .Add(InputHandler.Tags, new TagActions(host, panes.Rows))
            .Add(InputHandler.Sync, new SyncActions(host, panes.Rows))
            .Add(InputHandler.RemoteManagement, new RemoteManagementActions(host, panes.Rows))
            .Add(InputHandler.Reset, Reset)
            .Add(InputHandler.Merge, new MergeActions(host, panes.Rows))
            .Add(InputHandler.Rebase, new RebaseActions(host, panes.Rows, panes.Graph))
            .Add(InputHandler.Compare, panes.Compare)
            .Add(InputHandler.ConflictEditor, new ConflictEditorActions(overlays.ConflictEditor, overlays.Cover))
            .Add(InputHandler.Worktrees, new WorktreeActions(host, panes.Rows))
            .Add(InputHandler.Operations, new OperationActions(host))
            .Add(InputHandler.DetailTabs, new DetailTabInput(host))
            .Add(InputHandler.PaneSizes, PaneSizes)
            .Add(InputHandler.ExternalTools, new ExternalToolInput(host, panes.Graph))
            .Add(InputHandler.WorkspaceActions, panes.WorkspaceActions)
            .Add(InputHandler.IssueLinks, new IssueLinkInput(host, panes.Detail))
            .Add(InputHandler.Overlays, keys.Overlays)
            .Add(InputHandler.Creation, panes.Creation)
            .Add(InputHandler.CreationEscape, new CreationEscapeActions(panes.Creation));
    }
}
