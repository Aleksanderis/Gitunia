# UX: keymap

Part of the UX spec; start at [ux.md](ux.md). The key tables are generated: [keymap.md](keymap.md) lists every default binding per view.

## Keymap

The single source of truth for bindings is the data file `src/Gitunia.Core/Keymap/keymap.default.json` (one row per action, view and mode set: `action`, `view`, `modes`, `keys` with the main key first; a superset of the config `keys` entry, which has one `mode` and one `key`). Change a default there, not in code, and regenerate the reference with `scripts/gen-keymap-doc.sh` (`tools/KeymapDoc`); never edit `keymap.md` by hand.
Tests on the file (no test parses a table in this document): `keymap.md` matches what the generator renders from the file, every action id exists in `ActionIds`, no key is bound twice in a view and mode, every action in `ActionIds` is bound or listed as unbound in the test (mouse-only, modal or palette-only), and `DefaultKeymap` has no conflicts.
Add the row to the JSON in the same change that adds a binding; no conflicts within a mode/view. A new action also needs an `ActionCatalog` title (the reference shows it).
The layout was redesigned in #840 (design #833, "Keymap rework" below); the generated reference is `docs/keymap.md`.
Notation: `Ctrl-w c` is a chord, `<space>o` a leader chord, `<` is a literal `<` (config files also accept `<lt>`), `:name` is an ex command and not a key.
Modes: a row's `modes` say where its keys are active; `Visual` repeats the Lists motions; global `Ctrl-`/`Alt-` rows also work in Insert; `Esc` is bound in every mode; Insert/Command `Enter` accept the input; Confirm mode has only `y` (yes), `n` (no) and `Esc` (no), so a stray `Enter` never confirms a destructive action. `Picker` keys are Insert mode and `Palette` keys Command mode.
Key notes that the tables cannot carry:

- `Ctrl-c` force-quits from any mode or modal (cancels running git work, leaves at once; a failed session save still warns once, like `q`); it is matched before the keymap, so it cannot be remapped away.
- `Alt-1`..`Alt-9` jump to tab N; terminal multiplexers may take `Alt-N` for themselves, `3gt` (a count before `gt`) does the same and never conflicts.
- `Ctrl-h` `Ctrl-j` `Ctrl-k` `Ctrl-l` and `Ctrl-Backspace` focus panes in Normal mode only. `Ctrl-j` arrives as `Ctrl+Enter`, distinct from `Enter`; `Ctrl-h` is `Backspace` (0x08) in many terminals, so `Ctrl-Backspace` (focus left) is bound too and plain `Backspace` stays unbound in Normal mode. Where a terminal or multiplexer swallows them, `Tab` and `Ctrl-w h/j/k/l` still work; remap in the config file.
- `Ctrl-w >` `Ctrl-w <` move the divider beside the focused pane by 2 columns (a count multiplies; dragging a divider does the same); `Ctrl-w +` `Ctrl-w -` move the edge between the graph and the bottom panes by 1 row; `Ctrl-w =` resets the sizes. The `ResizeRepeat` and `ContextRepeat` views are the short-lived repeat modes after such a step: any other key ends the mode.
- `Ctrl-^` has a second binding, `<space><Tab>`, for terminals that drop it.
- `Esc` ladder in Normal mode: cancels a pending chord, then clears the search highlight, leaves Insert, cancels running remote work in the graph, else dismisses the status message.
- Visual `y` copies the range without `+`/`-`/space markers or gutter, a wrapped row once, notes skipped; above about 75 KB it refuses, and a failed copy keeps the range.
- `<space>bd` asks a second Confirm when the branch is unmerged; `<space>gr` with no conflicted file says so in the status bar.
- Working tree, `Enter` on a screen under 80 columns: the diff replaces the list until `Esc`.
- Graph search prefixes: `author:`, `hash:`, `body:`.

