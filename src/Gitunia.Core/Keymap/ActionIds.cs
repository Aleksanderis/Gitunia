using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Keymap;

/// <summary>Identifiers of the actions the default keymap binds. The state layer maps them to commands.</summary>
public static class ActionIds
{
    public static ActionId Cancel { get; } = new("cancel");

    public static ActionId CommandExecute { get; } = new("command.execute");

    public static ActionId ConfirmNo { get; } = new("confirm.no");

    public static ActionId ConfirmYes { get; } = new("confirm.yes");

    public static ActionId DetailCollapse { get; } = new("detail.collapse");

    public static ActionId DetailExpand { get; } = new("detail.expand");

    public static ActionId DetailCopyPath { get; } = new("detail.copy-path");

    public static ActionId DetailFilter { get; } = new("detail.filter");

    public static ActionId DetailNextTab { get; } = new("detail.next-tab");

    public static ActionId DetailOpenDiff { get; } = new("detail.open-diff");

    public static ActionId DetailOpenLink { get; } = new("detail.open-link");

    public static ActionId DetailPreviousTab { get; } = new("detail.previous-tab");

    public static ActionId DetailRetry { get; } = new("detail.retry");

    public static ActionId DetailToggleFolder { get; } = new("detail.toggle-folder");

    public static ActionId DetailTabChanges { get; } = new("detail.tab-changes");

    public static ActionId DetailTabDetails { get; } = new("detail.tab-details");

    public static ActionId DetailTabFiles { get; } = new("detail.tab-files");

    public static ActionId FilesTogglePathStyle { get; } = new("files.toggle-path-style");

    public static ActionId FilesPathNameFirst { get; } = new("files.path-name-first");

    public static ActionId FilesPathFullPath { get; } = new("files.path-full-path");

    public static ActionId FilesPathTree { get; } = new("files.path-tree");

    public static ActionId ListOpenViewOptions { get; } = new("files.open-view-options");

    public static ActionId DiffCopyFile { get; } = new("diff.copy-file");

    public static ActionId DiffCopyLine { get; } = new("diff.copy-line");

    public static ActionId DiffOpenViewOptions { get; } = new("diff.open-view-options");

    public static ActionId DiffLoadAnyway { get; } = new("diff.load-anyway");

    public static ActionId DiffMergeParent { get; } = new("diff.merge-parent");
    public static ActionId DiffLessContext { get; } = new("diff.less-context");

    public static ActionId DiffMoreContext { get; } = new("diff.more-context");

    public static ActionId DiffNextFile { get; } = new("diff.next-file");

    public static ActionId DiffNextHunk { get; } = new("diff.next-hunk");

    public static ActionId DiffPreviousFile { get; } = new("diff.previous-file");

    public static ActionId DiffPreviousHunk { get; } = new("diff.previous-hunk");

    public static ActionId DiffScrollDown { get; } = new("diff.scroll-down");

    public static ActionId DiffScrollHalfPageDown { get; } = new("diff.scroll-half-page-down");

    public static ActionId DiffScrollHalfPageUp { get; } = new("diff.scroll-half-page-up");

    public static ActionId DiffScrollUp { get; } = new("diff.scroll-up");

    public static ActionId DiffSearchNext { get; } = new("diff.search-next");

    public static ActionId DiffSearchOpen { get; } = new("diff.search-open");

    public static ActionId DiffSearchPrevious { get; } = new("diff.search-previous");

    public static ActionId DiffToggleIgnoreWhitespace { get; } = new("diff.toggle-ignore-whitespace");

    public static ActionId DiffToggleSideBySide { get; } = new("diff.toggle-side-by-side");

    public static ActionId DiffToggleWhitespace { get; } = new("diff.toggle-whitespace");

    public static ActionId DiffToggleWholeFile { get; } = new("diff.toggle-whole-file");

    public static ActionId DiffToggleWrap { get; } = new("diff.toggle-wrap");

    public static ActionId GraphCopyHash { get; } = new("graph.copy-hash");

    public static ActionId GraphCompare { get; } = new("graph.compare");

    public static ActionId GraphJumpToHead { get; } = new("graph.jump-to-head");

