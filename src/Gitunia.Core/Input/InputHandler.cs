namespace Gitunia.Core.Input;

/// <summary>
/// Who may take an action, a typed key or a click. <see cref="InputRouting"/> puts them in order; the first that takes
/// the input wins. The terminal layer maps each case to the class that does the work.
/// </summary>
public enum InputHandler
{
    /// <summary>The settings screen.</summary>
    Settings,

    /// <summary>The conflict editor.</summary>
    ConflictEditor,

    /// <summary>The interactive rebase editor.</summary>
    RebaseEditor,

    /// <summary>The reflog list.</summary>
    Reflog,

    /// <summary>The file history.</summary>
    FileHistory,
    /// <summary>The blame view.</summary>
    Blame,

    /// <summary>The help, the palette, the actions menu, the message history and the which-key popup.</summary>
    Overlays,

    /// <summary>Clone and init from the repository picker.</summary>
    Creation,

    /// <summary>Tabs, pinning and the repository picker.</summary>
    WorkspaceActions,

    /// <summary>The open prompt that lives in state (text field, picker, choice).</summary>
    OpenPrompt,

    /// <summary>The quick jump prompt.</summary>
    QuickJump,

    /// <summary>Dismiss, retry of a failed detail load and the merge parent switch.</summary>
    StatusActions,

    /// <summary>Refreshes the active tab.</summary>
    Refresh,

    /// <summary>Open the history of the selected file.</summary>
    FileHistoryOpen,

    /// <summary>Create, switch, rename and delete a branch.</summary>
    Branches,

    /// <summary>Push, apply, pop and drop a stash.</summary>
    Stash,

    /// <summary>Create, delete and push tags.</summary>
    Tags,

    /// <summary>Fetch, pull, push and the cancel of the running transfer.</summary>
    Sync,

    /// <summary>Add, remove, rename and set the URL of a remote.</summary>
    RemoteManagement,

    /// <summary>Merge the selected commit or branch.</summary>
    Merge,

    /// <summary>Cherry-pick and revert.</summary>
    Pick,

    /// <summary>Reset the current branch.</summary>
    Reset,

    /// <summary>Rebase onto a commit or ref and the quick rebase actions.</summary>
    Rebase,

    /// <summary>The Changes view.</summary>
    WorkingTree,

    /// <summary>Difftool, mergetool and the editor.</summary>
    ExternalTools,

    /// <summary>Visual mode (range selection).</summary>
    Visual,

    /// <summary>The pane resize keys.</summary>
    PaneSizes,

    /// <summary>Opening the tracker link of a message line.</summary>
    IssueLinks,

    /// <summary>The Files tab.</summary>
    FileTree,

    /// <summary>The bottom pane tabs.</summary>
    DetailTabs,

    /// <summary>Continue, abort and skip of the operation in progress.</summary>
    Operations,

    /// <summary>Worktree operations.</summary>
    Worktrees,

    /// <summary>The sidebar.</summary>
    Sidebar,

    /// <summary>The history diff pane.</summary>
    Diff,

    /// <summary>The diff of the Changes view.</summary>
    WorkingDiff,

    /// <summary>Jump to a ref.</summary>
    RefJump,

    /// <summary>The search box.</summary>
    Search,

    /// <summary>Marking a commit and comparing it with another.</summary>
    Compare,

    /// <summary>Esc cancels a clone or init that runs.</summary>
    CreationEscape,

    /// <summary>The commit message box; it takes typed keys only.</summary>
    CommitMessage,

    /// <summary>The graph and detail lists: motion, jumps, copy, focus, quit; also what nothing else claimed.</summary>
    Navigation,
}
