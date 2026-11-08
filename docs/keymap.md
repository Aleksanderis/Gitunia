# Keymap reference

<!-- Generated from src/Gitunia.Core/Keymap/keymap.default.json by tools/KeymapDoc. Do not edit: change the JSON, then run `scripts/gen-keymap-doc.sh`. A test fails when this file is stale. -->

Every default key binding, one table per view. The design rules, notation and modes are in [ux-keymap.md](internal/ux-keymap.md). A view's keys also apply to the views that inherit from it (`Global` applies everywhere). Keys are listed main key first; `Ctrl-w c` is a chord, `<space>o` a leader chord. Rebinds in `config.json` are not shown here (see the Key reference in the settings screen).

## Global

| Keys | Modes | Action | What |
|---|---|---|---|
| `?` | Normal, Visual | `help.show` | Show keys for this view |
| `:` | Normal | `palette.open` | Open the command palette |
| `Ctrl-p` | Normal, Visual, Insert | `palette.open` | Open the command palette |
| `q` | Normal, Visual | `app.quit` | Quit |
| `Ctrl-c` | Normal, Visual, Insert | `app.quit` | Quit |
| `Tab` `Ctrl-w w` | Normal | `pane.next` | Next pane |
| `Shift-Tab` | Normal | `pane.previous` | Previous pane |
| `gt` | Normal | `tab.next` | Next tab |
| `gT` | Normal | `tab.previous` | Previous tab |
| `Alt-1` | Normal, Visual, Insert | `tab.go-to.1` | Go to tab 1 (same as 1gt) |
| `Alt-2` | Normal, Visual, Insert | `tab.go-to.2` | Go to tab 2 (same as 2gt) |
| `Alt-3` | Normal, Visual, Insert | `tab.go-to.3` | Go to tab 3 (same as 3gt) |
| `Alt-4` | Normal, Visual, Insert | `tab.go-to.4` | Go to tab 4 (same as 4gt) |
| `Alt-5` | Normal, Visual, Insert | `tab.go-to.5` | Go to tab 5 (same as 5gt) |
| `Alt-6` | Normal, Visual, Insert | `tab.go-to.6` | Go to tab 6 (same as 6gt) |
| `Alt-7` | Normal, Visual, Insert | `tab.go-to.7` | Go to tab 7 (same as 7gt) |
| `Alt-8` | Normal, Visual, Insert | `tab.go-to.8` | Go to tab 8 (same as 8gt) |
| `Alt-9` | Normal, Visual, Insert | `tab.go-to.9` | Go to tab 9 (same as 9gt) |
| `<space>oo` | Normal | `picker.open` | Open the repository picker |
| `Ctrl-t` | Normal, Visual, Insert | `picker.open` | Open the repository picker |
| `<space>oc` `Ctrl-w c` | Normal | `tab.close` | Close the tab |
| `<space>on` `Ctrl-w r` | Normal | `tab.rename` | Rename the tab |
| `<space>o <` | Normal | `tab.move-left` | Move the tab one place left |
| `<space>o>` | Normal | `tab.move-right` | Move the tab one place right |
| `<space>op` | Normal | `repo.pin` | Pin to the top of the picker (toggle) |
| `Ctrl-h` `Ctrl-w h` `Ctrl-Backspace` | Normal | `pane.focus-left` | Focus the pane to the left |
| `Ctrl-j` `Ctrl-w j` | Normal | `pane.focus-down` | Focus the pane below |
| `Ctrl-k` `Ctrl-w k` | Normal | `pane.focus-up` | Focus the pane above |
| `Ctrl-l` `Ctrl-w l` | Normal | `pane.focus-right` | Focus the pane to the right |
| `<space>ol` | Normal | `tab.last-active` | Previously active tab |
| `Ctrl-^` | Normal, Visual, Insert | `tab.last-active` | Previously active tab |
| `<space><Tab>` | Normal | `tab.last-active` | Previously active tab |
| `Ctrl-w >` | Normal | `pane.wider` | Make the focused pane wider |
| `Ctrl-w <` | Normal | `pane.narrower` | Make the focused pane narrower |
| `Ctrl-w +` | Normal | `pane.taller` | Make the focused pane taller |
| `Ctrl-w -` | Normal | `pane.shorter` | Make the focused pane shorter |
| `Ctrl-w =` | Normal | `pane.reset-sizes` | Reset the pane sizes |
| `<space>z` | Normal | `pane.zoom` | Zoom the focused pane |
| `<space>x` | Normal | `status.dismiss` | Dismiss the status message |
| `<space>m` | Normal | `status.history` | Show the status message history |
| `<space>*` | Normal | `view.cycle-command-level` | Cycle the command level (Everyday, Advanced, Advanced+) |
| `<space>,` | Normal | `settings.open` | Open the settings |
| `<space>s` | Normal | `working-tree.toggle` | Open or leave the Changes view |
| `<space>f` `<space>rf` | Normal | `remote.fetch` | Fetch the remote of the current branch (prunes) |
| `<space>p` `<space>rl` | Normal | `remote.pull` | Pull the current branch (fast-forward only) |
| `<space>P` `<space>rp` | Normal | `remote.push` | Push the current branch |
| `<space>rP` | Normal | `remote.push-force` | Force push the current branch with a lease (asks first) |
| `<space>bD` | Normal | `remote.delete-branch` | Delete a branch on a remote (asks first) |
| `<space>rc` | Normal | `remote.cancel` | Cancel the running fetch, pull or push |
| `<space>ra` | Normal | `remote.add` | Add a remote (name, then URL) |
| `<space>rd` | Normal | `remote.remove` | Remove a remote (asks first) |
| `<space>rn` | Normal | `remote.rename` | Rename a remote |
| `<space>ru` | Normal | `remote.set-url` | Change the URL of a remote |
| `<space>ks` | Normal | `stash.push` | Stash the tracked changes (asks for a message) |
| `<space>ku` | Normal | `stash.push-untracked` | Stash the tracked and untracked changes (asks for a message) |
| `<space>gc` | Normal | `operation.continue` | Continue the merge, rebase, cherry-pick or revert in progress |
| `<space>ga` | Normal | `operation.abort` | Abort the merge, rebase, cherry-pick or revert in progress (asks first) |
| `<space>gs` | Normal | `operation.skip` | Skip the commit a rebase, cherry-pick or revert stopped on (asks first) |
| `<space>gr` | Normal | `conflict.open` | Open the conflict editor (selected or first conflicted file) |
| `<space>em` | Normal | `tool.mergetool` | Resolve conflicts in the mergetool |
| `Esc` | Normal, Visual, Insert, Command, Confirm | `cancel` | Cancel |
| `Enter` | Insert | `input.accept` | Accept the input |
| `Enter` | Command | `command.execute` | Run the command |
| `y` | Confirm | `confirm.yes` | Answer yes |
| `n` | Confirm | `confirm.no` | Answer no |