    public static ActionId GraphJumpToRef { get; } = new("graph.jump-to-ref");

    public static ActionId QuickJump { get; } = new("view.quick-jump-to-row");

    public static ActionId ViewCycleCommandLevel { get; } = new("view.cycle-command-level");

    public static ActionId GraphOpenDetails { get; } = new("graph.open-details");

    public static ActionId HelpSearch { get; } = new("help.search");

    public static ActionId HelpShow { get; } = new("help.show");

    public static ActionId HelpTabNext { get; } = new("help.tab-next");

    public static ActionId HelpTabPrevious { get; } = new("help.tab-previous");

    public static ActionId HelpTabSelect { get; } = new("help.tab-select");

    public static ActionId InputAccept { get; } = new("input.accept");

    public static ActionId ListBottom { get; } = new("list.bottom");

    public static ActionId ListDown { get; } = new("list.down");

    public static ActionId ListHalfPageDown { get; } = new("list.half-page-down");

    public static ActionId ListHalfPageUp { get; } = new("list.half-page-up");

    public static ActionId ListTop { get; } = new("list.top");

    public static ActionId ListUp { get; } = new("list.up");

    public static ActionId AppQuit { get; } = new("app.quit");

    public static ActionId NavigateBack { get; } = new("navigate.back");

    public static ActionId OperationContinue { get; } = new("operation.continue");

    public static ActionId OperationAbort { get; } = new("operation.abort");

    public static ActionId OperationSkip { get; } = new("operation.skip");

    public static ActionId PaletteNext { get; } = new("palette.next");

    public static ActionId PaletteOpen { get; } = new("palette.open");

    public static ActionId PalettePrevious { get; } = new("palette.previous");

    public static ActionId PaneFocusDown { get; } = new("pane.focus-down");

    public static ActionId PaneFocusLeft { get; } = new("pane.focus-left");

    public static ActionId PaneFocusRight { get; } = new("pane.focus-right");

    public static ActionId PaneFocusUp { get; } = new("pane.focus-up");

    public static ActionId PaneNext { get; } = new("pane.next");

    public static ActionId PanePrevious { get; } = new("pane.previous");

    public static ActionId PaneZoom { get; } = new("pane.zoom");

    public static ActionId PaneWider { get; } = new("pane.wider");

    public static ActionId PaneNarrower { get; } = new("pane.narrower");

    public static ActionId PaneTaller { get; } = new("pane.taller");

    public static ActionId PaneShorter { get; } = new("pane.shorter");

    public static ActionId PaneResetSizes { get; } = new("pane.reset-sizes");

    public static ActionId RepeatEnd { get; } = new("repeat.end");

    public static ActionId PickerAccept { get; } = new("picker.accept");

    public static ActionId BranchCreate { get; } = new("branch.create");

    public static ActionId BranchCreateOnly { get; } = new("branch.create-only");

    public static ActionId BranchDelete { get; } = new("branch.delete");

    public static ActionId BranchRename { get; } = new("branch.rename");

    public static ActionId BranchSetUpstream { get; } = new("branch.set-upstream");

    public static ActionId BranchUnsetUpstream { get; } = new("branch.unset-upstream");

    public static ActionId BranchFastForward { get; } = new("branch.fast-forward");

    public static ActionId BranchSwitch { get; } = new("branch.switch");

    public static ActionId TagCreate { get; } = new("tag.create");

    public static ActionId TagDelete { get; } = new("tag.delete");

    public static ActionId TagDeleteRemote { get; } = new("tag.delete-remote");

    public static ActionId TagPush { get; } = new("tag.push");

    public static ActionId RemoteAdd { get; } = new("remote.add");

    public static ActionId RemoteRemove { get; } = new("remote.remove");

    public static ActionId RemoteRename { get; } = new("remote.rename");

    public static ActionId RemoteSetUrl { get; } = new("remote.set-url");

    public static ActionId RemoteFetch { get; } = new("remote.fetch");

    public static ActionId RemoteCancel { get; } = new("remote.cancel");

    public static ActionId RemoteFetchAll { get; } = new("remote.fetch-all");

    public static ActionId RemotePull { get; } = new("remote.pull");

