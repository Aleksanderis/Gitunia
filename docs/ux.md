# UX

Spec for workflows and keys; the flows are written before the views. Keyboard and mouse are both fully supported: every action is reachable with either.

## Principles

- Modal and discoverable: Normal mode navigates, actions are short mnemonic chords, `?` shows keys for the current view, `:` or `Ctrl-p` opens a command palette that lists every action with its binding.
- Key bindings are the defaults, and users can remap them or add extra bindings in the config file (planned for M4; the keymap engine is data-driven from M1 so remapping needs no redesign). Vim-like motions everywhere: `h j k l`, `gg`/`G`, `Ctrl-d`/`Ctrl-u`, `/` search, `n`/`N`, counts (`5j`). Arrow keys and PageUp/Down also work.
- History layout: left sidebar (branches, remotes, tags, stashes, worktrees), center commit graph, right/bottom detail (commit info, files, diff). Working tree status is a pseudo-row at the top of the graph.
- Working-tree layout (staging): selecting the status row (or a key from anywhere) switches the center and detail panes to unstaged files, staged files and a diff/hunk view. Whole files, hunks or selected lines are staged and unstaged from the diff itself, then the commit message is written and committed from the same screen. Details of this flow are specified as workflow 2 below, before M2.
- Branches and worktrees are first-class, not a submenu: they are always visible in the sidebar and every branch shows which worktree holds it.
- Destructive actions confirm and say what they will do and how to undo it.
- Mouse is optional but complete: everything doable by keyboard is doable with the mouse (click to focus/select, double-click to open, wheel to scroll, clickable tabs, buttons and menus for actions, drag to select ranges and resize panes). Nothing is keyboard-only or mouse-only; each action's mouse path is listed next to its binding.

## Workspace and tabs

- The app opens with or without a repo: `gitunia` (restore last session, or repo picker if none), `gitunia <path>` (open that repo as a tab, also from outside any repo; a subdirectory resolves to its repo root).
- Tab bar shows repo name (path on disambiguation), branch and a dirty/ahead/behind marker, and background-activity state.
- Repo picker: fuzzy search over recent, pinned and discovered repos (configurable scan roots), open by typing a path, close/pin entries. Opening an already-open repo focuses its tab.
- Each tab keeps its own selection, scroll, filters and focused pane.

## Modes

Normal (navigate, act), Visual (select a range of lines/commits), Insert (text input: commit message, filters), Command (`:` palette), Confirm (y/n dialogs).

## Core workflows to design first

Each needs a written step-by-step key flow here before it is implemented.

0. Open a repo from anywhere, switch between tabs, reopen the previous session.
1. Browse the graph, jump to a ref/commit, search by message/author/hash.
2. Working-tree layout: review unstaged changes, stage whole files, hunks or selected lines in the built-in diff view, unstage, write the message, commit, amend.
3. Create/switch/rename/delete branch; set upstream; see ahead/behind.
4. Worktrees: add (new branch or existing), switch the app's active worktree, remove, prune, lock; see dirty state across all worktrees at a glance.
5. Merge, rebase onto, cherry-pick, revert from the graph.
6. Stash push/apply/pop/drop.
7. Fetch/pull/push with visible progress and cancel.

## Keymap

The single source of truth for bindings. Add a row in the same change that adds a binding; no conflicts within a mode/view.

| Mode/View | Key | Action |
|---|---|---|
| Global | `?` | Show keys for current view |
| Global | `:` / `Ctrl-p` | Command palette |
| Global | `q` | Back / quit from root view |
| Global | `Tab` / `Shift-Tab` | Next / previous pane |
| Lists | `j` `k` / `gg` `G` | Move / top / bottom |
| Lists | `/` `n` `N` | Search, next, previous |
| Global | `gt` / `gT` | Next / previous tab (counts: `3gt` goes to tab 3) |
| Global | `Alt-1`..`Alt-9` | Jump to tab N |
| Global | `Ctrl-t` / `<space>o` | Open repo picker in a new tab |
| Global | `Ctrl-w c` / `:tabclose` | Close tab |
| Global | `<space>p` | Pin/unpin repo |

(More rows come with the workflows above.)

