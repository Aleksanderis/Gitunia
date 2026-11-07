# UX: mouse

*Internal design note for maintainers and agents. Not needed to use or contribute to Gitunia.*

Part of the UX spec; start at [ux.md](ux.md).

## Mouse

Mouse input is optional and goes through the same action path as keys: the mouse map (`DefaultMouseMap`) turns a gesture on a view into the action id a key would produce, and the same handler runs it.
A test parses the table below and checks it against the defaults, so add a row in the same change that adds a mouse path.
Views inherit like in the keymap: `Lists` covers Graph, Detail, Sidebar, Diff and the two Changes view lists.
`CommandsButton`, `NewTabButton`, `Tabs`, `Picker`, the banner chips (`BannerButton.*`), the overlays (`Help`, `Messages`, `Palette`, `Reflog`, `FileHistory`, `Settings` and `Backdrop` for everything outside an open one) and the buttons (`DiffButton.*`, `DetailTab.*`, `PickerButton.*`, `ListButton.*`, one view per button or label) are views of their own.
`CommandsButton`, `NewTabButton`, `Tabs`, `Picker`, the banner chips (`BannerButton.*`), the overlays (`Help`, `Messages`, `Palette`, `Reflog`, `Blame`, `Settings` and `Backdrop` for everything outside an open one) and the buttons (`DiffButton.*`, `DetailTab.*`, `PickerButton.*`, `ListButton.*`, one view per button or label) are views of their own.