**Actions menu** (`<space>.` in the graph or sidebar, a double-click on a graph row, or a click on the `...` marker at the right edge of the selected sidebar row; #471, #479, #482).
A small popup that hangs under the row (above it near the bottom of the screen), titled `Actions`, one line per action of that row with its key at the right edge, so the menu teaches the keyboard path.
The menu is as wide as its longest title plus key needs, up to 96 columns, so no title is cut at 120 columns or more (a test renders every row menu at 100, 120 and 160; titles in `ActionCatalog` stay short, details belong in the Confirm; #573).
It is not the command palette (`:`): no text field, no filter.
The command palette is up to 100 columns wide so its titles are not cut either.
The same holds for the other popups (#597): Help and which-key take up to 96 columns and settings up to 100; Confirm, the prompt popup and the mainline picker fit their longest line, from 64, 64 and 72 columns up to 96, so a short dialog stays compact (the layout at 80 columns changes too, wherever the content needs more than the old limit).
`j`/`k`/`Down`/`Up` move, `Enter` or a click runs the highlighted action, `Esc` or a click outside closes it; the action runs after the menu closes, exactly as its key would, so its prompt popup or Confirm opens as usual.
A resize closes it.
There is no right-, middle- or Ctrl-click binding (they feel like a desktop app and multiplexers may swallow them, #479): the mouse path is a double-click on a graph row (the first click selects it) or a plain left click on the `...` marker that the selected sidebar row shows in its last three columns (not shown in panes narrower than 20 columns; a first click on another row only selects it, as before).
Graph rows have no marker and use their full width (#482).
Actions without a key in the pane are not listed.
The list depends on the row:

- Graph commit: new branch (`bc`, `bC`), switch, tag create, delete, push, remote delete, merge, merge-commit, squash, rebase, interactive rebase, reset, cherry-pick, revert, fixup, reword, drop (quick rebase, #459), copy hash, mark or compare a commit (`<space>=`, #535), open details (`Enter`; the mouse route to the details pane; on screens under 14 rows the pane is hidden, so the row is left out and `Enter` shows a notice, #486, #490).
- Sidebar local branch: switch, new branch, new-only, rename, delete, set and unset upstream, merge (three kinds), rebase, interactive rebase, reset, cherry-pick, revert. Remote branch: the same without rename, delete and upstream.
- Sidebar tag: tag create, delete, push, remote delete, new branch, new-only, merge (three kinds), rebase, interactive rebase, reset, cherry-pick, revert.
- Sidebar worktree: switch, open, add, remove, lock, unlock, prune.
- Sidebar stash: apply, pop, drop, push, push-all.
- Section headers: Branches new, new-only; Tags create; Worktrees add, prune; Stashes push, push-all. Remotes fetch, fetch all, pull, push, force push, delete remote branch, add, remove, rename, set URL.

The menu is the mouse path of every sidebar action; the header chips it replaced are gone (#469).

**Title dropdown (#645, `view ▾`).** The reusable title-row dropdown is the actions menu popup (above) opened under a title button: titled `View`, one line per option with its key at the right edge (`+` / `-` context, `<space>vf` whole file, `<space>vi` ignore space, `<space>vs` split, `<space>vw` wrap, `<space>vc` whitespace marks, then `<space>z` zoom), `j`/`k`/`Down`/`Up` move, `Enter` or a click runs the option after the menu closes, `Esc` or a click outside closes it, a resize closes it.
It hangs under the button (above the row near the bottom of the screen), is as wide as its longest line needs, and when the screen is too small for it the status bar says so and the keys still work.
The history diff, the Changes tab diff and both working-tree diffs carry it; `<space>vv` opens it from the keyboard.
A file's content has no dropdown (its only option, wrap, stays a button).
The Changes tab, both working-tree lists and the Files tab use the same component (#646): titled `View`, hanging under a `view ▾` chip at the right end of their title row, opened by `<space>vv` or a click.
Changes and the working-tree lists offer the path style (`name first`, `full path`, `tree`, set directly; `<space>vn` still cycles), the Files tab the path filter (its only display option; the same as `/`).
A tab without options (Details) has no chip and `<space>vv` is not bound there.

**Grouped actions (#483).** The palette and the actions menu list actions under non-selectable subheaders (`Branch`, `Merge and rebase`, `Tag`, `Worktree`, `Stash`, `Working tree`, `Copy`, `Diff`, `Details`, `External tools`, `Navigate`, `View`, `Tabs`, `General`).
The group of an action comes from `ActionCatalog.GroupOf` (metadata, not a per-list hand-written list; a test fails when a bound action has none) and the which-key popup names its chord groups by noun (`WhichKeyBuilder`, one name per prefix).
`j`/`k`, the wheel and clicks only ever land on actions: a click on a subheader or a gap does nothing.
A blank gap line precedes every subheader except the first (#489), like the help overlay's sections, so it is visible in every colour tier and the ASCII set; on a short screen the gaps are dropped before any row (the menu when gaps plus rows exceed its height, the scrolling palette when it has fewer than 8 list rows).
The palette keeps the groups in the fixed order above, inside a group the clusters of related actions (#838), and inside a cluster the focused view's own actions first (lone actions come last, whatever their view); a filter keeps matching actions under their groups, the group with the best match first, and groups without a match disappear.
The row menu uses subheaders only when it has more than 5 actions and at least two groups (the graph row menu; the worktree and stash menus are short or single-group and stay flat).
Subheaders use the section-title style and are plain text, so they work in the 16-colour, `NO_COLOR` and ASCII tiers.
The help overlay uses the same groups as subheaders inside its tabs (#652); the which-key popup lists chord continuations and gets none.
Inside a group, actions that share an id prefix form a cluster (#838, `ActionClusters`): the palette, the help and the which-key popup keep a cluster together and put a blank line between clusters; a prefix with one action in the group forms none, and those loose actions follow the clusters. The blank lines are dropped before any row is when the list does not fit. The which-key popup lists the variants it keeps out of the main list (`WhichKeyBuilder.Unlisted`: squash-merge, fixup, reword, drop, interactive from the root) after a `+  +more (variants and shortcuts)` row, so they are shown and keep their keys.

Mouse paths are listed in the next section.

## Keymap rework (design #833; implemented by #839 and #840)

Status: **implemented in #840, owner-approved** (plus `m` and `Ctrl-x` in the conflict editor). The JSON file is the truth and `docs/keymap.md` is generated from it; the tables below keep the Old column as the migration reference. #839 built the levels, the setting and the toggle, and no test reads this section.
Source: the owner's feedback on #833 (too many keys, `<space>b` / `<space>r` confusing, tabs on `Ctrl-w` but repositories on `<space>o`, rare commands mixed with everyday ones).
Counts, read from `DefaultKeymap` and `ActionIds` at this commit: 245 action ids (the issue said 244), 240 bound, 5 without a key (`help.tab-select` is mouse-only, `pick.mainline` modal, `remote.fetch-all`, `settings.open-theme` and `working-tree.open` palette-only), plus the nine `tab.go-to.N` ids (`Alt-1`..`Alt-9`, Everyday, unchanged). The design adds one action, `view.cycle-command-level`, and a key for `detail.retry`. 50 actions change their main key (the rows of the tables below with a key in "New"); the rest keep theirs.

### Layout rules

1. **One noun, one prefix.** Every `<space>` group is one noun and a noun has exactly one group: `b` branch, `t` tag, `w` worktree, `k` stash, `r` remote, `o` repositories and tabs, `g` operation in progress, `e` external tools, `v` view options.
2. **No lowercase/uppercase pair with unrelated meanings.** Gone: `s`/`S` (working tree / stash), `t`/`T` (tools / tags), `r`/`R` (refresh / remotes), `l`/`L` (reflog / pull), `p`/`P` (pin / push), `f`/`F` (jump to ref / fetch), `c`/`C` (commit / cancel), and `b`/`B`, `r`/`R` in the conflict editor. Kept, because the two keys are one action in two strengths or its direct opposite: `bc`/`bC` (create and check out / create only), `bu`/`bU` (set / unset upstream), `bm`/`bM`, `bd`/`bD` (local / remote branch), `rp`/`rP` (push / force push), `td`/`tD`, `a`/`A`, `u`/`U` (stage and unstage, one file / all), `n`/`N`, `o`/`O`, `t`/`T`, `y`/`Y`, `j`/`J`, `k`/`K`.
3. **All branch work under `<space>b`.** Create, switch, rename, delete (local `bd`, remote `bD`), upstream, fast-forward, merge (three kinds), rebase (the sub-group `<space>br`, which also holds fixup, reword and drop), reset, cherry-pick and revert. Tags and worktrees are nouns of their own (rule 1) and keep their own groups, listed next to `b` in the which-key popup.
4. **Tabs and repositories together under `<space>o`** (`oo` open, `or` refresh, `oc` close tab, `on` rename tab, `op` pin, `ol` last tab). `Ctrl-t`, `Ctrl-w c`, `Ctrl-w r`, `Ctrl-^`, `<space><Tab>`, `gt`, `gT` and `Alt-1`..`Alt-9` stay (aliases, or the tab-switching keys).
5. **Aliases on one row.** Help, palette and the tables below show one row per action: main key first, then the other keys (`Tab  also Ctrl-w w`). The main key is the first one in the "New" column. Built by #838 (help); the palette shows the same main-key-first list.
6. **Vim-clash rule unchanged** (#335): Gitunia actions are `<space>` chords or `Ctrl-`/`Alt-` keys. The bare letters of the modal screens (rebase todo, conflict editor, settings) stay: they are keys of a modal list.

### The leader map

| Prefix | Noun | Keys (after `<space>`) |
|---|---|---|
| `b` | Branch | `bc` create and check out, `bC` create only, `bs` switch, `bn` rename, `bd` delete, `bD` delete on a remote, `bu` / `bU` set / unset upstream, `bf` fast-forward, `bm` merge, `bM` merge with a commit, `bq` squash-merge, `bp` cherry-pick, `bv` revert, `bx` reset |
| `br` | Rebase (inside branch) | `brr` onto, `bri` interactive, `brI` interactive from the root, `brf` fixup, `brw` reword, `brd` drop |
| `t` | Tag | `tc` create, `td` delete, `tp` push, `tD` delete on a remote |
| `w` | Worktree | unchanged: `wa` add, `ws` switch, `wt` open in a tab, `wd` remove, `wl` lock, `wu` unlock, `wp` prune |
| `k` | Stash | `ks` push, `ku` push with untracked, `ka` apply, `kp` pop, `kd` drop |
| `r` | Remote | `rf` fetch, `rl` pull, `rp` push, `rP` force push, `rc` cancel, `ra` add, `rd` remove, `rn` rename, `ru` set URL (`rF` reserved for fetch all) |
| `o` | Repositories and tabs | `oo` open a repository (new tab), `or` refresh, `oc` close tab, `on` rename tab, `op` pin, `ol` last tab |
| `g` | Operation in progress | `gc` continue, `ga` abort, `gs` skip, `gr` resolve (open the conflict editor) |
| `e` | External tools | `ed` difftool, `em` mergetool, `ec` commit in `$EDITOR` |
| `v` | View options | unchanged (`vv` menu, `vw`, `vf`, `vi`, `vs`, `vc`, `vl`, `vp`, `vn`, `v1`..`v3`), plus `vb` base in the conflict editor |
| leaves | | `s` working tree, `c` write the message or commit (working tree), `a` `A` `d` `u` `U` stage, discard, unstage (working tree), `1`..`4` working-tree panes, `h` jump to HEAD, `f` jump to a ref, `j` quick jump, `=` compare, `l` reflog, `z` zoom, `x` dismiss, `m` messages, `,` settings, `.` actions menu, `*` toggle advanced commands |

No leaf shares its letter with a group. Letters left free for later: `i` `n` `p` `q` `y`. The stash letter is `k` because `s` stays the working tree (the most used leader key) and `S` would be an unrelated pair; the owner may prefer another free letter.
The which-key popup names the groups after the nouns (`+more (branch)`, `+more (rebase)`, `+more (tag)`, ...); `ActionGroups` stays the vocabulary of the palette, the row menu and the help subheaders.

Old chords that now mean something else (muscle-memory hazards; every destructive one still asks first): `<space>td` (difftool, now delete tag), `<space>gs` (squash-merge, now skip), `<space>gr` (rebase, now resolve), `<space>br` (rename, now the rebase group), `<space>P` (push; push is now `rp`, and `rP` is force push: one Shift slip, but it is a lease push that asks first), `<space>r` and `<space>o` (leaves, now groups that wait for a second key). Chords that are simply gone: `<space>gm` `gM` `gp` `gv` `gx` `gi` `gI` `gf` `gw` `gd` `gk`, `<space>tm`, `<space>te`, `<space>to` (see #742 below), `<space>p`, `<space>F` `L` `C`, `<space>R...`, `<space>S...`, `<space>T...`. In the conflict editor `B` (take both) becomes `m` and `R` (re-create) `Ctrl-x` (both approved by the owner on #840, beyond the table of #847). `detail.retry` moves from `<space>r` to `<space>od` because `<space>r` is now a group and a key cannot be both a complete binding and the start of a longer chord.

### Decisions (approved by the owner on #840)

- **Tags and worktrees stay their own groups** (`t`, `w`), and fetch, pull, push stay under `r`, although the issue's rule "all branch work under `<space>b`" listed `T` and `w` among today's places of branch work. Rule 1 (one noun, one prefix) and the length of everyday chords (`<space>bwa` for adding a worktree) decided it; only deleting a branch on a remote moved into `b` (`bD`). To follow the rule literally, move them to `<space>bt` and `<space>bw` and say so in the PR.
- **Stash is `k`**, and the working tree keeps `s` (see the leader map).
- **Levels** (the Level column) and the judgement notes in it.
- **`<space>*`** as the toggle key.

### Conflicts with other issues

- **#834 (h/l stay in the pane, `Ctrl-h/j/k/l` between panes).** Merged; the rework keeps it. `Ctrl-h` `Ctrl-j` `Ctrl-k` `Ctrl-l` become the main keys of `pane.focus-*` in the tables (the old table listed `Ctrl-w h/j/k/l` first), `Ctrl-w h/j/k/l/w` are aliases on the same row, `Ctrl-Backspace` stays an alias of `Ctrl-h`. Bare `h`/`l` keep their in-pane meaning (fold, expand, the message box cursor); in the modal screens that replace the panes (help, settings, reflog, rebase todo, conflict editor) `h` stays "back" and `l` "next tab" / "activate", as #834 decided. No new binding uses bare `h`/`l`, and `<space>h` / `<space>l` stay leader leaves (HEAD, reflog), so the key sets do not clash. The picker, palette and row menu keep their own `Ctrl-j` / `Ctrl-k` / `Ctrl-l` (their view wins over `Global`). The help tabs of #838 switch with `h` / `l` and `Tab` / `Shift-Tab` as now.
- **#742 (conflict editor entry).** Its open PR #835 binds `<space>to`, which collides with rule 1 (`t` is the tag group). The rework moves `conflict.open` to `<space>gr` ("resolve", next to continue, abort and skip, where a user in a conflict already looks) and keeps `Enter`, `l` and the double-click on the conflicted row. #835 can merge as it is; #840 re-keys the action and sweeps the hint text and tests that name `<space>to` (the hints come from the keymap). #840 waits for #742 for that reason.
- **Settings.** The Key reference stays read-only (owner decision on #833); the new keys appear there from the keymap.
- **Existing `keys` entries keep working.** An entry is `{view, key, action}` and binds a key to an action id, so ids are the contract and none changes (the one new id is `view.cycle-command-level`). A user entry that binds an action to a key of the user's choice is untouched. Two edge cases, both reported by the existing config check, never fatal: an entry that binds a key to a prefix the new defaults now use (a user leaf `<space>r` against the group `<space>r...`) is refused with the message naming both keys; an entry `none` on an old default key that no longer exists reports "nothing to unbind". #840 lists both in the user guide (#842).

### Fast toggle for the command level

`<space>*` (action `view.cycle-command-level`, Global, Normal mode). `*` reads as "show all", needs no letter from the noun groups, and the leader keeps it clear of vim's bare `*` (word search, which Gitunia does not use). Alternatives considered: `<space>A` (taken, stage all), `<space>M` (unrelated pair with `m`), `<space>?` (confusable with the help key).
It cycles Everyday, Advanced, Advanced+ and back to Everyday. It changes the same setting as the Settings row (Settings, Keys; the row shows the key), writes it to `config.json` like any setting, and the status bar confirms (`Command level: Advanced`). It is also in the palette as "Cycle the command level" (Everyday).

### Levels (Everyday, Advanced and Advanced+)

- **Three levels, each includes the ones below.** Everyday shows only Everyday actions; Advanced adds the Advanced ones; Advanced+ shows everything (rare or risky commands such as upstream set/unset, interactive rebase from the root, remote URL, worktree lock/unlock, stash with untracked, reflog). One setting, `Command level`, holds the choice.

- **Hidden, never disabled.** An Advanced or Advanced+ action keeps its key, its palette id and its command (typing its exact id in `:` still runs it); it is only left out of the places that teach keys: the help tabs, the which-key popup, the palette list, the row menu and the status bar hints. A user `keys` entry for a hidden action keeps working.
- **Messages keep their keys.** A message or confirm text that names a key (`<space>vl` load all, the reflog hint after a reset, the retry key) always names it, whatever the level; only the lists that teach keys (help, which-key, palette, row menu, status hints) filter. So an undo hint never points at nothing.
- **Default Everyday.** `Command level` defaults to Everyday (advanced commands off); Settings (Keys) and `<space>*` change it. The owner sets Advanced in their own config.
- **Where the level lives.** One level per action in `ActionCatalog` (metadata like the group; a test fails when an action has none), so every surface reads the same list. A which-key group whose actions are all above the current level is hidden with them (`<space>e` and `<space>br` disappear at Everyday; `<space>t` stays visible because it holds two Everyday actions).
- **Modal screens.** The keys of a screen follow the level of the way into it: the rebase todo keys are Advanced because rebasing is; the conflict editor keys are Everyday except base, re-create, redo, the diff jumps and the side scroll. While a screen is open its footer and help show all its keys, whatever the level, since a screen hiding its own keys is unusable.
- **How the proposal was made.** It starts from the issue (Everyday: move and search, details and diff, tabs and opening repositories, stage/unstage/discard, commit/amend, branch create/switch/delete, merge, fetch/pull/push, stash push/pop, worktree add/switch, copy; Advanced: reflog, reset, interactive and quick rebase, cherry-pick/revert, upstream, fast-forward, remote management, tag push and remote delete, force push, difftool/mergetool/`$EDITOR` commit, squash and no-ff merge, extra view options, status history) and fills the rest with one test: would someone who just wants to commit and push see this daily? Judgement calls have a note. `conflict.open` is plain Everyday (the `g` group is shown even with no operation running; the row only acts in a conflict). Counts of the first proposal: 165 Everyday, 81 Advanced (246 ids with the new action; the nine `tab.go-to.N` are Everyday and not counted).
- **The Level column is the approved list** (owner-edited, #847): 188 Everyday, 47 Advanced, 9 Advanced+ rows. #839 implements it (`ActionLevels`, read through `ActionCatalog.LevelOf`) with these corrections to the ids, since the code is the truth: the rows `conflict-editor.reset`, `next-diff`, `previous-diff`, `scroll-down` and `scroll-up` named actions that do not exist (the Ctrl-d/Ctrl-u scroll and the `]c` jump are the list and diff actions) and are dropped; `conflict-editor.take`, `untake`, `scroll-left` and `scroll-right` (#862) were missing and are added (take and untake Everyday, the side scroll Advanced, per the rule above). The counts of the table are now 191 Everyday, 45 Advanced, 9 Advanced+ (245 ids, plus the nine `tab.go-to.N`).
- **What the setting and the toggle do (#839).** `commandLevel` in `config.json` (`everyday`, `advanced`, `advanced+`; Settings, Keys, row "Command level") and `<space>*` change the level at once, without a restart. The hidden surfaces are the help tabs (the all-commands tabs and the contextual ones; the `Commands` tab ends with "N advanced commands hidden" and the toggle key), the palette list, the which-key popup (a chord group disappears with its last listed action), the row and view-options menus and the status bar hints (a hint whose actions are all above the level is left out). Not filtered: the `Custom` help tab (the user's own bindings), messages and confirm texts, the keys of the screen that is open (see "Modal screens"), and a command typed by its exact id in the palette, which still runs.

### Old key and new key, level per action

Grouped as the palette groups them (`ActionCatalog.GroupOf`). "Old" lists every default key of the action over all views, "New" the main key then its aliases, `same` when the main key does not change. A key written `<space>bc` is the leader followed by `b`, `c`.
**Branch**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `branch.create` | Create a branch and check it out | `<space>bc` | same | Everyday |  |
| `branch.create-only` | Create a branch without checking out | `<space>bC` | same | Advanced |  |
| `branch.delete` | Delete the branch (asks first) | `<space>bd` | same | Everyday |  |
| `branch.fast-forward` | Fast-forward the branch to its upstream (no checkout) | `<space>bf` | same | Advanced |  |
| `branch.rename` | Rename the branch | `<space>br` | `<space>bn` | Everyday | not on the issue's Everyday list |
| `branch.set-upstream` | Set the branch upstream | `<space>bu` | same | Advanced+ |  |
| `branch.switch` | Switch to a branch (check out) | `<space>bs` | same | Everyday |  |
| `branch.unset-upstream` | Unset the branch upstream | `<space>bU` | same | Advanced+ |  |

**Merge and rebase**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `conflict-editor.all-ours` | Conflict editor: take our side for every unresolved hunk of the file | `O` | same | Everyday |  |
| `conflict-editor.all-theirs` | Conflict editor: take their side for every unresolved hunk of the file | `T` | same | Everyday |  |
| `conflict-editor.base` | Conflict editor: take the common ancestor for the hunk | `b` | same | Everyday |  |
| `conflict-editor.both` | Conflict editor: take both sides for the hunk, ours first | `B` | `m` | Everyday | owner decision on #840: `B` (Shift-b next to `b` base) is easy to hit by mistake, `m` is "merge both" |
| `conflict-editor.edit` | Conflict editor: edit the result of the hunk as text | `e` | same | Everyday |  |
| `conflict-editor.edit-done` | Conflict editor: keep the edited text | `Ctrl-g` | same | Everyday |  |
| `conflict-editor.edit-newline` | Conflict editor: insert a new line in the edited text | `Enter` | same | Everyday |  |
| `conflict-editor.next` | Conflict editor: go to the next conflict hunk | `n` | same | Everyday |  |
| `conflict-editor.ours` | Conflict editor: take our side for the hunk | `o` | same | Everyday |  |
| `conflict-editor.previous` | Conflict editor: go to the previous conflict hunk | `N` | same | Everyday |  |
| `conflict-editor.recreate` | Conflict editor: re-create the conflict of the file (asks first) | `R` | `Ctrl-x` | Advanced |  |
| `conflict-editor.redo` | Conflict editor: redo the undone pick or edit | `Ctrl-r` | same | Advanced |  |
| `conflict-editor.save` | Conflict editor: save the file and stage it when resolved | `Ctrl-s` | same | Everyday |  |
| `conflict-editor.scroll-left` | Conflict editor: scroll the base, ours and theirs panes sideways to the left | `zh` | same | Advanced | not in the approved list (#862 came after it) |
| `conflict-editor.scroll-right` | Conflict editor: scroll the base, ours and theirs panes sideways to the right | `zl` | same | Advanced | not in the approved list (#862 came after it) |
| `conflict-editor.take` | Conflict editor: take this pane's version for the conflict (a Visual selection: only its lines) | `<space>a` | same | Everyday | not in the approved list (#862 came after it) |
| `conflict-editor.theirs` | Conflict editor: take their side for the hunk | `t` | same | Everyday |  |
| `conflict-editor.untake` | Conflict editor: untake, make the conflict unresolved again (a Visual selection: remove only its lines) | `<space>u` | same | Everyday | not in the approved list (#862 came after it) |
| `conflict-editor.undo` | Conflict editor: undo the last pick or edit | `u` | same | Everyday |  |
| `merge.branch` | Merge the selection (fast-forward if possible) | `<space>gm` | `<space>bm` | Everyday |  |
| `merge.branch-no-ff` | Merge the selection with a merge commit | `<space>gM` | `<space>bM` | Advanced |  |
| `merge.branch-squash` | Squash-merge the selection (stages changes) | `<space>gs` | `<space>bq` | Advanced |  |
| `operation.abort` | Abort the merge, rebase, cherry-pick or revert in progress (asks first) | `<space>ga` | same | Everyday |  |
| `operation.continue` | Continue the merge, rebase, cherry-pick or revert in progress | `<space>gc` | same | Everyday |  |
| `operation.skip` | Skip the commit a rebase, cherry-pick or revert stopped on (asks first) | `<space>gk` | `<space>gs` | Everyday |  |
| `pick.cherry-pick` | Cherry-pick the selection onto the current branch | `<space>gp` | `<space>bp` | Everyday |  |
| `pick.mainline` | Choose the mainline parent in the open cherry-pick or revert prompt | none | same | Advanced |  |
| `pick.revert` | Revert the selection with new commits | `<space>gv` | `<space>bv` | Advanced |  |
| `rebase-editor.drop` | Rebase todo: remove the commit | `d` | same | Advanced |  |
| `rebase-editor.fixup` | Rebase todo: meld the commit into the one above and keep that one's message | `f` | same | Advanced |  |
| `rebase-editor.message-newline` | Rebase todo: new line in the message | `Enter` | same | Advanced |  |
| `rebase-editor.message-save` | Rebase todo: keep the edited message | `Ctrl-g` | same | Advanced |  |
| `rebase-editor.move-down` | Rebase todo: replay the commit later (move it down) | `J` `Alt-j` | same | Advanced |  |
| `rebase-editor.move-up` | Rebase todo: replay the commit earlier (move it up) | `K` `Alt-k` | same | Advanced |  |
| `rebase-editor.pick` | Rebase todo: keep the commit as it is | `p` | same | Advanced |  |
| `rebase-editor.reword` | Rebase todo: keep the commit with a new message (opens the message editor) | `r` | same | Advanced |  |
| `rebase-editor.squash` | Rebase todo: meld the commit into the one above and keep both messages | `s` | same | Advanced |  |
| `rebase-editor.start` | Rebase todo: start the rebase (asks first) | `Enter` | same | Advanced |  |
| `rebase.drop` | Drop the commit from the branch (asks first) | `<space>gd` | `<space>brd` | Advanced |  |
| `rebase.fixup` | Fix up the commit into the one before it (asks first) | `<space>gf` | `<space>brf` | Advanced |  |
| `rebase.interactive` | Interactive rebase onto the selection (edit the todo) | `<space>gi` | `<space>bri` | Advanced |  |
| `rebase.interactive-root` | Interactive rebase from the root commit (selection ignored) | `<space>gI` | `<space>brI` | Advanced+ |  |
| `rebase.onto` | Rebase the current branch onto the selection | `<space>gr` | `<space>brr` | Everyday | plain rebase is common; the issue list names only interactive and quick rebase as advanced |
| `rebase.reword` | Reword the commit subject (asks first) | `<space>gw` | `<space>brw` | Advanced |  |
| `reset.branch` | Reset the current branch to the selection | `<space>gx` | `<space>bx` | Everyday |  |

**Tag**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `tag.create` | Create a tag at the selected commit | `<space>Tc` | `<space>tc` | Everyday |  |
| `tag.delete` | Delete the tag (asks first) | `<space>Td` | `<space>td` | Everyday |  |
| `tag.delete-remote` | Delete the tag on a remote (asks first) | `<space>TD` | `<space>tD` | Everyday |  |
| `tag.push` | Push the tag to a remote | `<space>Tp` | `<space>tp` | Everyday |  |

**Remote**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `remote.add` | Add a remote (name, then URL) | `<space>Ra` | `<space>ra` | Advanced |  |
| `remote.cancel` | Cancel the running fetch, pull or push | `<space>C` | `<space>rc` | Everyday |  |
| `remote.delete-branch` | Delete a branch on a remote (asks first) | `<space>RD` | `<space>bD` | Everyday |  |
| `remote.fetch` | Fetch the remote of the current branch (prunes) | `<space>F` | `<space>rf` | Everyday |  |
| `remote.fetch-all` | Fetch all remotes (prunes) | none | none (palette) | Advanced | unbound today; `<space>rF` is reserved for it; bound in #840 only if the owner wants a key |
| `remote.pull` | Pull the current branch (fast-forward only) | `<space>L` | `<space>rl` | Everyday |  |
| `remote.push` | Push the current branch | `<space>P` | `<space>rp` | Everyday |  |
| `remote.push-force` | Force push the current branch with a lease (asks first) | `<space>Rf` | `<space>rP` | Everyday |  |
| `remote.remove` | Remove a remote (asks first) | `<space>Rd` | `<space>rd` | Advanced |  |
| `remote.rename` | Rename a remote | `<space>Rn` | `<space>rn` | Advanced |  |
| `remote.set-url` | Change the URL of a remote | `<space>Ru` | `<space>ru` | Advanced+ |  |

**Worktree**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `worktree.add` | Add a worktree (branch, then path) | `<space>wa` | same | Everyday |  |
| `worktree.lock` | Lock the selected worktree (optional reason) | `<space>wl` | same | Advanced+ |  |
| `worktree.open` | Open the selected worktree (or the branch's, made if needed) in its own tab | `<space>wt` | same | Everyday |  |
| `worktree.prune` | Prune worktrees whose directory is gone | `<space>wp` | same | Everyday |  |
| `worktree.remove` | Remove the selected worktree (asks first) | `<space>wd` | same | Everyday | cleanup of what Add created; consider Everyday |
| `worktree.switch` | Switch this tab to the selected worktree | `<space>ws` | same | Everyday |  |
| `worktree.unlock` | Unlock the selected worktree | `<space>wu` | same | Advanced+ |  |

**Stash**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `stash.apply` | Apply the selected stash and keep it | `<space>Sa` | `<space>ka` | Everyday |  |
| `stash.drop` | Drop the selected stash (asks first) | `<space>Sd` | `<space>kd` | Everyday | consider Everyday (cleanup after Apply) |
| `stash.pop` | Apply the selected stash and drop it | `<space>Sp` | `<space>kp` | Everyday |  |
| `stash.push` | Stash the tracked changes (asks for a message) | `<space>Ss` | `<space>ks` | Everyday |  |
| `stash.push-untracked` | Stash the tracked and untracked changes (asks for a message) | `<space>Su` | `<space>ku` | Advanced+ |  |

**Working tree**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `working-tree.collapse` | Close the folder, else go to the parent folder | `h` | same | Everyday |  |
| `working-tree.commit` | Commit the staged changes | `<space>c` `Ctrl-g` | same | Everyday |  |
| `working-tree.discard` | Discard the changes of the file or folder, the hunk or the selected lines (asks first) | `<space>d` | same | Everyday |  |
| `working-tree.edit-message` | Edit the commit message | `i` `Enter` | same | Everyday |  |
| `working-tree.expand` | Open the folder under the cursor | `l` | same | Everyday |  |
| `working-tree.focus-diff` | Focus the diff | `<space>3` | same | Advanced |  |
| `working-tree.focus-message` | Focus the commit message | `<space>4` | same | Advanced |  |
| `working-tree.focus-staged` | Focus the Staged list | `<space>2` | same | Advanced |  |
| `working-tree.focus-unstaged` | Focus the Unstaged list | `<space>1` | same | Advanced | direct jumps; Tab and Ctrl-h/j/k/l cover the same |
| `working-tree.message-append` | Message box: insert after the cursor | `a` | same | Everyday |  |
| `working-tree.message-append-at-end` | Message box: insert at the line end | `A` | same | Everyday |  |
| `working-tree.message-delete-char` | Message box: delete the character under the cursor | `x` | same | Everyday |  |
| `working-tree.message-delete-line` | Message box: delete the line | `dd` | same | Everyday |  |
| `working-tree.message-down` | Message box: cursor down | `j` | same | Everyday |  |
| `working-tree.message-insert-at-start` | Message box: insert at the line start | `I` | same | Everyday |  |
| `working-tree.message-left` | Message box: cursor left | `h` | same | Everyday |  |
| `working-tree.message-line-end` | Message box: line end | `$` | same | Everyday |  |
| `working-tree.message-line-start` | Message box: line start | `0` | same | Everyday |  |
| `working-tree.message-new-line` | Start a new line in the commit message | `Enter` | same | Everyday |  |
| `working-tree.message-open-line` | Message box: open a line below and insert | `o` | same | Everyday |  |
| `working-tree.message-right` | Message box: cursor right | `l` | same | Everyday |  |
| `working-tree.message-undo` | Message box: undo | `u` | same | Everyday |  |
| `working-tree.message-up` | Message box: cursor up | `k` | same | Everyday |  |
| `working-tree.message-word-next` | Message box: next word | `w` | same | Everyday |  |
| `working-tree.message-word-previous` | Message box: previous word | `b` | same | Everyday |  |
| `working-tree.open` | Open the working tree | none | same | Advanced |  |
| `working-tree.open-diff` | Go to the diff of the file or folder | `Enter` | same | Everyday |  |
| `working-tree.stage` | Stage the file or folder, the hunk or the selected lines | `<space>a` | same | Everyday |  |
| `working-tree.stage-all` | Stage all files | `<space>A` | same | Everyday |  |
| `working-tree.toggle` | Open or leave the working tree | `<space>s` | same | Everyday |  |
| `working-tree.toggle-amend` | Commit option: Amend (toggle) | `Alt-a` | same | Everyday |  |
| `working-tree.toggle-folder` | Open or close the folder under the cursor (tree style) | `za` | same | Everyday |  |
| `working-tree.toggle-no-verify` | Commit option: No-Verify (toggle) | `Alt-n` | same | Advanced |  |
| `working-tree.toggle-sign-off` | Commit option: SignOff (toggle) | `Alt-s` | same | Advanced |  |
| `working-tree.unstage` | Unstage the file or folder, the hunk or the selected lines | `<space>u` | same | Everyday |  |
| `working-tree.unstage-all` | Unstage all files | `<space>U` | same | Everyday |  |
| `working-tree.write-message` | Write the commit message | `<space>c` | same | Everyday |  |

**Copy**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `detail.copy-path` | Copy the file path | `y` | same | Everyday |  |
| `diff.copy-file` | Copy the displayed rows of the current file | `Y` | same | Everyday |  |
| `diff.copy-line` | Copy the current diff line | `y` | same | Everyday |  |
| `graph.copy-hash` | Copy the commit hash | `y` | same | Everyday |  |
| `sidebar.copy` | Copy the branch, tag, stash name or worktree path | `y` | same | Everyday |  |
| `visual.copy` | Copy the selected range | `y` | same | Everyday |  |
| `working-tree.copy-path` | Copy the file or folder path | `y` | same | Everyday |  |

**Diff**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `diff.less-context` | Show fewer lines around the changes in the diff | `-` | same | Everyday |  |
| `diff.load-anyway` | Load the whole diff (lift the size limits) | `<space>vl` | same | Advanced |  |
| `diff.merge-parent` | Switch the merge commit's parent | `<space>vp` | same | Advanced |  |
| `diff.more-context` | Show more lines around the changes in the diff | `+` | same | Everyday |  |
| `diff.next-file` | Next file | `]f` | same | Everyday |  |
| `diff.next-hunk` | Next hunk | `]c` | same | Everyday |  |
| `diff.open-view-options` | Open the view options menu of the diff | `<space>vv` | same | Everyday |  |
| `diff.previous-file` | Previous file | `[f` | same | Everyday |  |
| `diff.previous-hunk` | Previous hunk | `[c` | same | Everyday |  |
| `diff.scroll-down` | Scroll the diff down without focusing it | `J` | same | Everyday |  |
| `diff.scroll-half-page-down` | Scroll the diff half a page down without focusing it | `D` | same | Everyday |  |
| `diff.scroll-half-page-up` | Scroll the diff half a page up without focusing it | `U` | same | Everyday |  |
| `diff.scroll-up` | Scroll the diff up without focusing it | `K` | same | Everyday |  |
| `diff.search-next` | Next diff search match | `n` | same | Everyday |  |
| `diff.search-open` | Search in the diff | `/` | same | Everyday |  |
| `diff.search-previous` | Previous diff search match | `N` | same | Everyday |  |
| `diff.toggle-ignore-whitespace` | Toggle ignoring whitespace changes in the diff | `<space>vi` | same | Everyday |  |
| `diff.toggle-side-by-side` | Toggle the side-by-side diff | `<space>vs` | same | Everyday |  |
| `diff.toggle-whitespace` | Toggle whitespace marks in the diff | `<space>vc` | same | Everyday |  |
| `diff.toggle-whole-file` | Toggle showing whole files in the diff | `<space>vf` | same | Everyday |  |
| `diff.toggle-wrap` | Toggle word wrap in the diff | `<space>vw` | same | Everyday |  |
| `files.open-view-options` | Open the view options menu of the file list | `<space>vv` | same | Everyday |  |
| `files.path-full-path` | Show file paths in full | `<space>v2` | same | Everyday |  |
| `files.path-name-first` | Show file paths name first | `<space>v1` | same | Everyday |  |
| `files.path-tree` | Show files as a tree | `<space>v3` | same | Everyday |  |
| `files.toggle-path-style` | Cycle the file paths: name first, full path, tree | `<space>vn` | same | Everyday |  |

**Details**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `detail.collapse` | Close the directory or folder under the cursor, else go to its parent | `h` | same | Everyday |  |
| `detail.expand` | Open the directory or folder under the cursor | `l` | same | Everyday |  |
| `detail.filter` | Filter the file tree by path (Files tab) | `/` | same | Everyday |  |
| `detail.next-tab` | Next bottom pane tab | `]` | same | Everyday |  |
| `detail.open-diff` | Go to the file diff | `Enter` | same | Everyday |  |
| `detail.open-link` | Open the issue link in the message line (a count picks the nth link) | `gx` | same | Everyday |  |
| `detail.previous-tab` | Previous bottom pane tab | `[` | same | Everyday |  |
| `detail.retry` | Retry loading the commit details | `<space>r` | `<space>od` | Advanced |  |
| `detail.tab-changes` | Show the Changes tab | `gc` | same | Everyday |  |
| `detail.tab-details` | Show the Details tab | `gd` | same | Everyday |  |
| `detail.tab-files` | Show the Files tab | `gf` | same | Everyday |  |
| `detail.toggle-folder` | Open or close the folder under the cursor (Changes tab, tree style) | `za` | same | Everyday |  |

**External tools**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `conflict.open` | Resolve the conflicted file under the cursor in the conflict editor | `<space>to` | `<space>gr` | Everyday | also `Enter` / `l` on the row |
| `tool.commit-editor` | Commit the staged changes in $EDITOR | `<space>te` | `<space>ec` | Advanced |  |
| `tool.difftool` | Open the commit in the difftool | `<space>td` | `<space>ed` | Advanced |  |
| `tool.mergetool` | Resolve conflicts in the mergetool | `<space>tm` | `<space>em` | Advanced |  |

**Navigate**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `graph.compare` | Mark or compare a commit | `<space>=` | same | Advanced | new in #535, not on the issue's lists |
| `graph.jump-to-head` | Jump to HEAD | `<space>h` | same | Everyday |  |
| `graph.jump-to-ref` | Jump to a ref or hash | `<space>f` | same | Everyday |  |
| `graph.open-details` | Focus the commit details | `Enter` | same | Everyday |  |
| `list.bottom` | Go to the bottom | `G` | same | Everyday |  |
| `list.down` | Move down | `j` `Down` | same | Everyday |  |
| `list.half-page-down` | Half page down | `Ctrl-d` `PageDown` | same | Everyday |  |
| `list.half-page-up` | Half page up | `Ctrl-u` `PageUp` | same | Everyday |  |
| `list.top` | Go to the top | `gg` | same | Everyday |  |
| `list.up` | Move up | `k` `Up` | same | Everyday |  |
| `navigate.back` | Go back | `h` `Esc` | same | Everyday |  |
| `reflog.open` | Open the reflog of HEAD (of the branch under the sidebar cursor) | `<space>l` | same | Advanced+ |  |
| `reflog.select-commit` | Select the reflog entry's commit in the graph | `Enter` | same | Advanced+ |  |
| `search.next` | Next search match | `n` | same | Everyday |  |
| `search.open` | Search | `/` | same | Everyday |  |
| `search.previous` | Previous search match | `N` | same | Everyday |  |
| `sidebar.activate` | Activate the sidebar entry | `Enter` | same | Everyday |  |
| `sidebar.collapse` | Collapse the sidebar entry | `h` | same | Everyday |  |
| `sidebar.expand` | Expand the sidebar entry | `l` | same | Everyday |  |
| `sidebar.filter` | Filter the sidebar | `/` | same | Everyday |  |
| `view.quick-jump-to-row` | Quick jump to a visible row | `<space>j` | same | Everyday |  |
| `visual.toggle` | Start or end range selection | `v` | same | Everyday |  |

**View**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `pane.focus-down` | Focus the pane below | `Ctrl-w j` `Ctrl-j` | `Ctrl-j` also `Ctrl-w j` | Everyday |  |
| `pane.focus-left` | Focus the pane to the left | `Ctrl-w h` `Ctrl-h` `Ctrl-Backspace` | `Ctrl-h` also `Ctrl-w h`, `Ctrl-Backspace` | Everyday |  |
| `pane.focus-right` | Focus the pane to the right | `Ctrl-w l` `Ctrl-l` | `Ctrl-l` also `Ctrl-w l` | Everyday |  |
| `pane.focus-up` | Focus the pane above | `Ctrl-w k` `Ctrl-k` | `Ctrl-k` also `Ctrl-w k` | Everyday |  |
| `pane.narrower` | Make the focused pane narrower | `Ctrl-w <` `<` | same | Everyday |  |
| `pane.next` | Next pane | `Tab` `Ctrl-w w` | `Tab` also `Ctrl-w w` | Everyday |  |
| `pane.previous` | Previous pane | `Shift-Tab` | same | Everyday |  |
| `pane.reset-sizes` | Reset the pane sizes | `Ctrl-w =` `=` | same | Everyday |  |
| `pane.shorter` | Make the focused pane shorter | `Ctrl-w -` `-` | same | Everyday |  |
| `pane.taller` | Make the focused pane taller | `Ctrl-w +` `+` | same | Everyday  |  |
| `pane.wider` | Make the focused pane wider | `Ctrl-w >` `>` | same | Everyday  |  |
| `pane.zoom` | Zoom the focused pane | `<space>z` | same | Everyday |  |

**Tabs**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `tab.close` | Close the tab | `Ctrl-w c` | `<space>oc` also `Ctrl-w c` | Everyday |  |
| `tab.last-active` | Previously active tab | `Ctrl-^` `<space><Tab>` | `<space>ol` also `Ctrl-^`, `<space><Tab>` | Everyday |  |
| `tab.next` | Next tab | `gt` | same | Everyday |  |
| `tab.previous` | Previous tab | `gT` | same | Everyday |  |
| `tab.rename` | Rename the tab | `Ctrl-w r` | `<space>on` also `Ctrl-w r` | Everyday |  |

**General**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `app.quit` | Quit | `q` `Ctrl-c` | same | Everyday |  |
| `cancel` | Cancel | `Esc` | same | Everyday |  |
| `command.execute` | Run the command | `Ctrl-l` `Enter` | same | Everyday |  |
| `confirm.no` | Answer no | `n` | same | Everyday |  |
| `confirm.yes` | Answer yes | `y` | same | Everyday |  |
| `help.show` | Show keys for this view | `?` | same | Everyday |  |
| `help.tab-next` | Next help tab | `Tab` `l` | same | Everyday |  |
| `help.tab-previous` | Previous help tab | `Shift-Tab` `h` | same | Everyday |  |
| `help.tab-select` | Show the clicked help tab | none | same | Everyday |  |
| `input.accept` | Accept the input | `Enter` | same | Everyday |  |
| `menu.open-for-row` | Open the actions menu of the selected row | `<space>.` | same | Everyday |  |
| `palette.next` | Next palette entry | `Down` `Ctrl-j` `j` | same | Everyday |  |
| `palette.open` | Open the command palette | `:` `Ctrl-p` | same | Everyday |  |
| `palette.previous` | Previous palette entry | `Up` `Ctrl-k` `k` | same | Everyday |  |
| `picker.accept` | Open the repository | `Enter` `Ctrl-l` | same | Everyday |  |
| `picker.clone` | Clone a repository into a new tab | `Alt-c` | same | Everyday |  |
| `picker.complete-path` | Accept the suggestion / complete the path | `Tab` `Right` | same | Everyday |  |
| `picker.init` | Initialise a directory as a repository | `Alt-i` | same | Everyday |  |
| `picker.next` | Next picker entry | `Down` `Ctrl-j` | same | Everyday |  |
| `picker.open` | Open the repository picker | `Ctrl-t` `<space>o` | `<space>oo` also `Ctrl-t` | Everyday |  |
| `picker.pin` | Pin to the top of the picker (toggle) | `Alt-p` | same | Everyday |  |
| `picker.previous` | Previous picker entry | `Up` `Ctrl-k` | same | Everyday |  |
| `picker.remove-recent` | Remove from recents | `Alt-d` | same | Everyday |  |
| `repeat.end` | Leave the repeat mode | `Esc` | same | Advanced |  |
| `repo.pin` | Pin to the top of the picker (toggle) | `<space>p` | `<space>op` | Everyday |  |
| `repo.refresh` | Refresh the repository | `<space>r` | `<space>or` | Everyday | the views refresh by themselves; consider Advanced |
| `settings.activate` | Settings: enter the column, edit the setting or keep the option | `Enter` `l` | same | Everyday |  |
| `settings.add` | Settings: add a folder to the list under the cursor | `a` | same | Everyday |  |
| `settings.decrease` | Settings: lower the number under the cursor | `-` | same | Everyday |  |
| `settings.filter` | Settings: filter the settings by name and help text | `/` | same | Everyday |  |
| `settings.increase` | Settings: raise the number under the cursor | `+` | same | Everyday |  |
| `settings.move-down` | Settings: move the folder down in its list | `J` | same | Everyday |  |
| `settings.move-up` | Settings: move the folder up in its list | `K` | same | Everyday |  |
| `settings.open` | Open the settings | `<space>,` | same | Everyday |  |
| `settings.open-theme` | Open the settings at the theme | none | same | Advanced |  |
| `settings.remove` | Settings: remove the folder under the cursor (asks first) | `d` | same | Everyday |  |
| `settings.toggle` | Settings: flip the toggle under the cursor | `x` | same | Everyday |  |
| `status.dismiss` | Dismiss the status message | `<space>x` | same | Everyday |  |
| `status.history` | Show the status message history | `<space>m` | same | Everyday |  |

**New action**

| Action | What | Old | New | Level | Note |
|---|---|---|---|---|---|
| `view.cycle-command-level` | Cycle the command level | none | `<space>*` | Everyday | built by #839, in the Global view |

The nine `tab.go-to.N` ids (`Alt-1`..`Alt-9`) are Everyday and unchanged.