## Lists

| Keys | Modes | Action | What |
|---|---|---|---|
| `j` `Down` | Normal, Visual | `list.down` | Move down |
| `k` `Up` | Normal, Visual | `list.up` | Move up |
| `gg` | Normal, Visual | `list.top` | Go to the top |
| `G` | Normal, Visual | `list.bottom` | Go to the bottom |
| `/` | Normal | `search.open` | Search |
| `n` | Normal | `search.next` | Next search match |
| `N` | Normal | `search.previous` | Previous search match |
| `Ctrl-d` `PageDown` | Normal, Visual | `list.half-page-down` | Half page down |
| `Ctrl-u` `PageUp` | Normal, Visual | `list.half-page-up` | Half page up |
| `v` | Normal, Visual | `visual.toggle` | Start or end range selection |
| `y` | Visual | `visual.copy` | Copy the selected range |
| `Alt-v` | Normal, Visual | `visual.toggle-chars` | Start or end character selection |
| `h` | Visual | `visual.char-left` | Move the character selection one character left |
| `l` | Visual | `visual.char-right` | Move the character selection one character right |
| `w` | Visual | `visual.word-next` | Move the character selection to the next word |
| `b` | Visual | `visual.word-previous` | Move the character selection to the previous word |
| `0` | Visual | `visual.line-start` | Move the character selection to the start of the line |
| `$` | Visual | `visual.line-end` | Move the character selection to the end of the line |
| `iw` | Visual | `visual.select-word` | Select the word under the cursor (or the pointer) |
| `il` | Visual | `visual.select-line` | Select the text of the line (or the line under the pointer) |

## Graph

| Keys | Modes | Action | What |
|---|---|---|---|
| `<space>h` | Normal | `graph.jump-to-head` | Jump to HEAD |
| `<space>J` | Normal | `graph.jump-to-ref` | Jump to a ref or hash |
| `<space>j` | Normal | `view.quick-jump-to-row` | Quick jump to a visible row |
| `Enter` | Normal | `graph.open-details` | Focus the commit details |
| `y` | Normal | `graph.copy-hash` | Copy the commit hash |
| `<space>=` | Normal | `graph.compare` | Mark or compare a commit |
| `<space>or` | Normal | `repo.refresh` | Refresh the repository |
| `<space>ed` | Normal | `tool.difftool` | Open the commit in the difftool |
| `<space>ec` | Normal | `tool.commit-editor` | Commit the staged changes in $EDITOR |
| `]` | Normal | `detail.next-tab` | Next bottom pane tab |
| `[` | Normal | `detail.previous-tab` | Previous bottom pane tab |
| `gd` | Normal | `detail.tab-details` | Show the Details tab |
| `gc` | Normal | `detail.tab-changes` | Show the Changes tab |
| `gf` | Normal | `detail.tab-files` | Show the Files tab |
| `<space>bc` | Normal | `branch.create` | Create a branch and check it out |
| `<space>bC` | Normal | `branch.create-only` | Create a branch without checking out |
| `<space>bs` | Normal | `branch.switch` | Switch to a branch (check out) |
| `<space>bn` | Normal | `branch.rename` | Rename the branch |
| `<space>bd` | Normal | `branch.delete` | Delete the branch (asks first) |
| `<space>bu` | Normal | `branch.set-upstream` | Set the branch upstream |
| `<space>bU` | Normal | `branch.unset-upstream` | Unset the branch upstream |
| `<space>bf` | Normal | `branch.fast-forward` | Fast-forward the branch to its upstream (no checkout) |
| `<space>tc` | Normal | `tag.create` | Create a tag at the selected commit |
| `<space>td` | Normal | `tag.delete` | Delete the tag (asks first) |
| `<space>tp` | Normal | `tag.push` | Push the tag to a remote |
| `<space>tD` | Normal | `tag.delete-remote` | Delete the tag on a remote (asks first) |
| `<space>bx` | Normal | `reset.branch` | Reset the current branch to the selection |
| `<space>brr` | Normal | `rebase.onto` | Rebase the current branch onto the selection |
| `<space>bri` | Normal | `rebase.interactive` | Interactive rebase onto the selection (edit the todo) |
| `<space>brI` | Normal | `rebase.interactive-root` | Interactive rebase from the root commit (selection ignored) |
| `<space>brf` | Normal | `rebase.fixup` | Fix up the commit into the one before it (asks first) |
| `<space>brw` | Normal | `rebase.reword` | Reword the commit subject (asks first) |
| `<space>brd` | Normal | `rebase.drop` | Drop the commit from the branch (asks first) |
| `<space>l` | Normal | `reflog.open` | Open the reflog of HEAD (of the branch under the sidebar cursor) |
| `<space>.` | Normal | `menu.open-for-row` | Open the actions menu of the selected row |
| `<space>bm` | Normal | `merge.branch` | Merge the selection (fast-forward if possible) |
| `<space>bM` | Normal | `merge.branch-no-ff` | Merge the selection with a merge commit |
| `<space>bq` | Normal | `merge.branch-squash` | Squash-merge the selection (stages changes) |
| `<space>bp` | Normal, Visual | `pick.cherry-pick` | Cherry-pick the selection onto the current branch |
| `<space>bv` | Normal, Visual | `pick.revert` | Revert the selection with new commits |

## Sidebar

