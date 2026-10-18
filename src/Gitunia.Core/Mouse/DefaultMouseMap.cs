using Gitunia.Core.Keymap;

namespace Gitunia.Core.Mouse;

/// <summary>The default mouse map, written as data. It mirrors the mouse table in <c>docs/ux.md</c> (a test checks that).</summary>
public static class DefaultMouseMap
{
    /// <summary>The view of the tab row.</summary>
    public const string TabsView = "Tabs";

    /// <summary>The <c>*</c> of a dirty tab chip; a click activates that tab and opens its working-tree view (#331).</summary>
    public const string TabDirtyMarker = "TabDirtyMarker";

    /// <summary>The commands button at the right end of the tab row; a click opens the command palette, the menu of every action.</summary>
    public const string CommandsButton = "CommandsButton";
    /// <summary>The "+" button after the last tab; a click opens the repository picker for a new tab, like <c>Ctrl-t</c>.</summary>
    public const string NewTabButton = "NewTabButton";

    /// <summary>The chips of the in-progress operation banner (#37): each is a view of its own, a click runs the
    /// action its keys run.</summary>
    public const string BannerContinueButton = "BannerButton.Continue";

    public const string BannerAbortButton = "BannerButton.Abort";

    public const string BannerSkipButton = "BannerButton.Skip";

    /// <summary>The view of everything outside an open overlay: a click there closes the overlay.</summary>
    public const string BackdropView = "Backdrop";

    /// <summary>The buttons in the diff pane's title row: each is a view of its own so the map can bind a click to its action.</summary>
    public const string DiffPreviousHunkButton = "DiffButton.PreviousHunk";

    public const string DiffNextHunkButton = "DiffButton.NextHunk";

    public const string DiffWrapButton = "DiffButton.Wrap";

    public const string DiffZoomButton = "DiffButton.Zoom";

    public const string DiffMoreContextButton = "DiffButton.MoreContext";

    public const string DiffLessContextButton = "DiffButton.LessContext";

    public const string DiffWholeFileButton = "DiffButton.WholeFile";

    public const string DiffIgnoreWhitespaceButton = "DiffButton.IgnoreWhitespace";

    public const string DiffSideBySideButton = "DiffButton.SideBySide";

    /// <summary>The staging buttons of the working-tree diff's title row (#343): stage or unstage the hunk, discard it.</summary>
    public const string DiffStageButton = "DiffButton.Stage";

    public const string DiffUnstageButton = "DiffButton.Unstage";

    public const string DiffDiscardButton = "DiffButton.Discard";

    /// <summary>The tab labels in the detail pane's title row: each is a view of its own so the map can bind a click to its action.</summary>
    public const string DetailDetailsTab = "DetailTab.Details";

    public const string DetailChangesTab = "DetailTab.Changes";

    public const string DetailFilesTab = "DetailTab.Files";

    /// <summary>Picker row buttons, shown on the highlighted row.</summary>
    public const string PickerPinButton = "PickerButton.Pin";

    public const string PickerRemoveButton = "PickerButton.Remove";

    /// <summary>The checkboxes and the Commit button in the option row of the working-tree message box.</summary>
    public const string MessageSignOffButton = "MessageButton.SignOff";

    public const string MessageNoVerifyButton = "MessageButton.NoVerify";

    public const string MessageAmendButton = "MessageButton.Amend";

    public const string MessageCommitButton = "MessageButton.Commit";

    /// <summary>The buttons in the title rows of the two working-tree file lists: <c>stage all</c> (Unstaged) and <c>unstage all</c> (Staged).</summary>
    public const string StageAllButton = "ListButton.StageAll";

    public const string UnstageAllButton = "ListButton.UnstageAll";

    /// <summary>The buttons in the Branches header of the sidebar (#414): each acts on the selected row.</summary>
    public const string SidebarBranchNewButton = "SidebarButton.BranchNew";

