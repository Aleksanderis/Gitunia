# Architecture

Status: the target model below ("Effects as data") is the spec for M7 (#990). Built (#969, #993, #970, #971): `AppState`, `AppCommand`, `AppEffect`, `AppReducer`, the `Dispatcher` and the runner with its type switch, proven by the tag feature (create, delete, push, delete on a remote; `Core/Tags/`, `App/Tags/`), the branches (#972: create, switch, rename, delete, upstream, fast-forward; `Core/Branches/`, `App/Branches/`), remote management (add, remove, rename, set URL; `Core/Remotes/`, `App/Remotes/`, #975), stash (push, apply, pop, drop, file restore; `Core/Stash/`, `App/Stash/`, #978; the stash view keeps its decisions in `Core/Stash/StashViewRules` and its session state in `Gitunia.App`) and by the shell slice (key mode, status messages, confirm dialog, output overlay; `Core/Shell/`) and the prompt model (`Core/Prompts/`, tab rename and the tag prompts). Until each other feature has moved (strangler, see "Migration rule"), the code has two models side by side: the target pipeline and the old one (`Workspace`/`TabCommand` reducers in Core plus controllers in `Gitunia.Tui/App` wired with delegates, `TabActivity`, `TabOperationGate`). Text that describes only the old model says "today".

## Overview

```
Terminal (keys, mouse, resize)
   |  raw input / VT sequences
   v
Gitunia.Tui   views, rendering, input translation      (Terminal.Gui)
   ^  state (read)            | commands
   |                          v
Gitunia.Core  AppState + (AppState, Command) -> (AppState, Effects)   pure
                              | effects
                              v
Gitunia.App   runner + one handler per effect, stores, pollers, watching, persistence
                              | async, cancellable
                              v
Gitunia.Git   spawns `git` ---- repo on disk
              results come back to Core as commands, posted through the dispatcher
```

## The five projects

Each project is a layer. It is defined by what it may know about.

| Project | Job | May depend on | Must not touch |
|---|---|---|---|
| `Gitunia.Platform` | OS and environment code only: config/state directory locations, console capability detection, launching external programs (`$EDITOR`, difftool), byte-exact file deletion. It knows nothing about git or the app. | the OS | git, state, UI |
| `Gitunia.Git` | Runs the `git` CLI and turns its output into typed results: process runner, parsers, readers (commits, refs, status, diff, stash, worktrees), the graph model. | Platform | state, UI |
| `Gitunia.Core` | The app's logic: `AppState`, commands, reducers, effect definitions, keymap, mouse map, config models. Pure `(AppState, Command) -> (AppState, Effects)`; runs and tests without a terminal. | Git (its types) | the console, Terminal.Gui, spawning processes, timers; new file access goes to App (existing file helpers such as `AtomicFileWriter`, `ConfigLoader`, `ConfigWriter`, `SessionStore`, `RepositoryScanner` stay in Core until a migration issue moves them) |
| `Gitunia.App` | The UI-free runtime: effect runner and handlers, stores and loaders for big data, pollers, file watching, session and config persistence, the dispatcher. It does the work Core asks for. | Core, Git, Platform | Terminal.Gui, rendering |
| `Gitunia.Tui` | What you see and touch: Terminal.Gui views, rendering, key and mouse translation, terminal suspension, composition root and `Program.Main` (the AOT binary). | everything | deciding what a command means |

Dependencies run one way: **Tui -> App -> Core -> Git -> Platform** (Tui also uses Core, Git and Platform directly; App uses Platform). The compiler enforces it through project references: `Gitunia.App.csproj` has no Terminal.Gui reference and `Gitunia.Core.csproj` references only Git, so an App handler cannot draw. That is all the compiler enforces: that a reducer starts no process, reads no file and has no timer is kept by review and by the rule that reducers are pure static functions of their arguments. Only `Gitunia.Tui` references Terminal.Gui. `Gitunia.App` exists since #968 and holds the stores and loaders (`Loading/`), `GitServices`, the pollers, `TabRefresher`, `SessionPersistence`, the `Dispatcher`, `EffectRunner<TSession>` with the tag and remote handlers (`Tags/`, `Remotes/`) and the runtime `TabSession` (graph rows, load state, stores, cancellation). `TabSession` has protected hooks for the terminal side; `Gitunia.Tui` derives `TerminalTabSession` from it (the list sources, glyphs and diff views the views read) and supplies the `TerminalTabSessionFactory` the runner builds sessions with. Still in `Gitunia.Tui` (`App/`, moving with the strangler): everything with Terminal.Gui or rendering types.

Platform and Core are not the same thing: Platform is the bottom layer ("how do I do this on this OS?") and knows nothing about Gitunia; Core is the middle layer ("what happens when the user presses `j`?") and knows nothing about the OS.

## Effects as data (target model)

### State

One root `AppState` (Core) holds everything that is logic and not drawing:

- the `Workspace` (ordered repo tabs, active tab, recents and pins; each tab a `RepositoryState`: selection, scroll, status, filters);
- app-wide interaction state, `AppState.Shell` (`ShellState`, #970): the key mode, the confirm dialog (with the command to reduce on `y`), status messages, the output overlay, the open prompt (#971) and the screen size; the open overlays follow with #984 and #985;
- the running operations per tab (operation id, kind, cancellation requested or not).

`AppState` is immutable, changed only by reducers, and small: it is copied on every command, including every cursor key.

### Big data stays outside the state

Graph rows, diffs, file trees, commit details and caches are never fields of `AppState` or `RepositoryState`. They live in stores in `Gitunia.App` (mutable, one per tab and kind, readable from the UI thread while a handler fills them). State holds a small reference to a store: an id (tab, kind) and a version number, plus the cheap view state (selected index, scroll offset, counts). A handler fills a store off the UI thread and posts a result command carrying only the new version and counts; the reducer stores them, and views read the visible rows from the store by index.

What stays in state and what moves (so #978, #979, #981 and #994 decide alike): in state, small data bounded by the number of refs, stashes or worktrees (`Refs`, head, `Worktrees`, `Stashes`, `WorktreeStatuses`, the list of conflicted paths). In stores, anything that grows with commits, files or lines: graph rows, diffs, trees, commit details and the full working-tree status (state keeps its counts, the selected path and a version). A destructive command (discard, stash drop) names its target in the command (path, hunk, stash id) taken from what the user saw, never looked up in a store by the reducer.

This keeps the key path flat: `j` is a reducer call that copies a record of a few dozen fields and never touches a 100 000-row list; rendering reads only the rows it draws. A reducer needs counts and ids, not the data; when it needs a fact about the data (does this hash exist?), the command that asks carries the answer, computed by the handler. Review rule: if a field grows with the repository, it belongs in a store. The bar "startup and key latency unchanged" (`scripts/measure.sh` numbers on `main` before #968, compared in #968, #969 and #988; targets in `docs/internal/performance.md`) is what enforces it.

### Commands and effects

- A **command** is a sealed record in a sealed hierarchy per feature (`BranchCommand`, `StashCommand`, ...), in `Core/<Feature>/`. Features nest into the root through their parent: the root command carries `TabCommand(TabId, RepositoryCommand)` as today, and a feature's hierarchy is one case of it. User intent (`BranchCreateRequested`, `BranchNameAccepted`) and results (`BranchCreateSucceeded`, `BranchCreateFailed`) are both commands; a handler never changes state, it only posts result commands.
- An **effect** is a sealed record too and coarse: one effect is one git-layer operation or one external action (`CreateBranch(Repo, Name, StartPoint)`, `LoadDiff(...)`, `CopyToClipboard(...)`, `ScheduleStatusExpiry(...)`), never a script of several steps. A sequence ("create, switch, refresh") is the reducer reacting to each result command with the next effect, so every step shows in a Core test.
- A **feature reducer** is a pure static function `(FeatureState, FeatureCommand) -> (FeatureState, Effects)` in `Core/<Feature>/`. A feature owns its slice of state and does not read another feature's slice; shared facts (active tab, busy) come from the root.
- A reducer does no I/O and reads no clock or random source; time and ids arrive as part of a command.

### Registration: one line per feature

Adding a feature touches the root types in four places, one line each, so parallel feature PRs rarely conflict on them: one case in the root command hierarchy (the feature's command type), one field for its state slice (with an `Empty` default) in `RepositoryState` or `AppState`, one line in the root reducer's switch that delegates to the feature reducer, and one line in the runner's effect switch that delegates to the feature's handler class. Records, reducer and handler bodies and views live in the feature's own files in `Core/<Feature>/` and `App/<Feature>/`. A root switch case never holds logic. Insert your line in alphabetical order by feature name (not at the end, where two PRs would always collide); when two PRs still conflict on adjacent lines, keep both lines when merging `origin/main`.

### Runner contract (`Gitunia.App`)

- **One handler per effect type**, reached by a type switch over the sealed effect hierarchy (no reflection, no `Type`-keyed registry, so AOT and trimming are safe). The compiler does not check completeness of a hierarchy (C# has no closed hierarchies, a switch over an abstract record warns without a `_` arm), so each switch ends in `_ => throw` and `Gitunia.App.Tests` has a test that finds every concrete effect subtype and checks it has a handler (tests may use reflection; the binary may not). Core has the same test for commands of each reducer. This matters most for destructive effects (reset, discard, delete).
- A handler is an object with its collaborators as constructor parameters (readers, writers, stores, clock). It takes the effect (it carries the repo path and tab id; never the process working directory) and a `CancellationToken`, does the work and posts result commands. It does not read `AppState`.
- **Cancellation is per tab.** The runner keeps a token source per tab; closing a tab cancels its work. Superseded work (a newer selection) cancels the older effect of the same kind and tab. A result posted before the cancel still arrives: result commands carry the operation or request id, and the reducer ignores one that does not match the id in state. Target: the reducer decides which tabs are evicted (an `EvictTab` effect from the recent-use order in state) and the runner only frees the stores. Today the runner keeps the recent-use order and decides, reading `RunningOperations` from state (#993); moving the decision into the reducer is not scheduled. Eviction of a cold tab never cancels a running user operation.
- **Timeouts stay in the git layer**: every `GitRunner` call has its own bound. A handler adds no second one; a timeout arrives as a Git exception and becomes a failure result.
- **Failures become result commands** (`...Failed` with a typed error or message). A handler lets no exception escape except cancellation, which it swallows; the runner has a last-resort catch that posts a generic `EffectFailed` so state never stays "running" forever.
- **The running-operation flag lives in state** (#993). Each tab has `RunningOperations` (`Core/Running/`): a list of `RunningOperation(Id, Kind)`, ids counting up per tab. A result ends the operation of its kind; matching by id is not done yet (one operation per kind per tab, results carry no id), so the id is for the day a feature runs several of one kind. An effect that starts an operation derives from `OperationEffect` (tags: `TagEffect`) and a result command that ends it implements `IOperationResult`; the root reducer, not the feature, marks the kind running when the feature reducer returns such an effect and clears it when the result command has been reduced. A feature reducer reads the flag as a context fact (`TagContext.IsBusy`) and refuses a second operation of its kind, so busy refusals and tab eviction are decided from state and have Core tests. A new feature adds a `RunningOperationKind` value and nothing else. The runner evicts a cold tab only when `RunningOperations.IsBusy` is false for it (and the legacy check below). Cancellation requested joins the record when the first feature has a cancel command; today a cancel arrives as the result command. `TabActivity` and `TabOperationGate` stay until the last feature using them has moved; their users are the Tui action controllers that take a `TabOperationGate` (commit, conflict editor, discard, merge, operations, pick, rebase, remote and push, reset, stash, staging, worktrees; `grep TabOperationGate src/Gitunia.Tui`), and `EffectRunner.IsTabBusy` forwards their answer for eviction.
- **Background work goes through `BackgroundWork`** (#993, `Gitunia.App/Background/`): at most 8 jobs at once (`DefaultLimit`), the rest wait in arrival order and a cancelled job never starts. It carries what nobody is waiting for: the status poll of hidden tabs, the worktree overview poll, commit detail and diff prefetch, issue-link reads. Effects the user caused (the handlers behind the effect switch) and the load of a shown tab (except its issue-link read) start at once and never use it, so they cannot wait behind a burst of background reads. The runner owns the queue (`EffectRunner.Background`) and hands it to the stores it builds and, through the composition root, to the pollers. The active tab's own refresher and fingerprint poll are not queued: they are user-visible refreshes of the shown tab.

### The pilot: tags (#969)

`Core/Tags/` is the reference for a migrated feature. `TagCommand` (user intent and results) goes through `AppReducer` to `TagReducer`, which keeps no state of its own (whether an operation runs is `RepositoryState.Running`, kept by the root reducer; its prompts are the shared prompt model, next sections) and returns `TagEffect`s (`CreateTagEffect`, ...) plus generic effects (`PostStatusEffect`, `RefreshTabEffect`, `ShowConfirmEffect` whose accepted command is part of the effect, `ShowPromptEffect`, `SetKeyModeEffect`, `OfferTerminalRetryEffect`). `App/Tags/TagEffectHandler` runs the git work and posts exactly one result command; the runner's last-resort catch posts the feature's failure command (`TagOperationFailed`, which ends the running mark) or, for an effect without a slice, `EffectFailedCommand`. What only the terminal can do (clipboard, terminal retry) goes through `IShellEffects`, which `TuiApplication` implements until the clipboard moves; the confirm dialog and the key mode are shell state (next section). `Tui/App/Tags/TagActions` only turns the tag keys into commands. Tests: `TagReducerTests` and `AppReducerTests` (Core, synchronous), `TagEffectHandlerTests` (App, real git), `EffectPipelineTests` (command to git and back through the real runner) and the completeness tests that find every concrete effect and command type by reflection.

### Shell state: mode, status, confirm, output (#970)

`Core/Shell/` holds the app-wide interaction state and the commands that change it; `ShellReducer` is pure and reads no clock (a command that needs the time carries it, stamped by `Dispatcher.Clock`).

- **Key mode.** `SetKeyModeCommand` sets `ShellState.Mode` and counts the switch in `ModeChanges`. `KeyController` follows the state (it subscribes to `Dispatcher.Changed`) and drops its pending chord or count on every switch, also to the same mode. A feature reducer still asks with `SetKeyModeEffect`; the runner turns it into the command. Nobody calls a set-mode delegate.
- **Status messages.** `StatusPostedCommand` (with its time) appends to the `StatusLog` in state and returns `ScheduleStatusAgingEffect`; `StatusAgingTimer` (App) waits and posts `StatusAgedCommand`, whose reducer schedules the next step (end of the highlight, expiry) until the message is gone. `StatusReporter` (App, `Post`, `Error`, `Warning`, `Dismiss`, `Current`, `IsFresh`) is the thin class the old controllers keep calling: it stamps the time, dispatches the command and reads the current message back from state. `PostStatusEffect` is the same command for a reducer.
- **Confirm dialog.** `ConfirmRequestedCommand(prompt, onAccepted, refusal)` opens the dialog (mode Confirm, the mode to return to remembered) when it fits the screen; `ConfirmAnsweredCommand` closes it and, for yes only, reduces `onAccepted` right after (`ShellUpdate.Next`, so the accepted command shows in the same Core test). "Screen too small to ask" stays a refusal: nothing opens, the optional `refusal` status is posted and the caller must not start the action. Whether the dialog fits is `ConfirmLayout` (Core), the same arithmetic `ConfirmFrameBuilder` draws with, so reducer and renderer cannot disagree; the terminal layer reports its size with `ScreenResizedCommand` (on start and every resize), which also closes an open dialog that no longer fits, with a warning. `ShowConfirmEffect` (from a reducer) reaches the same command through the runner.
- **Output overlay.** `OutputShownCommand` / `OutputClosedCommand` hold the title, heading and lines; a dialog and a box replace each other and an open overlay.
- **Controllers that have not moved.** They keep their `Action` callback: `ShellDispatch.Confirm(host, prompt, action)` dispatches the request with a `RunActionCommand(action)` as accepted command (the reducer returns `RunActionEffect`, the runner runs it), `SetKeyMode` and `ShowOutput` dispatch their commands. `RunActionCommand` goes away with the last such controller; a moved feature puts its own command in the dialog.
- **Drawing.** `OverlayController` (Tui) only draws: it mirrors the dialog and the box into its `OverlayState` (a render projection with the scroll of the box) and answers keys by dispatching `ConfirmAnsweredCommand` or `OutputClosedCommand`. Who handles a key did not change (that is #987).

Tests: `ShellReducerTests`, `PromptReducerTests` (open, type, filter, move, accept, cancel, return mode), `TabRenameReducerTests`, `ConfirmLayoutTests` and `AppReducerTests` (Core: Esc and `n`, too small, resize, a tab closed while the dialog is open), `StatusReporterTests` (App), `EffectPipelineTests` (command to dialog to git and back), `KeyControllerModeTests` and `OverlayControllerTests` (Tui). Tui tests of controllers use `TestShell` (`tests/Gitunia.Tui.Tests/Fakes`): a real dispatcher whose dialogs, mode, status and output are the real state, plus `Answer()`/`Decline()` and the recorded prompts; it also turns a `ShowPromptEffect` into the prompt command, as the runner does.

### Prompts and pickers (#971)

One prompt model in `ShellState.Prompt` (`Core/Prompts/`) serves every text prompt and type-to-filter picker; `PromptPopup` stays the view model and `PromptPopupFrameBuilder` the only renderer.

- **State.** `OpenPrompt`: title, context, accept label, the typed `Text` (the caret is always at its end, prompts edit at the end), an optional `PromptPicker` (immutable: every `PromptChoice(Value, Text)`, the candidates matching the text best first, the highlighted `Index`; the state version of `FilterPicker<T>`), the `Owner` tab, the `OnAccepted` result and the key mode to return to.
- **Commands** (`PromptReducer`, pure): `PromptRequestedCommand(PromptRequest)` opens it (Insert mode; ignored while another prompt is open, so a late result cannot replace what the user types; replaces a dialog or output box), `PromptKeyPressedCommand(KeyEvent)` edits the text (`PromptTextEditor`), moves the picker highlight (`PickerKeys`: Up/Down/Tab, Ctrl-j/k) and re-filters, `PromptAcceptedCommand` closes the prompt and reduces its result right after (like the confirm dialog's accepted command), `PromptCancelledCommand` closes it. A picker with no highlighted candidate stays open and posts its no-match notice. A `ListOnly` request (the branch delete list) is a picker without a text field: `j/k/h/l` and the arrows move, no letter is filtered. A prompt belongs to its `Owner` tab: closing that tab or making another one active cancels it (`AppReducer`).
- **The result** is a small record implementing `IPromptResult` (`AppCommand Answered(PromptAnswer)`): it holds what the prompt was opened for, taken from what the user saw (the tab, the start point), and returns the feature's own command. No delegate is stored in state.
- **Drawing and keys.** `OpenPromptPopup` turns the state into the `PromptPopup`. `Tui/App/Prompts/PromptInputHandler` is the only Tui piece: it turns Enter, Esc and typed keys into the commands above and hands the popup to the renderer. It sees Enter and Esc before every other handler (`ActionRouter.OpenPrompt`). Prompts that have not moved still use `IPromptHandler` with their own popup; a moved feature deletes its handler.

**Recipe for a feature prompt** (tab rename, `Core/Workspaces/Tabs/TabRename*`, and the tags, `Core/Tags/`, are the examples):

1. The feature's request command (`TabRenameRequested`) reaches its reducer, which returns `ShowPromptEffect(new PromptRequest(title, context, acceptLabel, new XAnswer(...), initialText, choices, noMatch, owner))`. Refusals (busy, nothing selected) are decided before and post a status instead.
2. `XAnswer : IPromptResult` turns `PromptAnswer(Text, Choice)` into the feature's accepted command (`TabRenameAnswered(tab, text)`; for a picker `Choice` is the chosen `PromptChoice.Value`). A multi-step prompt (tag name, then message) is the reducer answering the first accepted command with another `ShowPromptEffect`.
3. The accepted command is reduced like any other: validate (pure), change state or return the effect (`CreateTagEffect`, a status post). The root reducer is the only place that touches the tab.
4. Tui: the key that starts it dispatches the request command (a few lines in an action class such as `WorkspaceActions` or `TagActions`). Nothing else: no popup code, no text editing, no mode switches.
5. Tests: a Core test per step (request gives the right `PromptRequest`, the answer gives the right command, the effect), plus an `AppReducerTests` chain through `PromptRequestedCommand` for tab-closed and tab-left cases. `PromptReducerTests` already covers typing, picking and closing; do not repeat them per feature.

### Threading

`AppState` and every reducer call live on the UI thread only. One dispatcher interface accepts commands from any thread, queues them and runs the reducer on the UI thread, then hands the effects to the runner (implementation in `Gitunia.App` over a UI-thread invoker that Tui supplies). Handlers run off the UI thread and post result commands back through the dispatcher; they never touch state or views. Stores are the only shared mutable data: written by a handler, read by views, published by swapping a complete snapshot (a reader sees a whole version, never a half-filled one).

### Key mode and the input path (#970 and #987)

- **#970 moved state** that lived in controllers and delegates: the key mode is a field of `AppState.Shell`, changed by `SetKeyModeCommand` instead of a `setMode` delegate; the confirm dialog, status messages and output overlay likewise (previous section). Prompt and picker state is #971 (next section), overlay state of reflog, settings and creation is #984 and #985. #970 did not change who handles a key.
- **#987 moves routing**: a key or mouse gesture becomes one command (action id, count, focus) and the reducer picks the handler from state. It is blocked on #970, #971 and the overlay migrations (#984, #985), reads the mode, prompt and overlay fields they created and changes only routing code (`ActionRouter`, `RouteAction`, `HandleTextKey`, overlay click routing).

So #970 never edited routing order, and #987 never adds state for mode, prompts or overlays; it reads `ShellState.Mode`, `Confirm` and `Output`. Until #987, controllers receive keys the old way and only dispatch the mode command.

### Where code goes

| Concern | Project |
|---|---|
| `AppState`, commands, reducers, effect records, keymap, mouse map, prompt and picker models | `Gitunia.Core` (`Core/<Feature>/`) |
| runner, handlers, stores and loaders, pollers, watchers, session and config persistence, `GitServices` | `Gitunia.App` (`App/<Feature>/`) |
| views, rendering and frame builders, key and mouse translation (a key event to a command), terminal suspension, composition root, `Program` | `Gitunia.Tui` |

A new class: if it needs Terminal.Gui or draws, it is Tui; if it spawns git, touches files, or owns a timer or thread, it is App; if it decides what happens, it is Core.

### Tests

- **Feature logic is a synchronous Core test**: build a state, apply a command, assert the new state and effects. No terminal, no `UiPump`, no git, no fakes.
- **Handlers are tested in `Gitunia.App.Tests` against real git** on temp repos (`testing` skill): the effect goes in, the result command comes out, the repo has changed.
- **`Gitunia.Tui.Tests` keeps snapshot tests only** (frames rendered from a given state) and the startup smoke test.
- A feature may add one test that chains reducer, handler and reducer by hand (feeding the result command back) to show the loop closes.

### Worked example: create a branch, from key to refreshed graph

1. Tui: the keymap turns `<space>bn` into the action `branch.create`; input translation dispatches the feature's `BranchCreateRequested` for the active tab.
2. Core, branch reducer: the tab has no running operation, so it opens the name prompt (state change only: prompt open, mode Insert). No effect. Each typed key is a prompt command that changes the prompt text.
3. Enter dispatches `BranchNameAccepted(name)`. The reducer validates the name (pure), closes the prompt, marks the tab's running operation `CreateBranch`, sets the status "Creating branch ..." and returns the effect `CreateBranch(repo, name, startPoint)`.
4. App, runner: the type switch routes the effect to the branch handler, which calls the branch writer off the UI thread with the tab's token and posts `BranchCreateSucceeded(tab, name)`.
5. Core: the reducer clears the running operation, sets "Created branch x" and returns two effects, `LoadRefs(repo)` and `LoadGraph(repo)` (one git-layer operation each). On failure the handler posts `BranchCreateFailed(error)`: the reducer clears the flag, shows the error and returns no refresh.
6. App: the refs handler posts `RefsLoaded(tab, refs)` (refs are small and stay in state, which also feeds the sidebar and name validation); the graph handler fills the tab's graph store and posts `GraphLoaded(tab, version, count, selectedIndex)`, where the handler worked out the new index of the selected hash.
7. Core: the reducer stores the refs, the version, the count and the selected index (it cannot look up the hash itself); Tui redraws the visible rows from the store.

Tests: steps 2, 3 and 5 are synchronous Core tests (including "second create refused while one runs" and "tab closed meanwhile"); step 4 is one `Gitunia.App.Tests` handler test against a temp repo.

### Migration rule

Strangler. Old controllers and the new pipeline coexist until each feature has moved. A feature moves in one PR, completely: state, commands, reducer, effects, handlers and tests, with the old controller and its tests deleted or reduced to view code. No feature is half-moved within a PR (no controller that dispatches half its actions and calls old delegates for the rest). Old controllers reach the new pipeline only by dispatching commands. Order and batches: tracking issue #990. A PR lists every old test case it dropped, with the reason.

### Limits every M7 PR keeps

- No delegate dependencies (`.claude/rules/csharp-style.md`, "Dependencies, not callbacks"), at most five constructor parameters, no `null!` late wiring.
- No file over about 500 lines in `Gitunia.Tui/App` or `Gitunia.App`.
- Behaviour, keys, texts and snapshots unchanged; startup and key latency unchanged (`scripts/measure.sh`).
- Build, tests and the AOT smoke run green.

## Principles

- **Workspace model.** `AppState` holds a `Workspace` (ordered repo tabs, active tab, recent/pinned repos). Each tab owns an independent `RepositoryState` (view state and small data; big loaded data lives in the tab's stores). Commands are routed to a tab by id; effects carry the repo path they run against. Only the 5 most recently used tabs keep a loaded graph (about 350 bytes per commit); colder tabs are evicted and reload on activation (`EffectRunner`, docs/internal/performance.md). The app starts with no repo (the picker shows) and restores the previous session when there is one.
- **Persistence.** Open tabs, recents, pins, per-tab view state and config live in the user config/state dir and are written atomically. A corrupt or missing file never prevents startup.
- **Pure state.** Effects (run a git command, open an editor, copy to clipboard) are returned as data; the runner executes them and feeds the results back as commands ("Effects as data").
- **Async git.** Every git call is async, cancellable and time-bounded; the UI never blocks on one.
- **Shared prompt components.** Prompts are data, not views: `PromptPopup` (Core) is drawn by the one `PromptPopupFrameBuilder`. A moved prompt lives in state as `OpenPrompt` with a `PromptPicker` for the candidates (#971); the prompts that have not moved yet keep theirs in a `FilterPicker<T>` (#766), which goes with the last of them. A new picker is a `PromptRequest` with choices, never a new copy. Behaviour: `docs/internal/ux.md`, "Prompt popups".
- **Shared key hints and chips.** A "key plus what it does" is drawn by one of two builders in `Rendering/Styling`: `HintBar` for a row of hints and `KeyedChip` for a clickable chip (`ChipStyle` picks the roles, the key comes from the keymap through `ChipKeys`). Footers pass explicit `HintItem` lists to `OverlayBox.Hints`; keys are never guessed from text. Prompt footers are a renderer-agnostic `PromptHint` list in Core (`PromptPopup.Hints`) mapped to `HintItem` by `PromptPopupFrameBuilder`. A new hint row or button uses these; rules: `.claude/rules/ui-changes.md`.
- **UI toolkit.** Terminal.Gui v2, wrapped by the rendering layer; Core never sees it. Custom drawing (graph, diff) uses custom views, and modal keys and chords are handled at the application level by our keymap engine. Keep Terminal.Gui types out of Core and keep view models and key events renderer-agnostic (no abstraction interface until a second renderer exists; see `docs/internal/decisions.md`). Settings, pitfalls and measured numbers: `docs/internal/history/architecture.md` (cell widths: `DisplayWidth` follows Terminal.Gui per character, ZWJ sequences differ).

## Solution layout

```
src/
  Gitunia.Platform/   OS-specific code
  Gitunia.Git/        git process runner, parsers, readers, models
  Gitunia.Core/       state, commands, reducers, effect definitions, keymap, config (no console, no process)
  Gitunia.App/        effect runner and handlers, stores, loaders, pollers, watching, persistence (no Terminal.Gui)
  Gitunia.Tui/        views, rendering, input translation, composition root, entry point (AOT publish target)
tests/                one test project per src project, same names plus `.Tests`
tools/AnsiToSvg/      tmux capture of the live UI to SVG for PR screenshots (local only, never in CI)
scripts/              bash and PowerShell helpers: big-repo generator, measurement harness, inspectcode wrapper, screenshots
```

- Folders are domain first, technical kind second (`Models/` for records and enums); namespaces follow folders. Look at the folders themselves for the current domain list.
- Each test project mirrors the folder and file names of the code it tests (`Commits/CommitLogParserTests.cs`). Test types: Git tests and App handler tests run against real temp repos, Core tests are synchronous state/command/keymap tests, Tui tests are snapshot tests of rendered frames. Shared helpers sit in the folder of their domain or in `Fixtures/`.
- Unused usings fail the build (IDE0005, which needs `GenerateDocumentationFile`; the missing-doc, partial-param-doc and broken-cref warnings that brings are silenced in `Directory.Build.props`).
- Build config is in `Directory.Build.props` and `Directory.Packages.props` (central package management); `IL*` (AOT/trim) warnings fail the build through the global `TreatWarningsAsErrors`.

## External tools

`$EDITOR` (via `git commit`), `git difftool` and `git mergetool` need the real terminal, so they never go through `GitRunner` (which redirects every stream).

- `ExternalToolCommands` (Git) builds the git arguments; no env vars are set, the user's config decides editor and tools.
- `IExternalToolLauncher` (`Gitunia.Platform/ExternalTools`) spawns the program without a shell and without redirection (inherited console on Windows, controlling terminal on Linux/macOS; any other OS gets an explicit "not supported" result). `IUrlOpener` (`Gitunia.Platform/Urls`) opens an http or https address in the default browser without waiting (`xdg-open`, `open`, the shell on Windows, "unsupported" elsewhere); the caller copies the address through OSC 52 when it fails. `IByteExactFileSystem` deletes a file or empty directory by raw path bytes for non-UTF-8 names (libc `unlink`/`rmdir` on Linux, "unsupported" elsewhere).
- `ExternalToolRunner` (Tui) orchestrates: suspend, run, pause for Enter on a non-zero exit (git's own message stays readable), resume, report the exit code in the status bar, refresh the tab quietly.
- `ITerminalSuspender` is implemented by `TuiApplication`: it stops the main loop, disposes the Terminal.Gui application (alternate screen, terminal and console modes restored, input thread ended so the tool is the only stdin reader), runs the tool, then creates a fresh `IApplication` (2.5.0 cannot re-initialise one instance) and runs the same window again; a resize while suspended is handled.
- `ITerminalModes` (`Gitunia.Platform/Terminal`) remembers the terminal modes before the UI driver starts (termios on Linux and macOS, console modes on Windows). `TerminalHandOff` puts them back right before the tool starts and after it ends. The capture is the first thing `Run` does, before the light-background query (#743).
- Background producers (watchers, pollers, timers) post through `SuspendableUiInvoker`: while suspended their work is queued and replayed, in order, on the new application. The tool blocks the UI thread by design; background git work continues.
- The credential retry (#45) reuses `ITerminalSuspender` through `ExternalToolRunner.LaunchGit`, which runs any git argument list (`-C <repo>` prefixed) with the real terminal, with no environment set: the user's ssh, askpass and credential helper decide how to prompt.

## Files tab (commit tree)

`CommitTreeStore` (Tui, one per tab, like `CommitDetailsStore`) reads `ls-tree -r -z --long` off the UI thread when the Files tab is shown, with the same debounce, cancels on every selection change and keeps the last four trees. The worker also builds the `FileTreeModel` (Core: nested directories, folded single-child chains, ordinal order), so the UI thread only asks it for the visible rows, which `FileTreeSession` caches until the opened directories or the filter change. A closed directory costs nothing: the rows are produced by walking only the open ones.

A 100 000-file commit loads in 0.6 s with the AOT binary and a worst-case filter keystroke stays near 0.1 s (`docs/internal/performance.md`); lazy per-directory loading is not needed at this size.

## Native AOT

Rules: `.claude/rules/csharp-style.md`. `PublishAot=true` and the trim/AOT analyzers are on from the first commit, `IL*` warnings are errors. Targets: linux-x64 first, then linux-arm64, osx-arm64, win-x64. Release archives for linux-x64, win-x64 and osx-arm64 are built on `v*` tags by `ci.yml` (`scripts/package.sh`, draft release; the Windows leg also attaches the bare `gitunia-<version>-win-x64.exe` plus `.sha256` for a portable winget package, #941; test: `scripts/test-package.sh`); the Linux leg also builds a `.deb` and an `.rpm` from the same binary with nfpm (`scripts/package-linux.sh`, `scripts/nfpm.yaml`, #942). The macOS leg (`macos-arm64`, #455) runs on tags and on `gh workflow run ci.yml -f platform=macos`, never in `all` (10x minutes); it always builds, tests (OS-sensitive tests only) and smoke-runs, since the check job covers Linux and Windows only. A tiny `check` job looks for a green full `workflow_dispatch` run of `ci.yml` on the tagged commit: if there is one, the tag run skips build and tests and only packages; otherwise (or if the lookup fails) the same run builds and tests first, then packages and releases. It never starts or awaits another run (#899, #937). On a tag both legs publish with `-p:Version=<tag without v>`; `Directory.Build.props` defaults plain builds to `0.0.0-dev`, and a target in `Gitunia.Tui.csproj` bakes `$(Version)` into the generated constant `BuildVersion.Value` (no reflection, no startup cost). `gitunia --version` prints `gitunia <version>` and exits before any discovery or UI (the check is in `CommandLineParser`).

## Composition root

`Program.Main` creates the `GitRunner` and calls `GitServices.Create`, which wires every reader and loader by hand and passes the `GitServices` record to the application; the composition root builds everything once into `readonly` fields, with no late wiring (#988). The App and Tui depend on the reader interfaces, each with a fake in `tests/Gitunia.Tui.Tests/Fakes`; tests build the same record (`TestGit.Services`) or swap one part with `with`. Static versus instance: `.claude/rules/csharp-style.md`.

## Cross-platform

- Linux, Windows and macOS are supported targets; macOS is the lowest priority. Build and test on Linux first; CI has a Windows leg and a macOS leg (tags, on demand). Platform-specific code and path rules: `CLAUDE.md`.
- The config file is `config.json` in the config directory (format and rules: `docs/internal/ux-look.md`, "Config file"). `ConfigLoader` and `KeymapConfigurator` (Core) read it and build the keymap; `ConfigStartup` (Tui) runs them at startup. `ConfigJsonContext` is the source-generated JSON context; sections are added as properties of `ConfigDocument`. `ConfigStartup.Load` also reads `theme` (`ThemeConfigurator`) and `tabs.showBranch`, `scanRoots`, `labelCaps` and `hints.whichKey`/`hints.statusBar` (`OptionsConfigurator`, result `ConfigOptions` in `StartupConfig`); `PaletteSettings.WithTheme` forces the light or dark side, or applies a theme file (`ThemeCatalog`, `BuiltInThemes` and `ThemeFileParser` in `Gitunia.Core.Themes`; built-in files are embedded resources in `Themes/Files`), before `Palette.Configure`; `PaletteTable` turns a `ThemeDefinition` into colours.
- `ConfigWriter` (Core) changes one key of `config.json` with a pure edit, an atomic replace and a content-hash stale check; the Settings screen persistence rules are in `docs/internal/ux-settings.md`.
- Config and state locations follow terminal-app convention on every OS, not the OS-native dirs: config in `$XDG_CONFIG_HOME/gitunia` or `~/.config/gitunia`, state in `$XDG_STATE_HOME/gitunia` or `~/.local/state/gitunia`. So the config folder can live in a dotfiles repository, and config stays apart from machine-local state.
- Terminal features are capability-detected (truecolor/256/16, Unicode width, mouse, OSC 52), not assumed.

Design history (why things are the way they are, measurements, rejected options): `docs/internal/history/architecture.md`. Not needed for routine work.