| Keys | Modes | Action | What |
|---|---|---|---|
| `<space>bc` | Normal | `branch.create` | Create a branch and check it out |
| `<space>bC` | Normal | `branch.create-only` | Create a branch without checking out |
| `<space>bs` | Normal | `branch.switch` | Switch to a branch (check out) |
| `<space>bn` | Normal | `branch.rename` | Rename the branch |
| `<space>bd` | Normal | `branch.delete` | Delete the branch (asks first) |
| `<space>bu` | Normal | `branch.set-upstream` | Set the branch upstream |
| `<space>bU` | Normal | `branch.unset-upstream` | Unset the branch upstream |
| `<space>bf` | Normal | `branch.fast-forward` | Fast-forward the branch to its upstream (no checkout) |
| `<space>tc` | Normal | `tag.create` | Create a tag at the selected commit |
| `<space>td` | Normal | `tag.delete` | Delete the tag (asks first) |
| `<space>tp` | Normal | `tag.push` | Push the tag to a remote |
| `<space>tD` | Normal | `tag.delete-remote` | Delete the tag on a remote (asks first) |
| `<space>.` | Normal | `menu.open-for-row` | Open the actions menu of the selected row |
| `<space>l` | Normal | `reflog.open` | Open the reflog of HEAD (of the branch under the sidebar cursor) |
| `<space>bx` | Normal | `reset.branch` | Reset the current branch to the selection |
| `<space>brr` | Normal | `rebase.onto` | Rebase the current branch onto the selection |
| `<space>bri` | Normal | `rebase.interactive` | Interactive rebase onto the selection (edit the todo) |
| `<space>brI` | Normal | `rebase.interactive-root` | Interactive rebase from the root commit (selection ignored) |
| `<space>bm` | Normal | `merge.branch` | Merge the selection (fast-forward if possible) |
| `<space>bM` | Normal | `merge.branch-no-ff` | Merge the selection with a merge commit |
| `<space>bq` | Normal | `merge.branch-squash` | Squash-merge the selection (stages changes) |
| `<space>bp` | Normal | `pick.cherry-pick` | Cherry-pick the selection onto the current branch |
| `<space>bv` | Normal | `pick.revert` | Revert the selection with new commits |
| `l` | Normal | `sidebar.expand` | Expand the sidebar entry |
| `Enter` | Normal | `sidebar.activate` | Activate the sidebar entry |
| `h` | Normal | `sidebar.collapse` | Collapse the sidebar entry |
| `/` | Normal | `sidebar.filter` | Filter the sidebar |
| `y` | Normal | `sidebar.copy` | Copy the branch, tag, stash name or worktree path |
| `<space>ka` | Normal | `stash.apply` | Apply the selected stash and keep it |
| `<space>kp` | Normal | `stash.pop` | Apply the selected stash and drop it |
| `<space>kd` | Normal | `stash.drop` | Drop the selected stash (asks first) |
| `<space>wa` | Normal | `worktree.add` | Add a worktree (branch, then path) |
| `<space>wd` | Normal | `worktree.remove` | Remove the selected worktree (asks first) |
| `<space>wl` | Normal | `worktree.lock` | Lock the selected worktree (optional reason) |
| `<space>wu` | Normal | `worktree.unlock` | Unlock the selected worktree |
| `<space>ws` | Normal | `worktree.switch` | Switch this tab to the selected worktree |
| `<space>wt` | Normal | `worktree.open` | Open the selected worktree (or the branch's, made if needed) in its own tab |
| `<space>wp` | Normal | `worktree.prune` | Prune worktrees whose directory is gone |

## DetailChanges

| Keys | Modes | Action | What |
|---|---|---|---|
| `<space>.` | Normal | `menu.open-for-row` | Open the actions menu of the selected row |
| `za` | Normal | `detail.toggle-folder` | Open or close the folder under the cursor (Changes tab, tree style) |
| `<space>vn` | Normal | `files.toggle-path-style` | Cycle the file paths: name first, full path, tree |
| `<space>vv` | Normal | `files.open-view-options` | Open the view options menu of the file list |
| `<space>v1` | Normal | `files.path-name-first` | Show file paths name first |
| `<space>v2` | Normal | `files.path-full-path` | Show file paths in full |
| `<space>v3` | Normal | `files.path-tree` | Show files as a tree |
| `<space>vp` | Normal | `diff.merge-parent` | Switch the merge commit's parent |
| `J` | Normal | `diff.scroll-down` | Scroll the diff down without focusing it |
| `K` | Normal | `diff.scroll-up` | Scroll the diff up without focusing it |
| `D` | Normal | `diff.scroll-half-page-down` | Scroll the diff half a page down without focusing it |
| `U` | Normal | `diff.scroll-half-page-up` | Scroll the diff half a page up without focusing it |
| `<space>kr` | Normal | `stash.restore-file` | Restore the selected file of the shown stash into the working tree (asks first) |

## DetailFiles

| Keys | Modes | Action | What |
|---|---|---|---|
| `<space>.` | Normal | `menu.open-for-row` | Open the actions menu of the selected row |
| `<space>vv` | Normal | `files.open-view-options` | Open the view options menu of the file list |
| `J` | Normal | `diff.scroll-down` | Scroll the diff down without focusing it |
| `K` | Normal | `diff.scroll-up` | Scroll the diff up without focusing it |
| `D` | Normal | `diff.scroll-half-page-down` | Scroll the diff half a page down without focusing it |
| `U` | Normal | `diff.scroll-half-page-up` | Scroll the diff half a page up without focusing it |

## WorkingUnstaged

| Keys | Modes | Action | What |
|---|---|---|---|
| `<space>.` | Normal | `menu.open-for-row` | Open the actions menu of the selected row |
| `<space>gb` | Normal | `blame.open` | Blame the selected file (at the shown commit, or the working tree file) |
| `<space>H` | Normal | `file-history.open` | Open the history of the selected file, renames followed |
| `<space>or` | Normal | `repo.refresh` | Refresh the repository |
| `y` | Normal | `working-tree.copy-path` | Copy the file or folder path |
| `h` | Normal | `working-tree.collapse` | Close the folder, else go to the parent folder |
| `za` | Normal | `working-tree.toggle-folder` | Open or close the folder under the cursor (tree style) |
| `Esc` | Normal | `navigate.back` | Go back |
| `<space>c` | Normal | `working-tree.write-message` | Write the commit message |
| `<space>ec` | Normal | `tool.commit-editor` | Commit the staged changes in $EDITOR |
| `Enter` | Normal | `working-tree.open-diff` | Go to the diff of the file or folder (a conflicted one: the conflict editor) |
| `l` | Normal | `working-tree.expand` | Open the folder under the cursor |
| `<space>j` | Normal | `view.quick-jump-to-row` | Quick jump to a visible row |
| `<space>vn` | Normal | `files.toggle-path-style` | Cycle the file paths: name first, full path, tree |
| `<space>vv` | Normal | `files.open-view-options` | Open the view options menu of the file list |
| `<space>v1` | Normal | `files.path-name-first` | Show file paths name first |
| `<space>v2` | Normal | `files.path-full-path` | Show file paths in full |
| `<space>v3` | Normal | `files.path-tree` | Show files as a tree |
| `J` | Normal | `diff.scroll-down` | Scroll the diff down without focusing it |
| `K` | Normal | `diff.scroll-up` | Scroll the diff up without focusing it |
| `D` | Normal | `diff.scroll-half-page-down` | Scroll the diff half a page down without focusing it |
| `U` | Normal | `diff.scroll-half-page-up` | Scroll the diff half a page up without focusing it |
| `<space>a` | Normal, Visual | `working-tree.stage` | Stage the file or folder, the hunk or the selected lines |
| `<space>A` | Normal | `working-tree.stage-all` | Stage all files |
| `<space>d` | Normal, Visual | `working-tree.discard` | Discard the changes of the file or folder, the hunk or the selected lines (asks first) |
| `Alt-s` | Normal, Visual, Insert | `working-tree.toggle-sign-off` | Commit option: SignOff (toggle) |
| `Alt-n` | Normal, Visual, Insert | `working-tree.toggle-no-verify` | Commit option: No-Verify (toggle) |
| `Alt-a` | Normal, Visual, Insert | `working-tree.toggle-amend` | Commit option: Amend (toggle) |
| `<space>1` | Normal | `working-tree.focus-unstaged` | Focus the Unstaged list |
| `<space>2` | Normal | `working-tree.focus-staged` | Focus the Staged list |
| `<space>3` | Normal | `working-tree.focus-diff` | Focus the diff |
| `<space>4` | Normal | `working-tree.focus-message` | Focus the commit message |

## WorkingStaged

| Keys | Modes | Action | What |
|---|---|---|---|
| `<space>.` | Normal | `menu.open-for-row` | Open the actions menu of the selected row |
| `<space>gb` | Normal | `blame.open` | Blame the selected file (at the shown commit, or the working tree file) |
| `<space>H` | Normal | `file-history.open` | Open the history of the selected file, renames followed |
| `<space>or` | Normal | `repo.refresh` | Refresh the repository |
| `y` | Normal | `working-tree.copy-path` | Copy the file or folder path |
| `h` | Normal | `working-tree.collapse` | Close the folder, else go to the parent folder |
| `za` | Normal | `working-tree.toggle-folder` | Open or close the folder under the cursor (tree style) |
| `Esc` | Normal | `navigate.back` | Go back |
| `<space>c` | Normal | `working-tree.write-message` | Write the commit message |
| `<space>ec` | Normal | `tool.commit-editor` | Commit the staged changes in $EDITOR |
| `Enter` | Normal | `working-tree.open-diff` | Go to the diff of the file or folder (a conflicted one: the conflict editor) |
| `l` | Normal | `working-tree.expand` | Open the folder under the cursor |
| `<space>j` | Normal | `view.quick-jump-to-row` | Quick jump to a visible row |
| `<space>vn` | Normal | `files.toggle-path-style` | Cycle the file paths: name first, full path, tree |
| `<space>vv` | Normal | `files.open-view-options` | Open the view options menu of the file list |
| `<space>v1` | Normal | `files.path-name-first` | Show file paths name first |
| `<space>v2` | Normal | `files.path-full-path` | Show file paths in full |
| `<space>v3` | Normal | `files.path-tree` | Show files as a tree |
| `J` | Normal | `diff.scroll-down` | Scroll the diff down without focusing it |
| `K` | Normal | `diff.scroll-up` | Scroll the diff up without focusing it |
| `D` | Normal | `diff.scroll-half-page-down` | Scroll the diff half a page down without focusing it |
| `U` | Normal | `diff.scroll-half-page-up` | Scroll the diff half a page up without focusing it |
| `<space>u` | Normal, Visual | `working-tree.unstage` | Unstage the file or folder, the hunk or the selected lines |
| `<space>U` | Normal | `working-tree.unstage-all` | Unstage all files |
| `Alt-s` | Normal, Visual, Insert | `working-tree.toggle-sign-off` | Commit option: SignOff (toggle) |
| `Alt-n` | Normal, Visual, Insert | `working-tree.toggle-no-verify` | Commit option: No-Verify (toggle) |
| `Alt-a` | Normal, Visual, Insert | `working-tree.toggle-amend` | Commit option: Amend (toggle) |
| `<space>1` | Normal | `working-tree.focus-unstaged` | Focus the Unstaged list |
| `<space>2` | Normal | `working-tree.focus-staged` | Focus the Staged list |
| `<space>3` | Normal | `working-tree.focus-diff` | Focus the diff |
| `<space>4` | Normal | `working-tree.focus-message` | Focus the commit message |

## Detail

| Keys | Modes | Action | What |
|---|---|---|---|
| `<space>gb` | Normal | `blame.open` | Blame the selected file (at the shown commit, or the working tree file) |
| `<space>H` | Normal | `file-history.open` | Open the history of the selected file, renames followed |
| `Enter` | Normal | `detail.open-diff` | Go to the file diff |
| `l` | Normal | `detail.expand` | Open the directory or folder under the cursor |
| `<space>od` | Normal | `detail.retry` | Retry loading the commit details |
| `y` | Normal | `detail.copy-path` | Copy the file path |
| `gx` | Normal | `detail.open-link` | Open the issue link in the message line (a count picks the nth link) |
| `<space>j` | Normal | `view.quick-jump-to-row` | Quick jump to a visible row |
| `h` | Normal | `detail.collapse` | Close the directory or folder under the cursor, else go to its parent |
| `/` | Normal | `detail.filter` | Filter the file tree by path (Files tab) |
| `Esc` | Normal | `navigate.back` | Go back |
| `]` | Normal | `detail.next-tab` | Next bottom pane tab |
| `[` | Normal | `detail.previous-tab` | Previous bottom pane tab |
| `gd` | Normal | `detail.tab-details` | Show the Details tab |
| `gc` | Normal | `detail.tab-changes` | Show the Changes tab |
| `gf` | Normal | `detail.tab-files` | Show the Files tab |

## Blame

| Keys | Modes | Action | What |
|---|---|---|---|
| `<space>L` | Normal, Visual | `line-history.open` | Open the history of the selected diff lines (the Visual range, else the cursor line) |
| `Enter` | Normal | `blame.show-commit` | Select the blame line's commit in the graph |
| `Alt-p` | Normal | `blame.parent` | Blame the file at the parent of the highlighted line's commit (Esc steps back) |
| `<space>H` | Normal | `file-history.open` | Open the history of the selected file, renames followed |
| `Alt-w` | Normal | `blame.toggle-whitespace` | Blame: ignore whitespace changes (-w) |
| `Alt-m` | Normal | `blame.toggle-moves` | Blame: detect lines moved inside the file (-M) |
| `Alt-c` | Normal | `blame.toggle-copies` | Blame: detect lines copied from other files (-C) |
| `h` `Esc` | Normal | `navigate.back` | Go back |
| `/` | Normal | `search.open` | Search |
| `n` | Normal | `search.next` | Next search match |
| `N` | Normal | `search.previous` | Previous search match |

## Reflog

| Keys | Modes | Action | What |
|---|---|---|---|
| `Enter` | Normal | `reflog.select-commit` | Select the reflog entry's commit in the graph |
| `h` `Esc` | Normal | `navigate.back` | Go back |
| `<space>or` | Normal | `repo.refresh` | Refresh the repository |
| `<space>l` | Normal | `reflog.open` | Open the reflog of HEAD (of the branch under the sidebar cursor) |
| `/` | Normal | `search.open` | Search |
| `n` | Normal | `search.next` | Next search match |
| `N` | Normal | `search.previous` | Previous search match |
| `<space>bx` | Normal | `reset.branch` | Reset the current branch to the selection |
| `<space>bc` | Normal | `branch.create` | Create a branch and check it out |
| `<space>bs` | Normal | `branch.switch` | Switch to a branch (check out) |

## Diff

| Keys | Modes | Action | What |
|---|---|---|---|
| `<space>L` | Normal, Visual | `line-history.open` | Open the history of the selected diff lines (the Visual range, else the cursor line) |
| `]c` | Normal | `diff.next-hunk` | Next hunk |
| `[c` | Normal | `diff.previous-hunk` | Previous hunk |
| `]f` | Normal | `diff.next-file` | Next file |
| `[f` | Normal | `diff.previous-file` | Previous file |
| `Esc` | Normal | `navigate.back` | Go back |
| `<space>vw` | Normal | `diff.toggle-wrap` | Toggle word wrap in the diff |
| `<space>vv` | Normal | `diff.open-view-options` | Open the view options menu of the diff |
| `<space>vl` | Normal | `diff.load-anyway` | Load the whole diff (lift the size limits) |
| `<space>vp` | Normal | `diff.merge-parent` | Switch the merge commit's parent |
| `+` | Normal | `diff.more-context` | Show more lines around the changes in the diff |
| `-` | Normal | `diff.less-context` | Show fewer lines around the changes in the diff |
| `<space>vf` | Normal | `diff.toggle-whole-file` | Toggle showing whole files in the diff |
| `<space>vi` | Normal | `diff.toggle-ignore-whitespace` | Toggle ignoring whitespace changes in the diff |
| `<space>vs` | Normal | `diff.toggle-side-by-side` | Toggle the side-by-side diff |
| `<space>vc` | Normal | `diff.toggle-whitespace` | Toggle whitespace marks in the diff |
| `/` | Normal | `diff.search-open` | Search in the diff |
| `n` | Normal | `diff.search-next` | Next diff search match |
| `N` | Normal | `diff.search-previous` | Previous diff search match |
| `y` | Normal | `diff.copy-line` | Copy the current diff line |
| `Y` | Normal | `diff.copy-file` | Copy the displayed rows of the current file |
| `<space>j` | Normal | `view.quick-jump-to-row` | Quick jump to a visible row |

## WorkingUnstagedDiff

| Keys | Modes | Action | What |
|---|---|---|---|
| `<space>L` | Normal, Visual | `line-history.open` | Open the history of the selected diff lines (the Visual range, else the cursor line) |
| `<space>or` | Normal | `repo.refresh` | Refresh the repository |
| `]c` | Normal | `diff.next-hunk` | Next hunk |
| `[c` | Normal | `diff.previous-hunk` | Previous hunk |
| `]f` | Normal | `diff.next-file` | Next file |
| `[f` | Normal | `diff.previous-file` | Previous file |
| `Esc` | Normal | `navigate.back` | Go back |
| `<space>vw` | Normal | `diff.toggle-wrap` | Toggle word wrap in the diff |
| `<space>vv` | Normal | `diff.open-view-options` | Open the view options menu of the diff |
| `<space>vl` | Normal | `diff.load-anyway` | Load the whole diff (lift the size limits) |
| `+` | Normal | `diff.more-context` | Show more lines around the changes in the diff |
| `-` | Normal | `diff.less-context` | Show fewer lines around the changes in the diff |
| `<space>vf` | Normal | `diff.toggle-whole-file` | Toggle showing whole files in the diff |
| `<space>vi` | Normal | `diff.toggle-ignore-whitespace` | Toggle ignoring whitespace changes in the diff |
| `<space>vs` | Normal | `diff.toggle-side-by-side` | Toggle the side-by-side diff |
| `<space>vc` | Normal | `diff.toggle-whitespace` | Toggle whitespace marks in the diff |
| `/` | Normal | `diff.search-open` | Search in the diff |
| `n` | Normal | `diff.search-next` | Next diff search match |
| `N` | Normal | `diff.search-previous` | Previous diff search match |
| `y` | Normal | `diff.copy-line` | Copy the current diff line |
| `Y` | Normal | `diff.copy-file` | Copy the displayed rows of the current file |
| `<space>j` | Normal | `view.quick-jump-to-row` | Quick jump to a visible row |
| `<space>c` | Normal | `working-tree.write-message` | Write the commit message |
| `<space>ec` | Normal | `tool.commit-editor` | Commit the staged changes in $EDITOR |
| `<space>a` | Normal, Visual | `working-tree.stage` | Stage the file or folder, the hunk or the selected lines |
| `<space>d` | Normal, Visual | `working-tree.discard` | Discard the changes of the file or folder, the hunk or the selected lines (asks first) |
| `Alt-s` | Normal, Visual, Insert | `working-tree.toggle-sign-off` | Commit option: SignOff (toggle) |
| `Alt-n` | Normal, Visual, Insert | `working-tree.toggle-no-verify` | Commit option: No-Verify (toggle) |
| `Alt-a` | Normal, Visual, Insert | `working-tree.toggle-amend` | Commit option: Amend (toggle) |
| `<space>1` | Normal | `working-tree.focus-unstaged` | Focus the Unstaged list |
| `<space>2` | Normal | `working-tree.focus-staged` | Focus the Staged list |
| `<space>3` | Normal | `working-tree.focus-diff` | Focus the diff |
| `<space>4` | Normal | `working-tree.focus-message` | Focus the commit message |

## WorkingStagedDiff

| Keys | Modes | Action | What |
|---|---|---|---|
| `<space>L` | Normal, Visual | `line-history.open` | Open the history of the selected diff lines (the Visual range, else the cursor line) |
| `<space>or` | Normal | `repo.refresh` | Refresh the repository |
| `]c` | Normal | `diff.next-hunk` | Next hunk |
| `[c` | Normal | `diff.previous-hunk` | Previous hunk |
| `]f` | Normal | `diff.next-file` | Next file |
| `[f` | Normal | `diff.previous-file` | Previous file |
| `Esc` | Normal | `navigate.back` | Go back |
| `<space>vw` | Normal | `diff.toggle-wrap` | Toggle word wrap in the diff |
| `<space>vv` | Normal | `diff.open-view-options` | Open the view options menu of the diff |
| `<space>vl` | Normal | `diff.load-anyway` | Load the whole diff (lift the size limits) |
| `+` | Normal | `diff.more-context` | Show more lines around the changes in the diff |
| `-` | Normal | `diff.less-context` | Show fewer lines around the changes in the diff |
| `<space>vf` | Normal | `diff.toggle-whole-file` | Toggle showing whole files in the diff |
| `<space>vi` | Normal | `diff.toggle-ignore-whitespace` | Toggle ignoring whitespace changes in the diff |
| `<space>vs` | Normal | `diff.toggle-side-by-side` | Toggle the side-by-side diff |
| `<space>vc` | Normal | `diff.toggle-whitespace` | Toggle whitespace marks in the diff |
| `/` | Normal | `diff.search-open` | Search in the diff |
| `n` | Normal | `diff.search-next` | Next diff search match |
| `N` | Normal | `diff.search-previous` | Previous diff search match |
| `y` | Normal | `diff.copy-line` | Copy the current diff line |
| `Y` | Normal | `diff.copy-file` | Copy the displayed rows of the current file |
| `<space>j` | Normal | `view.quick-jump-to-row` | Quick jump to a visible row |
| `<space>c` | Normal | `working-tree.write-message` | Write the commit message |
| `<space>ec` | Normal | `tool.commit-editor` | Commit the staged changes in $EDITOR |
| `<space>u` | Normal, Visual | `working-tree.unstage` | Unstage the file or folder, the hunk or the selected lines |
| `Alt-s` | Normal, Visual, Insert | `working-tree.toggle-sign-off` | Commit option: SignOff (toggle) |
| `Alt-n` | Normal, Visual, Insert | `working-tree.toggle-no-verify` | Commit option: No-Verify (toggle) |
| `Alt-a` | Normal, Visual, Insert | `working-tree.toggle-amend` | Commit option: Amend (toggle) |
| `<space>1` | Normal | `working-tree.focus-unstaged` | Focus the Unstaged list |
| `<space>2` | Normal | `working-tree.focus-staged` | Focus the Staged list |
| `<space>3` | Normal | `working-tree.focus-diff` | Focus the diff |
| `<space>4` | Normal | `working-tree.focus-message` | Focus the commit message |

## FileHistory

| Keys | Modes | Action | What |
|---|---|---|---|
| `Enter` | Normal | `file-history.select-commit` | Select the file history commit in the graph |
| `h` `Esc` | Normal | `navigate.back` | Go back |
| `<space>or` | Normal | `repo.refresh` | Refresh the repository |
| `J` | Normal | `diff.scroll-down` | Scroll the diff down without focusing it |
| `K` | Normal | `diff.scroll-up` | Scroll the diff up without focusing it |
| `D` | Normal | `diff.scroll-half-page-down` | Scroll the diff half a page down without focusing it |
| `U` | Normal | `diff.scroll-half-page-up` | Scroll the diff half a page up without focusing it |

## RebaseEditor

| Keys | Modes | Action | What |
|---|---|---|---|
| `p` | Normal | `rebase-editor.pick` | Rebase todo: keep the commit as it is |
| `r` | Normal | `rebase-editor.reword` | Rebase todo: keep the commit with a new message (opens the message editor) |
| `s` | Normal | `rebase-editor.squash` | Rebase todo: meld the commit into the one above and keep both messages |
| `f` | Normal | `rebase-editor.fixup` | Rebase todo: meld the commit into the one above and keep that one's message |
| `d` | Normal | `rebase-editor.drop` | Rebase todo: remove the commit |
| `K` `Alt-k` | Normal | `rebase-editor.move-up` | Rebase todo: replay the commit earlier (move it up) |
| `J` `Alt-j` | Normal | `rebase-editor.move-down` | Rebase todo: replay the commit later (move it down) |
| `Enter` | Normal | `rebase-editor.start` | Rebase todo: start the rebase (asks first) |
| `Esc` `h` | Normal | `navigate.back` | Go back |
| `Enter` | Insert | `rebase-editor.message-newline` | Rebase todo: new line in the message |
| `Ctrl-g` | Insert | `rebase-editor.message-save` | Rebase todo: keep the edited message |

## ConflictEditor

| Keys | Modes | Action | What |
|---|---|---|---|
| `n` `]c` | Normal | `conflict-editor.next` | Conflict editor: go to the next conflict |
| `N` `[c` | Normal | `conflict-editor.previous` | Conflict editor: go to the previous conflict |
| `u` | Normal | `conflict-editor.undo` | Conflict editor: undo the last pick or edit |
| `Ctrl-r` | Normal | `conflict-editor.redo` | Conflict editor: redo the undone pick or edit |
| `<space>u` | Normal, Visual | `conflict-editor.untake` | Conflict editor: untake, make the conflict unresolved again (a Visual selection: remove only its lines) |
| `zh` | Normal, Visual | `conflict-editor.scroll-left` | Conflict editor: scroll the base, ours and theirs panes sideways to the left |
| `zl` | Normal, Visual | `conflict-editor.scroll-right` | Conflict editor: scroll the base, ours and theirs panes sideways to the right |
| `Ctrl-x` | Normal | `conflict-editor.recreate` | Conflict editor: re-create the conflict of the file (asks first) |
| `Ctrl-s` | Normal | `conflict-editor.save` | Conflict editor: save the file and stage it when resolved |
| `Esc` | Normal | `navigate.back` | Go back |

## ConflictResult

| Keys | Modes | Action | What |
|---|---|---|---|
| `Ctrl-s` | Insert | `conflict-editor.save` | Conflict editor: save the file and stage it when resolved |
| `i` | Normal | `conflict-editor.edit` | Conflict editor: move into the result at this conflict and start typing (Insert mode) |
| `h` | Normal | `working-tree.message-left` | Message box and conflict result: cursor left |
| `l` | Normal | `working-tree.message-right` | Message box and conflict result: cursor right |
| `w` | Normal | `working-tree.message-word-next` | Message box and conflict result: next word |
| `b` | Normal | `working-tree.message-word-previous` | Message box and conflict result: previous word |
| `0` | Normal | `working-tree.message-line-start` | Message box and conflict result: line start |
| `$` | Normal | `working-tree.message-line-end` | Message box and conflict result: line end |
| `a` | Normal | `working-tree.message-append` | Message box and conflict result: insert after the cursor |
| `A` | Normal | `working-tree.message-append-at-end` | Message box and conflict result: insert at the line end |
| `I` | Normal | `working-tree.message-insert-at-start` | Message box and conflict result: insert at the line start |
| `o` | Normal | `working-tree.message-open-line` | Message box and conflict result: open a line below and insert |
| `x` | Normal | `working-tree.message-delete-char` | Message box and conflict result: delete the character under the cursor |
| `dd` | Normal | `working-tree.message-delete-line` | Message box and conflict result: delete the line |
| `Enter` | Insert | `conflict-editor.edit-newline` | Conflict editor: insert a new line in the result |
| `Ctrl-g` | Insert | `conflict-editor.edit-done` | Conflict editor: stop typing, keep the text |

## ConflictSide

| Keys | Modes | Action | What |
|---|---|---|---|
| `<space>a` | Normal, Visual | `conflict-editor.take` | Conflict editor: take this pane's version for the conflict (a Visual selection: only its lines) |
| `o` | Normal | `conflict-editor.ours` | Conflict editor: take our side for the conflict |
| `t` | Normal | `conflict-editor.theirs` | Conflict editor: take their side for the conflict |
| `b` | Normal | `conflict-editor.base` | Conflict editor: take the common ancestor for the conflict |
| `m` | Normal | `conflict-editor.both` | Conflict editor: take both sides for the conflict, ours first |
| `O` | Normal | `conflict-editor.all-ours` | Conflict editor: take our side for every unresolved conflict of the file |
| `T` | Normal | `conflict-editor.all-theirs` | Conflict editor: take their side for every unresolved conflict of the file |
| `e` | Normal | `conflict-editor.edit` | Conflict editor: move into the result at this conflict and start typing (Insert mode) |
| `h` | Normal | `navigate.back` | Go back |

## Settings

| Keys | Modes | Action | What |
|---|---|---|---|
| `Enter` `l` | Normal | `settings.activate` | Settings: enter the column, edit the setting or keep the option |
| `h` `Esc` | Normal | `navigate.back` | Go back |
| `x` | Normal | `settings.toggle` | Settings: flip the toggle under the cursor |
| `/` | Normal | `settings.filter` | Settings: filter the settings by name and help text |
| `a` | Normal | `settings.add` | Settings: add a folder to the list under the cursor |
| `d` | Normal | `settings.remove` | Settings: remove the folder under the cursor (asks first) |
| `J` | Normal | `settings.move-down` | Settings: move the folder down in its list |
| `K` | Normal | `settings.move-up` | Settings: move the folder up in its list |
| `+` | Normal | `settings.increase` | Settings: raise the number under the cursor |
| `-` | Normal | `settings.decrease` | Settings: lower the number under the cursor |

## WorkingMessage

| Keys | Modes | Action | What |
|---|---|---|---|
| `i` `Enter` | Normal | `working-tree.edit-message` | Edit the commit message |
| `<space>c` | Normal | `working-tree.commit` | Commit the staged changes |
| `<space>ec` | Normal | `tool.commit-editor` | Commit the staged changes in $EDITOR |
| `<space>or` | Normal | `repo.refresh` | Refresh the repository |
| `Esc` | Normal | `navigate.back` | Go back |
| `h` | Normal | `working-tree.message-left` | Message box and conflict result: cursor left |
| `j` | Normal | `working-tree.message-down` | Message box: cursor down |
| `k` | Normal | `working-tree.message-up` | Message box: cursor up |
| `l` | Normal | `working-tree.message-right` | Message box and conflict result: cursor right |
| `w` | Normal | `working-tree.message-word-next` | Message box and conflict result: next word |
| `b` | Normal | `working-tree.message-word-previous` | Message box and conflict result: previous word |
| `0` | Normal | `working-tree.message-line-start` | Message box and conflict result: line start |
| `$` | Normal | `working-tree.message-line-end` | Message box and conflict result: line end |
| `a` | Normal | `working-tree.message-append` | Message box and conflict result: insert after the cursor |
| `A` | Normal | `working-tree.message-append-at-end` | Message box and conflict result: insert at the line end |
| `I` | Normal | `working-tree.message-insert-at-start` | Message box and conflict result: insert at the line start |
| `o` | Normal | `working-tree.message-open-line` | Message box and conflict result: open a line below and insert |
| `x` | Normal | `working-tree.message-delete-char` | Message box and conflict result: delete the character under the cursor |
| `dd` | Normal | `working-tree.message-delete-line` | Message box and conflict result: delete the line |
| `u` | Normal | `working-tree.message-undo` | Message box: undo |
| `Ctrl-r` | Normal | `working-tree.message-redo` | Message box: redo |
| `viw` | Normal | `working-tree.message-select-word` | Message box: select the word at the cursor |
| `Ctrl-d` | Normal | `working-tree.message-scroll-down` | Message box: scroll down (3 lines) |
| `Ctrl-u` | Normal | `working-tree.message-scroll-up` | Message box: scroll up (3 lines) |
| `Enter` | Insert | `working-tree.message-new-line` | Start a new line in the commit message |
| `Ctrl-g` | Insert | `working-tree.commit` | Commit the staged changes |
| `Alt-s` | Normal, Visual, Insert | `working-tree.toggle-sign-off` | Commit option: SignOff (toggle) |
| `Alt-n` | Normal, Visual, Insert | `working-tree.toggle-no-verify` | Commit option: No-Verify (toggle) |
| `Alt-a` | Normal, Visual, Insert | `working-tree.toggle-amend` | Commit option: Amend (toggle) |
| `<space>1` | Normal | `working-tree.focus-unstaged` | Focus the Unstaged list |
| `<space>2` | Normal | `working-tree.focus-staged` | Focus the Staged list |
| `<space>3` | Normal | `working-tree.focus-diff` | Focus the diff |
| `<space>4` | Normal | `working-tree.focus-message` | Focus the commit message |

## Picker

| Keys | Modes | Action | What |
|---|---|---|---|
| `Up` `Ctrl-k` | Insert | `picker.previous` | Previous picker entry |
| `Down` `Ctrl-j` | Insert | `picker.next` | Next picker entry |
| `Enter` `Ctrl-l` | Insert | `picker.accept` | Open the repository |
| `Tab` `Right` | Insert | `picker.complete-path` | Accept the suggestion / complete the path |
| `Alt-p` | Insert | `picker.pin` | Pin to the top of the picker (toggle) |
| `Alt-d` | Insert | `picker.remove-recent` | Remove from recents |
| `Alt-c` | Insert | `picker.clone` | Clone a repository into a new tab |
| `Alt-i` | Insert | `picker.init` | Initialise a directory as a repository |

## Palette

| Keys | Modes | Action | What |
|---|---|---|---|
| `Up` `Ctrl-k` | Command | `palette.previous` | Previous palette entry |
| `Down` `Ctrl-j` | Command | `palette.next` | Next palette entry |
| `Ctrl-l` | Command | `command.execute` | Run the command |

## ContextMenu

| Keys | Modes | Action | What |
|---|---|---|---|
| `j` `Down` `Ctrl-j` | Normal | `palette.next` | Next palette entry |
| `k` `Up` `Ctrl-k` | Normal | `palette.previous` | Previous palette entry |
| `Enter` `Ctrl-l` | Normal | `command.execute` | Run the command |

## Help

| Keys | Modes | Action | What |
|---|---|---|---|
| `/` | Normal | `help.search` | Filter the help rows by key, title or action id |
| `Tab` `j` `Down` | Normal | `help.tab-next` | Next help section |
| `Shift-Tab` `k` `Up` | Normal | `help.tab-previous` | Previous help section |
| `J` | Normal | `help.scroll-down` | Scroll the help page down one line |
| `K` | Normal | `help.scroll-up` | Scroll the help page up one line |
| `D` | Normal | `help.scroll-half-page-down` | Scroll the help page down half a page |
| `U` | Normal | `help.scroll-half-page-up` | Scroll the help page up half a page |

## ContextRepeat

| Keys | Modes | Action | What |
|---|---|---|---|
| `+` | Normal | `diff.more-context` | Show more lines around the changes in the diff |
| `-` | Normal | `diff.less-context` | Show fewer lines around the changes in the diff |
| `Esc` | Normal | `repeat.end` | Leave the repeat mode |

## ResizeRepeat

| Keys | Modes | Action | What |
|---|---|---|---|
| `>` | Normal | `pane.wider` | Make the focused pane wider |
| `<` | Normal | `pane.narrower` | Make the focused pane narrower |
| `+` | Normal | `pane.taller` | Make the focused pane taller |
| `-` | Normal | `pane.shorter` | Make the focused pane shorter |
| `=` | Normal | `pane.reset-sizes` | Reset the pane sizes |
| `Esc` | Normal | `repeat.end` | Leave the repeat mode |

## Actions without a key

No default key; reached from the command palette (`:`), the actions menu, the mouse or a prompt.

| Action | What |
|---|---|
| `help.tab-select` | Show the clicked help section |
| `pick.mainline` | Choose the mainline parent in the open cherry-pick or revert prompt |
| `remote.fetch-all` | Fetch all remotes (prunes) |
| `settings.open-theme` | Open the settings at the theme |
| `working-tree.open` | Open the Changes view |