    public const string SidebarBranchNewOnlyButton = "SidebarButton.BranchNewOnly";

    public const string SidebarBranchSwitchButton = "SidebarButton.BranchSwitch";

    public const string SidebarBranchRenameButton = "SidebarButton.BranchRename";

    public const string SidebarBranchDeleteButton = "SidebarButton.BranchDelete";

    /// <summary>The buttons in the Worktrees header of the sidebar (#412): each acts on the selected row.</summary>
    public const string SidebarWorktreeAddButton = "SidebarButton.WorktreeAdd";

    public const string SidebarWorktreeRemoveButton = "SidebarButton.WorktreeRemove";

    public const string SidebarWorktreeLockButton = "SidebarButton.WorktreeLock";

    public const string SidebarWorktreeUnlockButton = "SidebarButton.WorktreeUnlock";

    public const string SidebarWorktreePruneButton = "SidebarButton.WorktreePrune";

    /// <summary>The buttons in the Tags header (#434): tag actions and the merge of the selected tag.</summary>
    public const string SidebarTagCreateButton = "SidebarButton.TagCreate";

    public const string SidebarTagDeleteButton = "SidebarButton.TagDelete";

    public const string SidebarTagPushButton = "SidebarButton.TagPush";

    public const string SidebarTagDeleteRemoteButton = "SidebarButton.TagDeleteRemote";

    /// <summary>Merge chips of the Branches and Tags headers (#434): merge the selected row.</summary>
    public const string SidebarMergeButton = "SidebarButton.Merge";

    public const string SidebarMergeNoFastForwardButton = "SidebarButton.MergeNoFastForward";

    public const string SidebarMergeSquashButton = "SidebarButton.MergeSquash";

    /// <summary>The two answer rows of a confirm dialog: each is a view of its own so a click on it answers like <c>y</c> or <c>n</c>.</summary>
    public const string ConfirmYesButton = "ConfirmButton.Yes";

    public const string ConfirmNoButton = "ConfirmButton.No";

    /// <summary>List rows moved per wheel notch.</summary>
    public const int WheelLines = 3;

    /// <summary>Views that inherit from <c>Lists</c>; the others stand alone.</summary>
    public static IReadOnlyDictionary<string, string> ViewParents { get; } = new Dictionary<string, string>
    {
        [DefaultKeymap.GraphView] = DefaultKeymap.ListsView,
        [DefaultKeymap.DetailView] = DefaultKeymap.ListsView,
        [DefaultKeymap.SidebarView] = DefaultKeymap.ListsView,
        [DefaultKeymap.DiffView] = DefaultKeymap.ListsView,
        [DefaultKeymap.WorkingUnstagedView] = DefaultKeymap.ListsView,
        [DefaultKeymap.WorkingStagedView] = DefaultKeymap.ListsView,
        [DefaultKeymap.WorkingUnstagedDiffView] = DefaultKeymap.ListsView,
        [DefaultKeymap.WorkingStagedDiffView] = DefaultKeymap.ListsView,
    };

    /// <summary>The views of the overlays: their gestures run even though an open overlay otherwise suppresses the panes behind it.</summary>
    public static IReadOnlyList<string> OverlayViews { get; } =
    [
        DefaultKeymap.HelpView,
        DefaultKeymap.MessagesView,
        DefaultKeymap.ReflogView,
        DefaultKeymap.PaletteView,
        BackdropView,
        ConfirmYesButton,
        ConfirmNoButton,
    ];

    public static MouseMap Create() => new(CreateBindings(), ViewParents);