    public static ActionId RemotePush { get; } = new("remote.push");

    public static ActionId RemotePushForce { get; } = new("remote.push-force");

    public static ActionId RemoteDeleteBranch { get; } = new("remote.delete-branch");

    public static ActionId MenuOpenForRow { get; } = new("menu.open-for-row");

    public static ActionId MergeBranch { get; } = new("merge.branch");

    public static ActionId MergeBranchNoFastForward { get; } = new("merge.branch-no-ff");

    public static ActionId MergeBranchSquash { get; } = new("merge.branch-squash");

    public static ActionId PickCherry { get; } = new("pick.cherry-pick");

    public static ActionId PickRevert { get; } = new("pick.revert");

    public static ActionId PickMainline { get; } = new("pick.mainline");

    public static ActionId ResetBranch { get; } = new("reset.branch");

    public static ActionId RebaseOnto { get; } = new("rebase.onto");

    public static ActionId RebaseInteractive { get; } = new("rebase.interactive");

    public static ActionId RebaseInteractiveRoot { get; } = new("rebase.interactive-root");

    public static ActionId RebaseEditorPick { get; } = new("rebase-editor.pick");

    public static ActionId RebaseEditorReword { get; } = new("rebase-editor.reword");

    public static ActionId RebaseEditorSquash { get; } = new("rebase-editor.squash");

    public static ActionId RebaseEditorFixup { get; } = new("rebase-editor.fixup");

    public static ActionId RebaseEditorDrop { get; } = new("rebase-editor.drop");

    public static ActionId RebaseEditorMoveUp { get; } = new("rebase-editor.move-up");

    public static ActionId RebaseEditorMoveDown { get; } = new("rebase-editor.move-down");

    public static ActionId RebaseEditorStart { get; } = new("rebase-editor.start");

    public static ActionId RebaseEditorMessageSave { get; } = new("rebase-editor.message-save");

    public static ActionId RebaseEditorMessageNewLine { get; } = new("rebase-editor.message-newline");

    public static ActionId ConflictOpen { get; } = new("conflict.open");

    public static ActionId ConflictEditorNext { get; } = new("conflict-editor.next");

    public static ActionId ConflictEditorPrevious { get; } = new("conflict-editor.previous");

    public static ActionId ConflictEditorOurs { get; } = new("conflict-editor.ours");

    public static ActionId ConflictEditorTheirs { get; } = new("conflict-editor.theirs");

    public static ActionId ConflictEditorBoth { get; } = new("conflict-editor.both");

    public static ActionId ConflictEditorAllOurs { get; } = new("conflict-editor.all-ours");

    public static ActionId ConflictEditorAllTheirs { get; } = new("conflict-editor.all-theirs");

    public static ActionId ConflictEditorUndo { get; } = new("conflict-editor.undo");

    public static ActionId ConflictEditorSave { get; } = new("conflict-editor.save");

    public static ActionId ConflictEditorUntake { get; } = new("conflict-editor.untake");

    public static ActionId ConflictEditorRedo { get; } = new("conflict-editor.redo");

    public static ActionId ConflictEditorEdit { get; } = new("conflict-editor.edit");

    public static ActionId ConflictEditorTake { get; } = new("conflict-editor.take");

    public static ActionId ConflictEditorBase { get; } = new("conflict-editor.base");

    public static ActionId ConflictEditorRecreate { get; } = new("conflict-editor.recreate");

    public static ActionId ConflictEditorEditDone { get; } = new("conflict-editor.edit-done");

    public static ActionId ConflictEditorEditNewLine { get; } = new("conflict-editor.edit-newline");

    public static ActionId ConflictEditorScrollLeft { get; } = new("conflict-editor.scroll-left");

    public static ActionId ConflictEditorScrollRight { get; } = new("conflict-editor.scroll-right");

    public static ActionId RebaseFixup { get; } = new("rebase.fixup");

    public static ActionId RebaseReword { get; } = new("rebase.reword");

    public static ActionId RebaseDrop { get; } = new("rebase.drop");

    public static ActionId FileHistoryOpen { get; } = new("file-history.open");

    public static ActionId FileHistorySelectCommit { get; } = new("file-history.select-commit");
    public static ActionId BlameOpen { get; } = new("blame.open");

