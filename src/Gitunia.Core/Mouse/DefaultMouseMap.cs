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

    /// <summary>
    /// The <c>...</c> marker at the right edge of the selected graph or sidebar row;
    /// a click opens the row's actions menu (#479).
    /// </summary>
    public const string RowMenuButton = "RowMenuButton";

    /// <summary>
    /// The fetch, pull and push chips of the tab row, and the cancel chip that replaces them while one runs (#456).
    /// </summary>
    public const string RemoteFetchButton = "RemoteButton.Fetch";

    public const string RemotePullButton = "RemoteButton.Pull";

    public const string RemotePushButton = "RemoteButton.Push";

    public const string RemoteCancelButton = "RemoteButton.Cancel";

    /// <summary>The view of everything outside an open overlay: a click there closes the overlay.</summary>
    public const string BackdropView = "Backdrop";

    /// <summary>The buttons in the diff pane's title row: each is a view of its own so the map can bind a click to its action.</summary>
    public const string DiffPreviousFileButton = "DiffButton.PreviousFile";

    public const string DiffNextFileButton = "DiffButton.NextFile";

    public const string DiffPreviousHunkButton = "DiffButton.PreviousHunk";

    public const string DiffNextHunkButton = "DiffButton.NextHunk";

    public const string DiffWrapButton = "DiffButton.Wrap";

    public const string DiffZoomButton = "DiffButton.Zoom";

    /// <summary>The <c>view ▾</c> dropdown: context, whole file, ignore space, split and wrap (#645).</summary>
    public const string DiffViewButton = "DiffButton.View";

    /// <summary>The staging buttons of the working-tree diff's title row (#343): stage or unstage the hunk, discard it.</summary>
    public const string DiffStageButton = "DiffButton.Stage";

    public const string DiffUnstageButton = "DiffButton.Unstage";

    public const string DiffDiscardButton = "DiffButton.Discard";

    /// <summary>The tab labels in the detail pane's title row: each is a view of its own so the map can bind a click to its action.</summary>
    public const string DetailDetailsTab = "DetailTab.Details";

    public const string DetailChangesTab = "DetailTab.Changes";

    public const string DetailFilesTab = "DetailTab.Files";

    /// <summary>The <c>view ▾</c> dropdown of the Changes and Files tabs' title row (#646).</summary>
    public const string DetailViewButton = "DetailTab.View";

    /// <summary>Picker row buttons, shown on the highlighted row.</summary>
    public const string PickerPinButton = "PickerButton.Pin";

    public const string PickerRemoveButton = "PickerButton.Remove";

    /// <summary>Picker buttons on the hint line: clone a repository, initialise a directory.</summary>
    public const string PickerCloneButton = "PickerButton.Clone";

    public const string PickerInitButton = "PickerButton.Init";

    /// <summary>The checkboxes and the Commit button in the option row of the working-tree message box.</summary>
    public const string MessageSignOffButton = "MessageButton.SignOff";

    public const string MessageNoVerifyButton = "MessageButton.NoVerify";

    public const string MessageAmendButton = "MessageButton.Amend";

    public const string MessageCommitButton = "MessageButton.Commit";

    /// <summary>The buttons in the title rows of the two working-tree file lists: <c>stage all</c> (Unstaged) and <c>unstage all</c> (Staged).</summary>
    public const string StageAllButton = "ListButton.StageAll";

    public const string UnstageAllButton = "ListButton.UnstageAll";

    /// <summary>The <c>view ▾</c> dropdown in the title row of both working-tree file lists (#646).</summary>
    public const string ListViewButton = "ListButton.View";

    /// <summary>The <c>diff ›</c> chip in the title row of both working-tree file lists, shown on screens under 80
    /// columns where the diff is not beside the list (#376).</summary>
    public const string ListDiffButton = "ListButton.Diff";

    /// <summary>The <c>resolve</c> chip in the Unstaged list's title row, shown while the cursor is on a conflicted
    /// file (#701).</summary>
    public const string ListResolveButton = "ListButton.Resolve";

    /// <summary>The action rows under the reflog list (#442) and a row of its ref picker (#441): each is a view of
    /// its own, a click runs the action its key runs on the highlighted entry.</summary>
    public const string ReflogSelectButton = "ReflogButton.Select";

    public const string ReflogResetButton = "ReflogButton.Reset";

    public const string ReflogBranchButton = "ReflogButton.Branch";

    public const string ReflogCheckoutButton = "ReflogButton.Checkout";

    public const string ReflogPickerRow = "ReflogPicker";

    /// <summary>The chips under the rebase todo editor (#458): each is a view of its own, a click runs the action
    /// its key runs on the highlighted commit.</summary>
    public const string RebasePickButton = "RebaseButton.Pick";

    public const string RebaseRewordButton = "RebaseButton.Reword";

    public const string RebaseSquashButton = "RebaseButton.Squash";

    public const string RebaseFixupButton = "RebaseButton.Fixup";

    public const string RebaseDropButton = "RebaseButton.Drop";

    public const string RebaseMoveUpButton = "RebaseButton.Up";

    public const string RebaseMoveDownButton = "RebaseButton.Down";

    public const string RebaseStartButton = "RebaseButton.Start";

    public const string RebaseCancelButton = "RebaseButton.Cancel";

    /// <summary>The chips under the conflict editor (#689): each is a view of its own, a click runs the action its
    /// key runs on the hunk under the cursor.</summary>
    public const string ConflictOursButton = "ConflictButton.Ours";

    public const string ConflictTheirsButton = "ConflictButton.Theirs";

    public const string ConflictBothButton = "ConflictButton.Both";

    public const string ConflictAllOursButton = "ConflictButton.AllOurs";

    public const string ConflictAllTheirsButton = "ConflictButton.AllTheirs";

    public const string ConflictUndoButton = "ConflictButton.Undo";

    public const string ConflictNextButton = "ConflictButton.Next";

    public const string ConflictPreviousButton = "ConflictButton.Previous";

    public const string ConflictSaveButton = "ConflictButton.Save";

    public const string ConflictCancelButton = "ConflictButton.Cancel";

    public const string ConflictRedoButton = "ConflictButton.Redo";

    public const string ConflictUntakeButton = "ConflictButton.Untake";

    public const string ConflictEditButton = "ConflictButton.Edit";

    public const string ConflictBaseButton = "ConflictButton.Base";

    public const string ConflictRecreateButton = "ConflictButton.Recreate";

    public const string ConflictEditDoneButton = "ConflictButton.EditDone";

    public const string ConflictTakeButton = "ConflictButton.Take";

    public const string ConflictPaneButton = "ConflictButton.Pane";

    public const string ConflictSelectButton = "ConflictButton.Select";

    /// <summary>The setting row, the <c>[+]</c> marker and the <c>[x]</c> marker of the settings screen (#523):
    /// each is a view of its own, a click runs the action its key runs on the row the click selected.</summary>
    public const string SettingsEditButton = "SettingsButton.Edit";

    public const string SettingsAddButton = "SettingsButton.Add";

    public const string SettingsRemoveButton = "SettingsButton.Remove";

    /// <summary>A tab label of the help overlay (#652); the click target is the tab's number, from 1.</summary>
    public const string HelpTabLabel = "HelpTab";

    /// <summary>A parent row of the mainline overlay of a cherry-pick or revert of a merge commit (#449); the click
    /// target is the parent's number.</summary>
    public const string MainlineParentRow = "MainlineParent";

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
        [DefaultKeymap.DetailChangesView] = DefaultKeymap.DetailView,
        [DefaultKeymap.DetailFilesView] = DefaultKeymap.DetailView,
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
        DefaultKeymap.RebaseEditorView,
        RebasePickButton,
        RebaseRewordButton,
        RebaseSquashButton,
        RebaseFixupButton,
        RebaseDropButton,
        RebaseMoveUpButton,
        RebaseMoveDownButton,
        RebaseStartButton,
        RebaseCancelButton,
        DefaultKeymap.ConflictSideView,
        DefaultKeymap.ConflictResultView,
        ConflictOursButton,
        ConflictTheirsButton,
        ConflictBothButton,
        ConflictAllOursButton,
        ConflictAllTheirsButton,
        ConflictUndoButton,
        ConflictNextButton,
        ConflictPreviousButton,
        ConflictSaveButton,
        ConflictCancelButton,
        ConflictRedoButton,
        ConflictUntakeButton,
        ConflictEditButton,
        ConflictBaseButton,
        ConflictRecreateButton,
        ConflictEditDoneButton,
        ConflictTakeButton,
        ConflictPaneButton,
        ConflictSelectButton,
        DefaultKeymap.SettingsView,
        SettingsEditButton,
        SettingsAddButton,
        SettingsRemoveButton,
        DefaultKeymap.PaletteView,
        DefaultKeymap.ContextMenuView,
        BackdropView,
        ReflogSelectButton,
        ReflogResetButton,
        ReflogBranchButton,
        ReflogCheckoutButton,
        ReflogPickerRow,
        MainlineParentRow,
        HelpTabLabel,
        ConfirmYesButton,
        ConfirmNoButton,
    ];

    public static MouseMap Create() => new(CreateBindings(), ViewParents);

    public static IReadOnlyList<MouseBinding> CreateBindings() =>
    [
        new(DefaultKeymap.ListsView, MouseGesture.WheelUp, ActionIds.ListUp, WheelLines),
        new(DefaultKeymap.ListsView, MouseGesture.WheelDown, ActionIds.ListDown, WheelLines),
        new(DefaultKeymap.GraphView, MouseGesture.DoubleClick, ActionIds.MenuOpenForRow),
        new(DefaultKeymap.DetailView, MouseGesture.DoubleClick, ActionIds.DetailOpenDiff),
        new(DefaultKeymap.DetailView, MouseGesture.Click, ActionIds.DetailToggleFolder),
        new(DefaultKeymap.SidebarView, MouseGesture.Click, ActionIds.SidebarActivate),
        new(TabsView, MouseGesture.Click, ActionIds.TabNext, UsesTarget: true),
        new(TabDirtyMarker, MouseGesture.Click, ActionIds.WorkingTreeOpen),
        new(DefaultKeymap.PickerView, MouseGesture.DoubleClick, ActionIds.PickerAccept),
        new(DefaultKeymap.PickerView, MouseGesture.WheelUp, ActionIds.PickerPrevious),
        new(DefaultKeymap.PickerView, MouseGesture.WheelDown, ActionIds.PickerNext),
        new(DefaultKeymap.WorkingUnstagedView, MouseGesture.Click, ActionIds.WorkingTreeToggleFolder),
        new(DefaultKeymap.WorkingStagedView, MouseGesture.Click, ActionIds.WorkingTreeToggleFolder),
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
        new(RowMenuButton, MouseGesture.Click, ActionIds.MenuOpenForRow),
        new(CommandsButton, MouseGesture.Click, ActionIds.PaletteOpen),
        new(NewTabButton, MouseGesture.Click, ActionIds.PickerOpen),
        new(RemoteFetchButton, MouseGesture.Click, ActionIds.RemoteFetch),
        new(RemotePullButton, MouseGesture.Click, ActionIds.RemotePull),
        new(RemotePushButton, MouseGesture.Click, ActionIds.RemotePush),
        new(RemoteCancelButton, MouseGesture.Click, ActionIds.RemoteCancel),
        new(BannerContinueButton, MouseGesture.Click, ActionIds.OperationContinue),
        new(BannerAbortButton, MouseGesture.Click, ActionIds.OperationAbort),
        new(BannerSkipButton, MouseGesture.Click, ActionIds.OperationSkip),
        new(DefaultKeymap.HelpView, MouseGesture.WheelUp, ActionIds.ListUp, WheelLines),
        new(DefaultKeymap.HelpView, MouseGesture.WheelDown, ActionIds.ListDown, WheelLines),
        new(DefaultKeymap.MessagesView, MouseGesture.WheelUp, ActionIds.ListUp, WheelLines),
        new(DefaultKeymap.MessagesView, MouseGesture.WheelDown, ActionIds.ListDown, WheelLines),
        new(DefaultKeymap.ReflogView, MouseGesture.WheelUp, ActionIds.ListUp, WheelLines),
        new(DefaultKeymap.ReflogView, MouseGesture.WheelDown, ActionIds.ListDown, WheelLines),
        new(DefaultKeymap.RebaseEditorView, MouseGesture.WheelUp, ActionIds.ListUp, WheelLines),
        new(DefaultKeymap.RebaseEditorView, MouseGesture.WheelDown, ActionIds.ListDown, WheelLines),
        new(RebasePickButton, MouseGesture.Click, ActionIds.RebaseEditorPick),
        new(RebaseRewordButton, MouseGesture.Click, ActionIds.RebaseEditorReword),
        new(RebaseSquashButton, MouseGesture.Click, ActionIds.RebaseEditorSquash),
        new(RebaseFixupButton, MouseGesture.Click, ActionIds.RebaseEditorFixup),
        new(RebaseDropButton, MouseGesture.Click, ActionIds.RebaseEditorDrop),
        new(RebaseMoveUpButton, MouseGesture.Click, ActionIds.RebaseEditorMoveUp),
        new(RebaseMoveDownButton, MouseGesture.Click, ActionIds.RebaseEditorMoveDown),
        new(RebaseStartButton, MouseGesture.Click, ActionIds.RebaseEditorStart),
        new(RebaseCancelButton, MouseGesture.Click, ActionIds.NavigateBack),
        new(DefaultKeymap.ConflictSideView, MouseGesture.WheelUp, ActionIds.ListUp, WheelLines),
        new(DefaultKeymap.ConflictSideView, MouseGesture.WheelDown, ActionIds.ListDown, WheelLines),
        new(DefaultKeymap.ConflictResultView, MouseGesture.WheelUp, ActionIds.ListUp, WheelLines),
        new(DefaultKeymap.ConflictResultView, MouseGesture.WheelDown, ActionIds.ListDown, WheelLines),
        new(ConflictOursButton, MouseGesture.Click, ActionIds.ConflictEditorOurs),
        new(ConflictTheirsButton, MouseGesture.Click, ActionIds.ConflictEditorTheirs),
        new(ConflictBothButton, MouseGesture.Click, ActionIds.ConflictEditorBoth),
        new(ConflictAllOursButton, MouseGesture.Click, ActionIds.ConflictEditorAllOurs),
        new(ConflictAllTheirsButton, MouseGesture.Click, ActionIds.ConflictEditorAllTheirs),
        new(ConflictUndoButton, MouseGesture.Click, ActionIds.ConflictEditorUndo),
        new(ConflictNextButton, MouseGesture.Click, ActionIds.ConflictEditorNext),
        new(ConflictPreviousButton, MouseGesture.Click, ActionIds.ConflictEditorPrevious),
        new(ConflictSaveButton, MouseGesture.Click, ActionIds.ConflictEditorSave),
        new(ConflictCancelButton, MouseGesture.Click, ActionIds.NavigateBack),
        new(ConflictRedoButton, MouseGesture.Click, ActionIds.ConflictEditorRedo),
        new(ConflictUntakeButton, MouseGesture.Click, ActionIds.ConflictEditorUntake),
        new(ConflictEditButton, MouseGesture.Click, ActionIds.ConflictEditorEdit),
        new(ConflictBaseButton, MouseGesture.Click, ActionIds.ConflictEditorBase),
        new(ConflictRecreateButton, MouseGesture.Click, ActionIds.ConflictEditorRecreate),
        new(ConflictEditDoneButton, MouseGesture.Click, ActionIds.ConflictEditorEditDone),
        new(ConflictTakeButton, MouseGesture.Click, ActionIds.ConflictEditorTake),
        new(ConflictPaneButton, MouseGesture.Click, ActionIds.PaneNext),
        new(ConflictSelectButton, MouseGesture.Click, ActionIds.VisualToggle),
        new(DefaultKeymap.SettingsView, MouseGesture.WheelUp, ActionIds.ListUp, WheelLines),
        new(DefaultKeymap.SettingsView, MouseGesture.WheelDown, ActionIds.ListDown, WheelLines),
        new(SettingsEditButton, MouseGesture.Click, ActionIds.SettingsActivate),
        new(SettingsAddButton, MouseGesture.Click, ActionIds.SettingsAdd),
        new(SettingsRemoveButton, MouseGesture.Click, ActionIds.SettingsRemove),
        new(ReflogSelectButton, MouseGesture.Click, ActionIds.ReflogSelectCommit),
        new(ReflogResetButton, MouseGesture.Click, ActionIds.ResetBranch),
        new(ReflogBranchButton, MouseGesture.Click, ActionIds.BranchCreate),
        new(ReflogCheckoutButton, MouseGesture.Click, ActionIds.BranchSwitch),
        new(ReflogPickerRow, MouseGesture.Click, ActionIds.InputAccept),
        new(MainlineParentRow, MouseGesture.Click, ActionIds.PickMainline, UsesTarget: true),
        new(HelpTabLabel, MouseGesture.Click, ActionIds.HelpTabSelect, UsesTarget: true),
        new(BackdropView, MouseGesture.Click, ActionIds.Cancel),
        new(DefaultKeymap.PaletteView, MouseGesture.Click, ActionIds.CommandExecute),
        new(DefaultKeymap.PaletteView, MouseGesture.WheelUp, ActionIds.PalettePrevious),
        new(DefaultKeymap.PaletteView, MouseGesture.WheelDown, ActionIds.PaletteNext),
        new(DefaultKeymap.ContextMenuView, MouseGesture.Click, ActionIds.CommandExecute),
        new(DefaultKeymap.ContextMenuView, MouseGesture.WheelUp, ActionIds.PalettePrevious),
        new(DefaultKeymap.ContextMenuView, MouseGesture.WheelDown, ActionIds.PaletteNext),
        new(ConfirmYesButton, MouseGesture.Click, ActionIds.ConfirmYes),
        new(ConfirmNoButton, MouseGesture.Click, ActionIds.ConfirmNo),
        new(StageAllButton, MouseGesture.Click, ActionIds.WorkingTreeStageAll),
        new(UnstageAllButton, MouseGesture.Click, ActionIds.WorkingTreeUnstageAll),
        new(ListResolveButton, MouseGesture.Click, ActionIds.ConflictOpen),
        new(ListDiffButton, MouseGesture.Click, ActionIds.WorkingTreeOpenDiff),
        new(DiffPreviousFileButton, MouseGesture.Click, ActionIds.DiffPreviousFile),
        new(DiffNextFileButton, MouseGesture.Click, ActionIds.DiffNextFile),
        new(DiffPreviousHunkButton, MouseGesture.Click, ActionIds.DiffPreviousHunk),
        new(DiffNextHunkButton, MouseGesture.Click, ActionIds.DiffNextHunk),
        new(DiffWrapButton, MouseGesture.Click, ActionIds.DiffToggleWrap),
        new(DiffZoomButton, MouseGesture.Click, ActionIds.PaneZoom),
        new(DiffViewButton, MouseGesture.Click, ActionIds.DiffOpenViewOptions),
        new(DiffStageButton, MouseGesture.Click, ActionIds.WorkingTreeStage),
        new(DiffUnstageButton, MouseGesture.Click, ActionIds.WorkingTreeUnstage),
        new(DiffDiscardButton, MouseGesture.Click, ActionIds.WorkingTreeDiscard),
        new(DetailDetailsTab, MouseGesture.Click, ActionIds.DetailTabDetails),
        new(DetailChangesTab, MouseGesture.Click, ActionIds.DetailTabChanges),
        new(DetailFilesTab, MouseGesture.Click, ActionIds.DetailTabFiles),
        new(DetailViewButton, MouseGesture.Click, ActionIds.ListOpenViewOptions),
        new(ListViewButton, MouseGesture.Click, ActionIds.ListOpenViewOptions),
        new(PickerPinButton, MouseGesture.Click, ActionIds.PickerPin),
        new(PickerRemoveButton, MouseGesture.Click, ActionIds.PickerRemoveRecent),
        new(PickerCloneButton, MouseGesture.Click, ActionIds.PickerClone),
        new(PickerInitButton, MouseGesture.Click, ActionIds.PickerInit),
    ];
}