| View | Gesture | Action | Effect |
|---|---|---|---|
| Lists | Wheel up | `list.up` | Moves the selection up 3 rows (like `3k`) in the pane under the pointer, which gets focus |
| Lists | Wheel down | `list.down` | Moves the selection down 3 rows (like `3j`) |
| Lists | Drag start | `visual.toggle` | Pressing a row and moving the pointer starts Visual mode (range selection, like `v`) at the pressed row (in the diff, the file content, the Changes view diff and the Details tab it selects characters instead, see Drag to select); graph, detail, diff, the Changes view diff and the two Changes view file lists (to stage the files or lines, press `<space>a` / `<space>u`, or run the action from the palette, `:`) |
| Diff | Double-click | `visual.select-word` | History diff and file content (the Changes view diff has its own row below, with no triple-click): select the word under the pointer by character, then `y` copies it |
| Diff | Triple-click | `visual.select-line` | Select the text of the line under the pointer (no gutter or marker) |
| Detail | Triple-click | `visual.select-line` | Details tab only: select the text of the row under the pointer by character (the double-click keeps opening an issue link; on the Changes and Files tabs a notice says there is no character selection) |
| Lists | Drag up | `list.up` | While dragging, the cursor follows the pointer: moves up by as many rows as the pointer is above it |
| Lists | Drag down | `list.down` | The same downwards |
| Graph | Double-click | `menu.open-for-row` | Same as `<space>.`: open the actions menu of the row (the first click selected it); a double-click on blank space below the last row does nothing; the details pane is reached by a click on it, `Enter`, `Ctrl-j` or the menu's Open details row |
| Detail | Click | `detail.toggle-folder` | Open or close the folder row under the pointer (Changes tab, tree style; nothing on other tabs and styles; the row is selected as usual, so the diff follows; a click on a file only selects it, and a double-click undoes the toggle of its first click, then opens the diff, #622) |
| Detail | Double-click | `detail.open-diff` | Same as `Enter`: go to the file diff (Files tab: open or close the directory, or focus the file's content; Details tab: open the issue link on the line, if any) |
| Sidebar | Click | `sidebar.activate` | Same as `Enter`: expand or collapse a section, select the ref's commit in the graph, show a stash in the detail pane (then a click on its file rows opens their diff) |
| Tabs | Click | `tab.next` | Switch to the clicked tab (same as `3gt` with the tab's number); closes an open picker |
| Tabs | Drag back | `tab.move-left` | Press a tab chip and move the pointer: the tab becomes the active one and moves one place left each time the pointer crosses the left edge of its chip, so the chip follows the pointer and the others shift (a click without moving only switches); release drops it, releasing outside the tab row or `Esc` puts the tab back where the drag began |
| Tabs | Drag forward | `tab.move-right` | The same to the right; the `+` button and the right-hand chips are no drop targets |
| TabDirtyMarker | Click | `working-tree.open` | The `*` of a dirty tab chip: switch to that tab and open its Changes view (a click elsewhere on the chip only switches); no effect where the marker is cut off |
| NewTabButton | Click | `picker.open` | The ` + ` button right after the last tab: open the repository picker for a new tab, same as `Ctrl-t` |
| CommandsButton | Click | `palette.open` | The ` : commands ` button at the right end of the tab row (rows of 44 columns or more; there is no header bar): open the command palette, the menu of the actions of the focused pane and the global ones, each with its keys |
| RemoteButton.Fetch | Click | `remote.fetch` | The `<space>f fetch` chip of the tab row (rows of 100 columns or more, the key text from 127), left of the commands button: same as `<space>f` (or `<space>rf`) |
| RemoteButton.Pull | Click | `remote.pull` | The ` pull ` chip: same as `<space>p` (or `<space>rl`) |
| RemoteButton.Push | Click | `remote.push` | The ` push ` chip: same as `<space>P` (or `<space>rp`) |
| RemoteButton.Cancel | Click | `remote.cancel` | The `<space>rc cancel` chip that replaces the three chips while one of them runs on the active tab: same as `<space>rc` |
| BannerButton.Continue | Click | `operation.continue` | The `continue` chip of the in-progress operation banner: same as `<space>gc` |
| BannerButton.Abort | Click | `operation.abort` | The `abort` chip of the banner: same as `<space>ga` (asks first) |
| BannerButton.Skip | Click | `operation.skip` | The `skip` chip of the banner (not shown for a merge): same as `<space>gs` (asks first) |
| RowMenuButton | Click | `menu.open-for-row` | The `...` marker in the last three columns of the selected sidebar row, or of the selected file row of the commit Changes and Files tabs and of both Changes view lists (not on folder rows): opens the actions menu of that row without moving the cursor (see Actions menu); the file menu offers `<space>H` file history and `<space>gb` blame (#1132); other rows only select on a click; on file rows only a single click hits the marker, a double-click keeps its own action |
| Backdrop | Click | `cancel` | While the help, palette or status history is open, a click on a pane or the tab row outside it closes it (like `Esc`); the double-click that follows is ignored |
| Palette | Click | `command.execute` | Run the clicked entry (it is highlighted first) |
| Palette | Wheel up | `palette.previous` | Move the highlight up one row |
| Palette | Wheel down | `palette.next` | Move the highlight down one row |
| ContextMenu | Click | `command.execute` | Run the clicked entry of the actions menu (it is highlighted first); a click outside closes the menu |
| ContextMenu | Wheel up | `palette.previous` | Move the highlight up one row |
| ContextMenu | Wheel down | `palette.next` | Move the highlight down one row |
| ConfirmButton.Yes | Click | `confirm.yes` | The `y` row of a confirm dialog, same as `y` |
| ConfirmButton.No | Click | `confirm.no` | The `n` row of a confirm dialog, same as `n` / `Esc` |
| HelpTab | Click | `help.tab-select` | A section in the help overlay's left menu, or a tab label in its tab row (few pages): shows that page (the row number is the click target; when the labels do not fit, the one compact label `[Graph] 1/4` moves to the next tab) |
| Help | Wheel up | `list.up` | Scroll the help up 3 lines (wherever the pointer is while the overlay is open) |
| Help | Wheel down | `list.down` | Scroll the help down 3 lines |
| Messages | Wheel up | `list.up` | Scroll the status history up 3 lines |
| Messages | Wheel down | `list.down` | Scroll the status history down 3 lines |
| Reflog | Wheel up | `list.up` | Scroll the reflog list up 3 entries |
| Reflog | Wheel down | `list.down` | Scroll the reflog list down 3 entries |
| ReflogButton.Select | Click | `reflog.select-commit` | Action row `Enter` under the reflog list: select the highlighted entry's commit in the graph (the list stays open); a click on an entry itself only highlights it |
| ReflogButton.Reset | Click | `reset.branch` | Row `<space>bx`: reset the current branch to the highlighted entry (closes the list first) |
| ReflogButton.Branch | Click | `branch.create` | Row `<space>bc`: new-branch prompt at the highlighted entry |
| ReflogButton.Checkout | Click | `branch.switch` | Row `<space>bs`: check the entry out with a detached HEAD |
| FileHistory | Wheel up | `list.up` | Move the highlighted commit of the file history up 3 rows; the diff follows |
| FileHistory | Wheel down | `list.down` | Move the highlighted commit of the file history down 3 rows; the diff follows |
| FileHistoryButton.Select | Click | `file-history.select-commit` | Action row `Enter` under the file history: select the highlighted commit in the graph (closes the history); a click on a commit row itself only highlights it |
| Blame | Wheel up | `list.up` | Scroll the blame view up 3 lines |
| Blame | Wheel down | `list.down` | Scroll the blame view down 3 lines |
| Blame | Drag start | `visual.toggle` | A drag from the code column selects characters (the pressed character is the anchor, the one under the pointer the moving end, both included, drawn in the range tint over exactly that text), a drag from the gutter selects whole lines; the cursor follows the pointer, also above or below the shown lines (no auto-scroll); `y` copies the code text of the selection (#1175); with `copyOnSelect` a released drag or a double or triple click copies it at once and keeps the selection (#1189) |
| Blame | Double-click | `visual.select-word` | On the code: select the word under the pointer by character, then `y` copies it (with `copyOnSelect` it is copied at once) |
| Blame | Triple-click | `visual.select-line` | On the code: select the text of the line, copied at once with `copyOnSelect` (the three Blame gesture rows are fixed: the view maps the pointer position itself, so they are not remappable) |
| BlameButton.Show | Click | `blame.show-commit` | Action row `Enter` under the blame view: select the highlighted line's commit in the graph (the view closes); a click on a line itself only highlights it |
| BlameButton.History | Click | `file-history.open` | Action row `<space>H` under the blame view: close the view and open the history of the blamed file at the blamed revision (`HEAD` for the working tree file) |
| BlameButton.LineHistory | Click | `line-history.open` | Action row `<space>L` under the blame view: close the view and open the history of the highlighted line, starting at the commit that wrote it with that commit's line number (an uncommitted line says it has no history) |
| BlameButton.Parent | Click | `blame.parent` | Action row `Alt-p` under the blame view: blame the file again at the parent of the highlighted line's commit (Esc steps back) |
| BlameButton.Whitespace | Click | `blame.toggle-whitespace` | Chip `-w` (`Alt-w`) in the chip row under the blame view: toggle ignoring whitespace changes and blame again; the switches in force show in the footer |
| BlameButton.Moves | Click | `blame.toggle-moves` | Chip `-M` (`Alt-m`) in the same row: toggle detecting lines moved within the file and blame again |
| BlameButton.Copies | Click | `blame.toggle-copies` | Chip `-C` (`Alt-c`) in the same row: toggle detecting lines copied from other files and blame again |
| ReflogPicker | Click | `input.accept` | A row of the reflog ref picker: highlights it and opens that ref's reflog, like `Enter` |
| RebaseEditor | Wheel up | `list.up` | Scroll the todo up 3 commits |
| RebaseEditor | Wheel down | `list.down` | Scroll the todo down 3 commits |
| RebaseButton.Pick | Click | `rebase-editor.pick` | Chip `p pick` under the todo: sets the highlighted commit; a click on a commit itself only highlights it |
| RebaseButton.Reword | Click | `rebase-editor.reword` | Chip `r reword`: opens the message editor |
| RebaseButton.Squash | Click | `rebase-editor.squash` | Chip `s squash` |
| RebaseButton.Fixup | Click | `rebase-editor.fixup` | Chip `f fixup` |
| RebaseButton.Drop | Click | `rebase-editor.drop` | Chip `d drop` |
| RebaseButton.Up | Click | `rebase-editor.move-up` | Chip `K move up` |
| RebaseButton.Down | Click | `rebase-editor.move-down` | Chip `J move down` |
| RebaseButton.Start | Click | `rebase-editor.start` | Chip `Enter start`: asks the Confirm (its `y` and `n` rows are the `ConfirmButton.Yes` and `ConfirmButton.No` views) |
| RebaseButton.Cancel | Click | `navigate.back` | Chip `Esc cancel`: closes the editor like `Esc` |
| ConflictSide | Wheel up | `list.up` | Cursor up three lines in the pane (the other panes follow) |
| ConflictSide | Wheel down | `list.down` | Cursor down three lines |
| ConflictSide | Drag start | `visual.toggle` | Drag from a row of the base, ours or theirs pane: the cursor goes to the row and a Visual selection starts like `v`; the far end follows the pointer, `<space>a` / the `take lines` chip takes only those lines (#861). A click on a row or result line places the cursor from the frame position (no action; see workflow 4, "Mouse") |
| ConflictSide | Sideways wheel back | `conflict-editor.scroll-left` | Shift or Ctrl with the wheel up: the base, ours and theirs panes scroll 8 columns to the left together (#862) |
| ConflictSide | Sideways wheel forward | `conflict-editor.scroll-right` | Shift or Ctrl with the wheel down: 8 columns to the right |
| ConflictResult | Sideways wheel back | `conflict-editor.scroll-left` | Same, with the cursor in the result: the version panes scroll, the result keeps scrolling its cursor line on its own |
| ConflictResult | Sideways wheel forward | `conflict-editor.scroll-right` | Same, to the right |
| ConflictResult | Wheel up | `list.up` | Result cursor up three lines |
| ConflictResult | Wheel down | `list.down` | Result cursor down three lines |
| ConflictButton.Ours | Click | `conflict-editor.ours` | Chip `o ours`: takes our side for the current conflict |
| ConflictButton.Theirs | Click | `conflict-editor.theirs` | Chip `t theirs` |
| ConflictButton.Both | Click | `conflict-editor.both` | Chip `m both` |
| ConflictButton.Base | Click | `conflict-editor.base` | Chip `b base`: takes the common ancestor |
| ConflictButton.Take | Click | `conflict-editor.take` | Chip `<space>a take` (`take lines` during a Visual selection): takes the active pane's version |
| ConflictButton.AllOurs | Click | `conflict-editor.all-ours` | Chip `O all ours` |
| ConflictButton.AllTheirs | Click | `conflict-editor.all-theirs` | Chip `T all theirs` |
| ConflictButton.Select | Click | `visual.toggle` | Chip `v select lines` (`end selection` while one is open) |
| ConflictButton.ScrollLeft | Click | `conflict-editor.scroll-left` | Chip `zh scroll left`: the version panes 8 columns to the left (#862) |
| ConflictButton.ScrollRight | Click | `conflict-editor.scroll-right` | Chip `zl scroll right` |
| ConflictButton.Pane | Click | `pane.next` | Chip `Tab next pane`: base, ours, theirs, result; on a narrow terminal it also changes the version panes shown |
| ConflictButton.Undo | Click | `conflict-editor.undo` | Chip `u undo` |
| ConflictButton.Redo | Click | `conflict-editor.redo` | Chip `Ctrl-r redo` |
| ConflictButton.Untake | Click | `conflict-editor.untake` | Chip `<space>u untake` (`untake lines` during a Visual selection) |
| ConflictButton.Edit | Click | `conflict-editor.edit` | Chip `e edit in result` (`i type` in the result pane): moves into the result at the current conflict and starts typing |
| ConflictButton.Recreate | Click | `conflict-editor.recreate` | Chip `Ctrl-x re-create`: asks first |
| ConflictButton.EditDone | Click | `conflict-editor.edit-done` | Chip `Ctrl-g keep`, the only chip while the result is typed in |
| ConflictButton.Next | Click | `conflict-editor.next` | Chip `n next conflict` |
| ConflictButton.Previous | Click | `conflict-editor.previous` | Chip `N previous conflict` |
| ConflictButton.Save | Click | `conflict-editor.save` | Chip `Ctrl-s save`: saves and stages (asks first when conflicts are unresolved, and then does not stage) |
| ConflictButton.Cancel | Click | `navigate.back` | Chip `Esc close`: closes the editor like `Esc` |
| Settings | Wheel up | `list.up` | Scroll the column under the pointer up 3 rows (the categories or the settings) |
| Settings | Wheel down | `list.down` | Scroll the column under the pointer down 3 rows |
| SettingsButton.Edit | Click | `settings.activate` | A click on a setting's control, on an option of an open choice, or a second click on the selected row: edits it like `Enter` (toggle flips, choice opens, option is kept). A first click on a row, and a click on a category, only select |
| SettingsButton.Add | Click | `settings.add` | The `[+]` marker of a path list: add a folder, like `a` |
| SettingsButton.Remove | Click | `settings.remove` | The `[x]` marker of a folder: ask to remove it, like `d`; the question's `y` and `n` rows are the `ConfirmButton.Yes` and `ConfirmButton.No` views |
| MainlineParent | Click | `pick.mainline` | A parent row of the mainline overlay of a cherry-pick or revert of a merge commit: answers like the parent's digit key (the row number is the click target; a click outside cancels) |
| DiffButton.PreviousFile | Click | `diff.previous-file` | Button `‹ file`, same as `[f` |
| DiffButton.NextFile | Click | `diff.next-file` | Button `file ›`, same as `]f` |
| DiffButton.PreviousHunk | Click | `diff.previous-hunk` | Button `‹ hunk`, same as `[c` |
| DiffButton.NextHunk | Click | `diff.next-hunk` | Button `hunk ›`, same as `]c` |
| DiffButton.Wrap | Click | `diff.toggle-wrap` | Button `wrap` (a file's content only; the diff has it in the `view ▾` dropdown), same as `<space>vw` |
| DiffButton.Zoom | Click | `pane.zoom` | Button `zoom` of a file's content (the diff has the entry in the `view ▾` dropdown instead, #758), same as `<space>z` |
| DiffButton.View | Click | `diff.open-view-options` | Button `view ▾`, same as `<space>vv`: opens the dropdown of context, whole file, ignore space, split, wrap, whitespace marks and zoom |
| DiffButton.Stage | Click | `working-tree.stage` | Changes view diff, Unstaged side: button `stage`, same as `<space>a` (the change group under the cursor, or the Visual range); also a chip at the right end of the group's first line (same view, same action) |
| DiffButton.Unstage | Click | `working-tree.unstage` | Changes view diff, Staged side: button `unstage`, same as `<space>u`; also a chip on the group's first line |
| DiffButton.Discard | Click | `working-tree.discard` | Changes view diff, Unstaged side: button `discard`, same as `<space>d`; opens the Confirm dialog, nothing is discarded before `y`; also a chip on the group's first line |
| DetailTab.Details | Click | `detail.tab-details` | The `Details` label in the detail pane's title row, same as `gd` (also focuses the pane) |
| DetailTab.Changes | Click | `detail.tab-changes` | The `Changes` label, same as `gc` |
| DetailTab.Files | Click | `detail.tab-files` | The `Files` label, same as `gf` |
| DetailTab.View | Click | `files.open-view-options` | The `view ▾` chip at the right end of the title row while the Changes or Files tab is selected (shown when the row has room), same as `<space>vv`: opens the dropdown of the path style (Changes) or the filter (Files) |
| WorkingUnstaged | Click | `working-tree.toggle-folder` | A folder row of the tree style opens or closes (same as `za`); on a file row it only moves the cursor |
| WorkingStaged | Click | `working-tree.toggle-folder` | A folder row of the tree style opens or closes (same as `za`); on a file row it only moves the cursor |
| WorkingUnstaged | Double-click | `working-tree.stage` | Stage the file, or every file under the folder (same as `<space>a`) |
| WorkingStaged | Double-click | `working-tree.unstage` | Unstage the file, or every file under the folder (same as `<space>u`) |
| ListButton.StageAll | Click | `working-tree.stage-all` | Button `stage all` at the right end of the Unstaged list's title row, same as `<space>A` (also focuses the list) |
| ListButton.UnstageAll | Click | `working-tree.unstage-all` | Button `unstage all` in the Staged list's title row, same as `<space>U` |
| ListButton.Resolve | Click | `conflict.open` | The `resolve` chip in the Unstaged list's title row, left of the `view ▾` chip; shown only while the cursor is on a conflicted file, same as `Enter` / `<space>gr`: opens the conflict editor (also focuses the list; #701) |
| ListButton.Diff | Click | `working-tree.open-diff` | The `diff ›` chip in the title row of either file list, shown only on screens under 80 columns where the diff is not beside the list; left of the stage all / unstage all button and ahead of the `view ▾` and `resolve` chips (they give way to it), same as `Enter`: the diff of the file or folder under the cursor takes focus and replaces the list (also focuses the list; #376) |
| ListButton.View | Click | `files.open-view-options` | The `view ▾` chip in the title row of either file list, left of the stage all / unstage all button, same as `<space>vv`: opens the dropdown of the path style |
| WorkingMessage | Click | `working-tree.edit-message` | Focus the message box and start writing (Insert mode), same as `i`; the cursor goes where the pointer is (the position is not part of the action id) |
| WorkingMessage | Double-click | `working-tree.message-select-word` | Select the word (or the run of spaces or symbols) under the pointer; the first click of the double-click already placed the cursor (#1165) |
| WorkingMessage | Wheel up | `working-tree.message-scroll-up` | Move the cursor 3 lines up, scrolling a long message (any mode, like the lists' wheel) |
| WorkingMessage | Wheel down | `working-tree.message-scroll-down` | Move the cursor 3 lines down |
| MessageButton.SignOff | Click | `working-tree.toggle-sign-off` | The `[ ] SignOff` checkbox of the message box, same as `Alt-s` |
| MessageButton.NoVerify | Click | `working-tree.toggle-no-verify` | The `[ ] No-Verify` checkbox, same as `Alt-n` |
| MessageButton.Amend | Click | `working-tree.toggle-amend` | The `[ ] Amend` checkbox, same as `Alt-a` |
| MessageButton.Commit | Click | `working-tree.commit` | The `Commit` button, same as `Ctrl-g` |
| WorkingUnstagedDiff | Double-click | `visual.select-word` | Select the word under the pointer by character, then `y` copies it (staging a change group is `<space>a`, the `stage` chip or the file list; no triple-click here, #1177) |
| WorkingStagedDiff | Double-click | `visual.select-word` | Select the word under the pointer by character, then `y` copies it (unstaging a change group is `<space>u` or the `unstage` chip; no triple-click here, #1177) |
| Picker | Double-click | `picker.accept` | Open the repository of the row; on a `▸ N more` line expand that section, on a `▴ less` line collapse it (the click before it highlighted the line; nothing is opened) |
| Picker | Wheel up | `picker.previous` | Move the highlight up one row |
| Picker | Wheel down | `picker.next` | Move the highlight down one row |
| PickerButton.Accept | Click | `picker.accept` | Footer chip `Enter open` (key from the keymap; `locate` while locating a missing repository): open the highlighted row, same as `Enter` (#1203, #1212) |
| PickerButton.Close | Click | `cancel` | Footer chip `Esc close`: close the picker, same as `Esc` (#1203) |
| PickerButton.Pin | Click | `picker.pin` | Footer chip `Alt-p pin` / `Alt-p unpin` for the highlighted row, same as `Alt-p` |
| PickerButton.Remove | Click | `picker.remove-recent` | Footer chip `Alt-d remove` for a highlighted recent row, same as `Alt-d` |
| PickerButton.Clone | Click | `picker.clone` | Footer chip `Alt-c clone` in the picker box (when it fits, not while locating), same as `Alt-c` |
| PickerButton.Init | Click | `picker.init` | Chip `Alt-i init` next to it, same as `Alt-i` |

Pointer-position behaviour that has no action of its own: a click on a pane focuses it, a click on a list row selects it (graph, detail, sidebar, diff; the details pane follows the graph selection as with keys), a click on a picker row highlights it (also on a `▸ N more` or `▴ less` line, which is a row kind of its own with no repository behind it).
A click on empty space does nothing.
The wheel moves the selection, not just the view (the keyboard has no pure scroll either), so the details pane follows it as it does for `j`/`k`.
A mouse action does not touch a pending count or chord typed before it.
While the help, palette or status history is open, panes and the picker ignore every mouse gesture (no focus change, no selection), except that the wheel scrolls the help or history (or moves the palette highlight) when the pointer is over a pane or the overlay, and a click on a pane or the tab row closes it, like `Esc`; the which-key popup does not block the mouse.

Drag to select: press a row of the graph, detail, diff or file-list pane and move the pointer; the pane takes focus, the pressed row becomes the anchor, Visual mode starts (status bar `VISUAL`) and the cursor follows the pointer, also above or below the pane while the button is held: while the pointer rests outside the pane the cursor keeps moving every 80 ms, 1 row just outside, 1 more per 2 rows of distance, at most 6, until the pointer is back inside, the list ends or the button is released.
The range is drawn stronger than the selected row (bolder and a brighter tint, reverse video where there are no colours), and its moving end, the cursor row, is underlined as well.
In the diff panes (history diff, file content, the Changes view diff, also while wrap or side by side is on) and on the Details tab the drag selects characters: the pressed character is the anchor and the character under the pointer is the moving end (both included), drawn in the same stronger tint over exactly that text, gutter and `+`/`-` markers excluded; a drag from the gutter starts at the first character and one past the end of a line stops at its last, and `y` copies exactly those characters (lines joined with a line feed).
With word wrap on, the pointer on any wrapped line of a row maps to the character it shows (the row's text continues across its lines and is copied without a line feed inside the row). Side by side, the half the button went down on is the side of the whole drag: the other half is not selected while the pointer crosses it, and only that side's text is copied.
The keyboard path is `Alt-v`, then `h` `l` `w` `b` `0` `$`, `iw`, `il` (see the keymap); the graph, the Changes and Files tabs and the file lists still select whole rows, or nothing; the blame view has its own selection (table above, #1175).
A double-click in the history diff, file content or Changes view diff selects the word under the pointer, the same as `iw`; a triple-click in the history diff or file content selects the text of the line, the same as `il` (the Changes view diff has none: drag or `Alt-v` `il`); the Details tab keeps its issue-link double-click (the triple-click selects the row there).
With mouse reporting on, the terminal's own text selection needs the terminal's bypass modifier (usually Shift-drag; Option-drag in some macOS terminals): that copy is plain screen text, with gutter and markers, and bypasses OSC 52.
The release leaves the range active; a click on a row, `Esc`, `q`, `v` (on a character selection it turns it into whole rows) or moving focus to another pane or tab ends it.
Copy on select (`copyOnSelect`, on by default, #1188): the release of a drag, and a double or triple click that selected a word or a line, copy the selection through OSC 52 at once; the highlight stays, so `y` copies it again. Selections made with keys (`v`, `Alt-v`, `iw`) never copy by themselves. The blame view does the same for its own selection (#1189); the conflict editor selects lines to take, not to copy.
`Ctrl-c` while a selection is active (in a pane or in the blame view) copies it too (the status bar says "Copied. Press Ctrl-c again to quit"; the blame view shows only the copy's own message) and keeps it; a second `Ctrl-c` within 2 s quits (see the keymap, "Quit").
`y` copies the range (the drag plus `y`, or `visual.copy` from the palette) and ends it; staging acts on it too (lines in the Changes view diff, files in its lists).
The sidebar has no ranges (a drag there only moves the selection). A drag in the message box is a text selection, not a Visual range: the press cell places the cursor and starts Insert mode like a click, the pointer then extends a selection drawn with the range tint (a drag above or below the box keeps extending to the first or last visible line; it does not auto-scroll on its own); it ends with a click, a cursor key or `Esc` (#1165); a drag also does not start a range while a prompt (search, filter, ref jump) or the picker is open.

Drag to resize: the line between the Changes view file lists and its diff (#1239), the line between the sidebar and the graph, the line between the detail and diff panes, and the title rows of the detail and diff panes (the line between the graph and the bottom panes) follow the pointer while the button is held.
Sizes are clamped to what fits (at least 12 columns for the sidebar and detail, 20 for the graph and diff, 3 rows for the bottom panes, 4 for the graph), belong to the tab and are saved with the session (the Changes view list width is the exception: it is clamped to at least 12 columns with 20 left for the diff and not saved, #1237); `Ctrl-w =` (or `:`, then "Reset the pane sizes") returns to automatic, and `Ctrl-w >` `Ctrl-w <` `Ctrl-w +` `Ctrl-w -` are the keyboard path; after a step the repeat mode (see Modes) takes bare `>` `<` `+` `-` `=` until `Esc` or another key.
A single zoomed pane has no dividers to drag (the divider between the paired Changes and diff panes does not drag while zoomed, but `Ctrl-w >` `Ctrl-w <` move it, #683).

Clickable titles and buttons: a click on the title row of the detail or diff pane focuses that pane.
The detail title row starts with the three tab labels (`Details`, `Changes`, `Files`), drawn as buttons: the selected one is the accent chip (quieter when the pane has no focus), the others look exactly like the inactive repository tabs of the tab row (a chip on a subtle tinted background; underlined where the terminal tier has no background colours, `[Name]` in the ASCII tier from 29 columns, one padding column per side).
The selected tab of an unfocused pane keeps the chip but in bold accent-coloured text (bold and underlined in the colourless tiers) so it still differs from the other tabs; the title of a pane with a single view (graph, sidebar, diff, Changes view lists) is drawn the same way when the pane has no focus, one column of line between them; below 29 columns the padding around the labels goes.
The content of the detail pane has the same background as the other panes; the frame line and the buttons separate it.
A click on a label selects the tab.
The diff title row ends in buttons (see the table), chips like the inactive repository tabs (with the key in bold before the label when the pane is wide enough; the chips on a change group's first line stay label-only) with one column of line between them and `<`/`>` arrows and brackets in the ASCII tier, when the pane is at least 30 columns wider than the buttons need; narrower panes drop buttons in this order, last to go first: `view ▾`, the two hunk buttons, the two file buttons, the keys stay available.
The path gets the room first: the diff options that used to be five chips (`ctx-`, `ctx+`, `file`, `-w`, `split`) and `wrap` sit behind the one `view ▾` dropdown (#645).
The Changes view diff has the same title row and buttons, with the staging buttons of its side first: `stage` and `discard` on the Unstaged side (`stage` is the last to be dropped on a narrow row, `discard` sits after the hunk buttons in the drop order), `unstage` on the Staged side; they act on the change group under the cursor or the Visual range like `<space>a`, `<space>u` and `<space>d`, and `discard` opens the Confirm dialog first.
The change group under the cursor also carries the same chips on its first line, at the right edge (`stage` and `discard` on the Unstaged side, `unstage` on the Staged side; the history diff and a Visual range have none; a line whose text reaches the chips gets none, the title row still has them): a click or double-click on one runs the title button's action, so `discard` still opens the Confirm first.
A row that would keep fewer than 30 columns of diff text drops `discard`, then `stage`/`unstage`; the keys and the title buttons stay.
`<space>z` and the `zoom` entry of the `view ▾` dropdown give the diff the whole view (lists and message box hidden) until focus leaves the diff (`Esc`, `Ctrl-h`) or zoom is used again; in the history view with the Changes tab beside the diff they zoom the file list and the diff together and `Esc`/`Ctrl-h` only return focus to the list (#632).
In the Changes view a click on the diff box focuses it (the mouse path from a list to its diff on screens of 80 columns or more; narrow screens use the `diff ›` chip in the title row of the list).
The picker's footer row (under a rule) shows the key-first chips of the diff title (#744, #880), in two groups: on the left `Enter open` (`Enter locate` while locating) and, for the highlighted row, `Alt-p pin`/`Alt-p unpin` and (recent repository) `Alt-d remove`; on the right `Alt-c clone`, `Alt-i init` and `Esc close` (#1203). Chips that do not fit are left out, the right group from its left end, so `Esc close` is the last to go. Picker list lines that are not rows (group headers and the blank spacer above each header but the first) ignore clicks; a click or double-click on a row hits that row wherever the spacers push it. The `▸ N more` and `▴ less` lines take clicks like rows (#1205): the footer's first chip then reads `Enter expand` or `Enter collapse` and the pin and remove chips are left out.
The repository picker (the opener) hides the fetch, pull and push chips of the tab row, since they act on the active tab's repository; `: commands` stays.
Clicking the commands button at the right end of the tab row opens the command palette, which lists the actions of the focused pane and the global ones, so every action without a button (for example `<space>vl` load anyway, the search keys, `<space>or` refresh) can be run with the mouse; typing a search or a filter text needs the keyboard, as nothing else can enter text.

Terminal.Gui turns on mouse reporting (SGR, mode 1006) at startup and turns it off on exit; there is no switch to disable it yet.
Verified by writing SGR mouse sequences into the pane of a tmux 3.7c session running the Linux build: clicks, double-clicks, the wheel, press-drag-release (reported as a press, position reports with the button held, a release and then a click at the release position, which the drag logic swallows), divider and title drags, header, palette and button clicks all arrive; a double-click is delivered as a click followed by a double-click event (so a double-click on a sidebar section toggles it once), and a sideways wheel (Ctrl or Shift plus wheel) is ignored (unit-tested only).
Not yet verified with a physical mouse, over ssh, or in kitty, Windows Terminal and macOS terminals.
Terminals or ssh paths that drop mouse sequences leave the keyboard path unaffected.

Design history (why things are the way they are, measurements, rejected options): `docs/history/ux.md`. Not needed for routine work.