    public static ActionId BlameDetectCopies { get; } = new("blame.toggle-copies");

    public static ActionId BlameDetectMoves { get; } = new("blame.toggle-moves");

    public static ActionId BlameIgnoreWhitespace { get; } = new("blame.toggle-whitespace");

    public static ActionId BlameParent { get; } = new("blame.parent");

    public static ActionId BlameShowCommit { get; } = new("blame.show-commit");

    public static ActionId ReflogOpen { get; } = new("reflog.open");

    public static ActionId ReflogSelectCommit { get; } = new("reflog.select-commit");

    public static ActionId SettingsOpen { get; } = new("settings.open");

    public static ActionId SettingsOpenTheme { get; } = new("settings.open-theme");

    public static ActionId SettingsActivate { get; } = new("settings.activate");

    public static ActionId SettingsToggle { get; } = new("settings.toggle");

    public static ActionId SettingsFilter { get; } = new("settings.filter");

    public static ActionId SettingsAdd { get; } = new("settings.add");

    public static ActionId SettingsRemove { get; } = new("settings.remove");

    public static ActionId SettingsMoveDown { get; } = new("settings.move-down");

    public static ActionId SettingsMoveUp { get; } = new("settings.move-up");

    public static ActionId SettingsIncrease { get; } = new("settings.increase");

    public static ActionId SettingsDecrease { get; } = new("settings.decrease");

    public static ActionId PickerClone { get; } = new("picker.clone");

    public static ActionId PickerInit { get; } = new("picker.init");

    public static ActionId PickerCompletePath { get; } = new("picker.complete-path");

    public static ActionId PickerNext { get; } = new("picker.next");

    public static ActionId PickerOpen { get; } = new("picker.open");

    public static ActionId PickerPin { get; } = new("picker.pin");

    public static ActionId PickerPrevious { get; } = new("picker.previous");

    public static ActionId PickerRemoveRecent { get; } = new("picker.remove-recent");

    public static ActionId RepositoryPin { get; } = new("repo.pin");

    public static ActionId RepositoryRefresh { get; } = new("repo.refresh");

    public static ActionId ToolCommitEditor { get; } = new("tool.commit-editor");

    public static ActionId ToolDifftool { get; } = new("tool.difftool");

    public static ActionId ToolMergetool { get; } = new("tool.mergetool");

    public static ActionId SearchNext { get; } = new("search.next");

    public static ActionId SearchOpen { get; } = new("search.open");

    public static ActionId SearchPrevious { get; } = new("search.previous");

    public static ActionId SidebarActivate { get; } = new("sidebar.activate");

    public static ActionId SidebarCollapse { get; } = new("sidebar.collapse");

    public static ActionId SidebarCopy { get; } = new("sidebar.copy");

    public static ActionId SidebarExpand { get; } = new("sidebar.expand");

    public static ActionId SidebarFilter { get; } = new("sidebar.filter");

    public static ActionId StashPush { get; } = new("stash.push");

    public static ActionId StashPushUntracked { get; } = new("stash.push-untracked");

    public static ActionId StashApply { get; } = new("stash.apply");

    public static ActionId StashPop { get; } = new("stash.pop");

    public static ActionId StashDrop { get; } = new("stash.drop");

    public static ActionId StashRestoreFile { get; } = new("stash.restore-file");

    public static ActionId StatusDismiss { get; } = new("status.dismiss");

    public static ActionId StatusHistory { get; } = new("status.history");

    public static ActionId TabClose { get; } = new("tab.close");

    public static ActionId TabLastActive { get; } = new("tab.last-active");

    public static ActionId TabNext { get; } = new("tab.next");

    public static ActionId TabPrevious { get; } = new("tab.previous");

    public static ActionId TabRename { get; } = new("tab.rename");

    public static ActionId VisualCopy { get; } = new("visual.copy");

    public static ActionId VisualToggle { get; } = new("visual.toggle");

    public static ActionId WorktreeAdd { get; } = new("worktree.add");

    public static ActionId WorktreeLock { get; } = new("worktree.lock");

    public static ActionId WorktreeOpen { get; } = new("worktree.open");