    public static IReadOnlyList<MouseBinding> CreateBindings() =>
    [
        new(DefaultKeymap.ListsView, MouseGesture.WheelUp, ActionIds.ListUp, WheelLines),
        new(DefaultKeymap.ListsView, MouseGesture.WheelDown, ActionIds.ListDown, WheelLines),
        new(DefaultKeymap.GraphView, MouseGesture.DoubleClick, ActionIds.GraphOpenDetails),
        new(DefaultKeymap.DetailView, MouseGesture.DoubleClick, ActionIds.DetailOpenDiff),
        new(DefaultKeymap.SidebarView, MouseGesture.Click, ActionIds.SidebarActivate),
        new(TabsView, MouseGesture.Click, ActionIds.TabNext, UsesTarget: true),
        new(TabDirtyMarker, MouseGesture.Click, ActionIds.WorkingTreeOpen),
        new(DefaultKeymap.PickerView, MouseGesture.DoubleClick, ActionIds.PickerAccept),
        new(DefaultKeymap.PickerView, MouseGesture.WheelUp, ActionIds.PickerPrevious),
        new(DefaultKeymap.PickerView, MouseGesture.WheelDown, ActionIds.PickerNext),
        new(DefaultKeymap.WorkingUnstagedView, MouseGesture.DoubleClick, ActionIds.WorkingTreeStage),
        new(DefaultKeymap.WorkingStagedView, MouseGesture.DoubleClick, ActionIds.WorkingTreeUnstage),
        new(DefaultKeymap.WorkingMessageView, MouseGesture.Click, ActionIds.WorkingTreeEditMessage),
        new(MessageSignOffButton, MouseGesture.Click, ActionIds.WorkingTreeToggleSignOff),
        new(MessageNoVerifyButton, MouseGesture.Click, ActionIds.WorkingTreeToggleNoVerify),
        new(MessageAmendButton, MouseGesture.Click, ActionIds.WorkingTreeToggleAmend),
        new(MessageCommitButton, MouseGesture.Click, ActionIds.WorkingTreeCommit),
        new(DefaultKeymap.WorkingUnstagedDiffView, MouseGesture.DoubleClick, ActionIds.WorkingTreeStage),
        new(DefaultKeymap.WorkingStagedDiffView, MouseGesture.DoubleClick, ActionIds.WorkingTreeUnstage),
        new(DefaultKeymap.ListsView, MouseGesture.DragStart, ActionIds.VisualToggle),
        new(DefaultKeymap.ListsView, MouseGesture.DragUp, ActionIds.ListUp, UsesTarget: true),
        new(DefaultKeymap.ListsView, MouseGesture.DragDown, ActionIds.ListDown, UsesTarget: true),
        new(SidebarBranchNewButton, MouseGesture.Click, ActionIds.BranchCreate),
        new(SidebarBranchNewOnlyButton, MouseGesture.Click, ActionIds.BranchCreateOnly),
        new(SidebarBranchSwitchButton, MouseGesture.Click, ActionIds.BranchSwitch),
        new(SidebarBranchRenameButton, MouseGesture.Click, ActionIds.BranchRename),
        new(SidebarBranchDeleteButton, MouseGesture.Click, ActionIds.BranchDelete),
        new(SidebarWorktreeAddButton, MouseGesture.Click, ActionIds.WorktreeAdd),
        new(SidebarWorktreeRemoveButton, MouseGesture.Click, ActionIds.WorktreeRemove),
        new(SidebarWorktreeLockButton, MouseGesture.Click, ActionIds.WorktreeLock),
        new(SidebarWorktreeUnlockButton, MouseGesture.Click, ActionIds.WorktreeUnlock),
        new(SidebarWorktreePruneButton, MouseGesture.Click, ActionIds.WorktreePrune),
        new(SidebarTagCreateButton, MouseGesture.Click, ActionIds.TagCreate),
        new(SidebarTagDeleteButton, MouseGesture.Click, ActionIds.TagDelete),
        new(SidebarTagPushButton, MouseGesture.Click, ActionIds.TagPush),
        new(SidebarTagDeleteRemoteButton, MouseGesture.Click, ActionIds.TagDeleteRemote),
        new(SidebarMergeButton, MouseGesture.Click, ActionIds.MergeBranch),
        new(SidebarMergeNoFastForwardButton, MouseGesture.Click, ActionIds.MergeBranchNoFastForward),
        new(SidebarMergeSquashButton, MouseGesture.Click, ActionIds.MergeBranchSquash),
        new(CommandsButton, MouseGesture.Click, ActionIds.PaletteOpen),
        new(NewTabButton, MouseGesture.Click, ActionIds.PickerOpen),
        new(BannerContinueButton, MouseGesture.Click, ActionIds.OperationContinue),
        new(BannerAbortButton, MouseGesture.Click, ActionIds.OperationAbort),
        new(BannerSkipButton, MouseGesture.Click, ActionIds.OperationSkip),
        new(DefaultKeymap.HelpView, MouseGesture.WheelUp, ActionIds.ListUp, WheelLines),
        new(DefaultKeymap.HelpView, MouseGesture.WheelDown, ActionIds.ListDown, WheelLines),
        new(DefaultKeymap.MessagesView, MouseGesture.WheelUp, ActionIds.ListUp, WheelLines),
        new(DefaultKeymap.MessagesView, MouseGesture.WheelDown, ActionIds.ListDown, WheelLines),
        new(DefaultKeymap.ReflogView, MouseGesture.WheelUp, ActionIds.ListUp, WheelLines),
        new(DefaultKeymap.ReflogView, MouseGesture.WheelDown, ActionIds.ListDown, WheelLines),
        new(BackdropView, MouseGesture.Click, ActionIds.Cancel),
        new(DefaultKeymap.PaletteView, MouseGesture.Click, ActionIds.CommandExecute),
        new(DefaultKeymap.PaletteView, MouseGesture.WheelUp, ActionIds.PalettePrevious),
        new(DefaultKeymap.PaletteView, MouseGesture.WheelDown, ActionIds.PaletteNext),
        new(ConfirmYesButton, MouseGesture.Click, ActionIds.ConfirmYes),
        new(ConfirmNoButton, MouseGesture.Click, ActionIds.ConfirmNo),
        new(StageAllButton, MouseGesture.Click, ActionIds.WorkingTreeStageAll),
        new(UnstageAllButton, MouseGesture.Click, ActionIds.WorkingTreeUnstageAll),
        new(DiffPreviousHunkButton, MouseGesture.Click, ActionIds.DiffPreviousHunk),
        new(DiffNextHunkButton, MouseGesture.Click, ActionIds.DiffNextHunk),
        new(DiffWrapButton, MouseGesture.Click, ActionIds.DiffToggleWrap),
        new(DiffZoomButton, MouseGesture.Click, ActionIds.PaneZoom),
        new(DiffMoreContextButton, MouseGesture.Click, ActionIds.DiffMoreContext),
        new(DiffLessContextButton, MouseGesture.Click, ActionIds.DiffLessContext),
        new(DiffWholeFileButton, MouseGesture.Click, ActionIds.DiffToggleWholeFile),
        new(DiffIgnoreWhitespaceButton, MouseGesture.Click, ActionIds.DiffToggleIgnoreWhitespace),
        new(DiffSideBySideButton, MouseGesture.Click, ActionIds.DiffToggleSideBySide),
        new(DiffStageButton, MouseGesture.Click, ActionIds.WorkingTreeStage),
        new(DiffUnstageButton, MouseGesture.Click, ActionIds.WorkingTreeUnstage),
        new(DiffDiscardButton, MouseGesture.Click, ActionIds.WorkingTreeDiscard),
        new(DetailDetailsTab, MouseGesture.Click, ActionIds.DetailTabDetails),
        new(DetailChangesTab, MouseGesture.Click, ActionIds.DetailTabChanges),
        new(DetailFilesTab, MouseGesture.Click, ActionIds.DetailTabFiles),
        new(PickerPinButton, MouseGesture.Click, ActionIds.PickerPin),
        new(PickerRemoveButton, MouseGesture.Click, ActionIds.PickerRemoveRecent),
    ];
}