    public static ActionId WorktreePrune { get; } = new("worktree.prune");

    public static ActionId WorktreeRemove { get; } = new("worktree.remove");

    public static ActionId WorktreeSwitch { get; } = new("worktree.switch");

    public static ActionId WorktreeUnlock { get; } = new("worktree.unlock");

    public static ActionId WorkingTreeCopyPath { get; } = new("working-tree.copy-path");

    public static ActionId WorkingTreeOpen { get; } = new("working-tree.open");

    public static ActionId WorkingTreeOpenDiff { get; } = new("working-tree.open-diff");

    public static ActionId WorkingTreeToggleFolder { get; } = new("working-tree.toggle-folder");

    public static ActionId WorkingTreeCollapse { get; } = new("working-tree.collapse");

    public static ActionId WorkingTreeExpand { get; } = new("working-tree.expand");

    public static ActionId WorkingTreeToggle { get; } = new("working-tree.toggle");

    public static ActionId WorkingTreeStage { get; } = new("working-tree.stage");

    public static ActionId WorkingTreeStageAll { get; } = new("working-tree.stage-all");

    public static ActionId WorkingTreeUnstage { get; } = new("working-tree.unstage");

    public static ActionId WorkingTreeUnstageAll { get; } = new("working-tree.unstage-all");

    public static ActionId WorkingTreeDiscard { get; } = new("working-tree.discard");

    public static ActionId WorkingTreeWriteMessage { get; } = new("working-tree.write-message");

    public static ActionId WorkingTreeEditMessage { get; } = new("working-tree.edit-message");

    public static ActionId WorkingTreeFocusUnstaged { get; } = new("working-tree.focus-unstaged");

    public static ActionId WorkingTreeFocusStaged { get; } = new("working-tree.focus-staged");

    public static ActionId WorkingTreeFocusDiff { get; } = new("working-tree.focus-diff");

    public static ActionId WorkingTreeFocusMessage { get; } = new("working-tree.focus-message");

    public static ActionId WorkingTreeMessageNewLine { get; } = new("working-tree.message-new-line");

    public static ActionId WorkingTreeMessageLeft { get; } = new("working-tree.message-left");

    public static ActionId WorkingTreeMessageDown { get; } = new("working-tree.message-down");

    public static ActionId WorkingTreeMessageUp { get; } = new("working-tree.message-up");

    public static ActionId WorkingTreeMessageRight { get; } = new("working-tree.message-right");

    public static ActionId WorkingTreeMessageWordNext { get; } = new("working-tree.message-word-next");

    public static ActionId WorkingTreeMessageWordPrevious { get; } = new("working-tree.message-word-previous");

    public static ActionId WorkingTreeMessageLineStart { get; } = new("working-tree.message-line-start");

    public static ActionId WorkingTreeMessageLineEnd { get; } = new("working-tree.message-line-end");

    public static ActionId WorkingTreeMessageAppend { get; } = new("working-tree.message-append");

    public static ActionId WorkingTreeMessageAppendAtEnd { get; } = new("working-tree.message-append-at-end");

    public static ActionId WorkingTreeMessageInsertAtStart { get; } = new("working-tree.message-insert-at-start");

    public static ActionId WorkingTreeMessageOpenLine { get; } = new("working-tree.message-open-line");

    public static ActionId WorkingTreeMessageDeleteChar { get; } = new("working-tree.message-delete-char");

    public static ActionId WorkingTreeMessageDeleteLine { get; } = new("working-tree.message-delete-line");

    public static ActionId WorkingTreeMessageUndo { get; } = new("working-tree.message-undo");

    public static ActionId WorkingTreeCommit { get; } = new("working-tree.commit");

    public static ActionId WorkingTreeToggleSignOff { get; } = new("working-tree.toggle-sign-off");

    public static ActionId WorkingTreeToggleNoVerify { get; } = new("working-tree.toggle-no-verify");

    public static ActionId WorkingTreeToggleAmend { get; } = new("working-tree.toggle-amend");

    /// <summary>Jump to tab <paramref name="number"/> (1-based).</summary>
    public static ActionId TabGoTo(int number) => new($"tab.go-to.{number}");

}
