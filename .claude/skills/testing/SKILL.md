---
name: testing
description: Write, run and debug Gitunia tests, and create throwaway Git repos (linear, branchy, worktrees, conflicts, odd paths, large) for tests, parser fixtures and manual checks. Use whenever you add or change code that needs tests, or need a repo to run git commands or the app against. Never experiment on the user's real repos.
---

# Testing

This skill is the procedure. Strategy, layers and the measurement harness are in `docs/testing.md`; open it only for the section you need (for example the harness contract when running `scripts/measure.sh`), not up front.

## Mark OS-independent tests (#637)

The Windows CI leg runs `--filter "Os!=Independent"`; Linux runs everything. Put `[Trait("Os", "Independent")]` on a test class in `Gitunia.Core.Tests`, `Gitunia.App.Tests` or `Gitunia.Tui.Tests` only when it is pure: in-memory data, fakes, no real `git` (`TestRepository`, `IsolatedGit`, `TestGit`, `GitRunner`), no file system, temp dirs, paths, symlinks, environment, process, terminal or ANSI capability, line endings or encodings. Unmarked is the default and means "runs on Windows", so when unsure leave it unmarked. `Gitunia.Git.Tests` and `Gitunia.Platform.Tests` are never marked. `OsTraitGuardTests` fails when a marked file uses one of those APIs.

## Pick the layer

| Changing | Write | Notes |
|---|---|---|
| A git output parser | Fixture test: captured real output in, model out | Capture from a fixture repo (below); include odd cases |
| A git operation | Integration test on a temp repo, real `git` | Assert on repo state afterwards, not on the command string |
| Graph layout | Invariant/property tests | Deterministic; incremental result equals full result |
| State, commands, keymap | Pure test, no terminal | Includes the "no binding conflicts" and "generated `docs/keymap.md` is current" tests |
| Rendering | Snapshot of the frame as text | Several terminal sizes, wide chars, resize |
| Dependencies or serialization | AOT publish + smoke run | No `IL*` warnings |

Name tests `Given<Context>_When<Action>_Then<Outcome>` (PascalCase parts, e.g. `GivenNoUnicode_WhenRendering_ThenAsciiFrame`). Tests must fail if the code is wrong; don't write tests that restate the implementation. Destructive operations always get an integration test, including the "user cancels" path.

## Waiting and fakes

- No new sleeps or polling loops to wait for background work. Wait on a signal: `UiPump` (queues posted UI callbacks and runs them on the test thread until a condition holds; stores are UI-thread-only, so a test that reads store state while a load is in flight must not post inline from the loader thread), a `TaskCompletionSource` completed from the store's `Changed` event or a file watcher, or an injected delay (e.g. the `delay` parameter of `CommitDetailsStore`, `PickerTimings.ScanningDelay`). A guard timeout only bounds a hang. `RepositoryWatcher.BecameStale` is the signal for a background tab turning stale.
- Fake git scripts (`FakeGitExecutable`) are probe-run once after writing (they exit at once when `GITUNIA_FAKE_PROBE` is set) and retried on "Text file busy": a process forked by a parallel test can hold the fresh file open for writing until its exec. Tests that cancel a running fake wait for `ProcessIdSignal` (the fake renames its `.pid` file into place) instead of a delay.
- Accepted intrinsic timer waits (the wait is the thing under test) are listed in `docs/internal/history/testing.md`; add no new ones without saying why in the PR.

## Isolated repos

- Create repos under the test temp dir or the scratchpad, never in the project tree or the user's repos.
- Deterministic and isolated:
  ```sh
  export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
  export GIT_AUTHOR_NAME=Test GIT_AUTHOR_EMAIL=test@example.com
  export GIT_COMMITTER_NAME=Test GIT_COMMITTER_EMAIL=test@example.com
  export GIT_AUTHOR_DATE="2026-01-01T00:00:00Z" GIT_COMMITTER_DATE="$GIT_AUTHOR_DATE"
  git init -q -b main repo
  ```
  Advance the dates per commit so hashes are stable and ordering is meaningful.
- Never `push`/`fetch` against real remotes. For remote scenarios use a local bare repo (`git init --bare`) as `origin`.

- Cross-platform: set git isolation through the process environment in code (not shell scripts), use `Path` APIs, and never assume `/dev/null` or `/tmp`; the Linux-only shell snippets here are for manual use.

## Driving the real binary (tmux)

The agent can't see a terminal, so run the app in a detached tmux session and assert on its text:

```sh
tmux new-session -d -s gt -x 120 -y 40 'path/to/gitunia <repo>'
tmux send-keys -t gt j j Enter
tmux capture-pane -t gt -p        # rendered frame as text
tmux kill-session -t gt
```

Wait for the frame to settle (poll `capture-pane` until it stops changing) instead of sleeping fixed times. tmux has no native Windows build; on Windows use **psmux** (`winget install marlocarlo.psmux`, free; installs `psmux`, `pmux` and a `tmux` alias, all the same binary, ConPTY underneath). Evaluated in #65 against the real `gitunia.exe`: `new-session -d -s gt -x 120 -y 40 '<exe> <repo>'`, `send-keys` (`j`, `Tab`, `Enter`, `Escape`, `C-d`, `-l` for literal text such as `:q`, `-N 3`), `capture-pane -p` (UTF-8 box drawing intact) and `-e` (SGR colours), `resize-window -x -y`, `-L <name>` private server and `set-option -t <session> remain-on-exit on` all work with the tmux syntax above. Differences: run from Git Bash or PowerShell (not cmd quoting); `set-option -g` fails with "no active session" before a session exists, so set it with `-t <session>` after creation (a binary that exits within milliseconds can race it); the server state lives in `%USERPROFILE%\.psmux`, so use `-L` for isolation; set `XDG_STATE_HOME` and `XDG_CONFIG_HOME` to a temp dir so the real session is not restored (honoured on every OS, `docs/architecture.md`); `wait-for` prints nothing, so poll `capture-pane` for settle (~2.6 s to first stable frame of a non-AOT Release build, polls cost ~50 ms). SGR mouse bytes sent with `send-keys -l` are accepted but their effect was not verified; check mouse in Windows Terminal. `scripts/measure.ps1` and `scripts/measure-tabs.ps1` are the psmux ports of `measure.sh` and `measure-tabs.sh`; `scripts/screenshot.ps1` is the psmux port of `screenshot.sh`. psmux drops the second key of a chord sent in one call (`send-keys g g`), so send `g` twice in separate calls. The old `tools/ConPtyHarness` (pass/fail checks on raw VT output, no frame text) was removed and is not the Windows path.

Tips (all implemented in `scripts/measure.sh`, copy from there):
- Use a private server (`tmux -L <name> -f /dev/null`) so the user's tmux and config never interfere; `kill-server` in a trap.
- `set -g remain-on-exit on` keeps the pane after a short-lived binary exits, so its output can still be captured.
- `capture-pane -p -e` keeps escape sequences; use it to detect changes that are only a highlight (selection moves).
- `send-keys -N 200 j` repeats a key; `C-d` is Ctrl-d; send `g g` as two keys.
- Wait for text (`grep -F` on the capture) rather than sleeping.

## Big repo and measurements

- Generate: `scripts/gen-big-repo.sh` (no args: 100k commits into `${GITUNIA_BIG_REPO:-~/.cache/gitunia/big-100000}`; ~5-10 s; idempotent; prints the path); `scripts/gen-big-file-repo.sh` makes a 50k-line file with 500 edits for blame. Never inside the project tree.
- Measure any binary the same way: `scripts/measure.sh <binary> <repo>` (contract and metrics: `docs/testing.md`, "Measurement harness").

## Canned shapes

- **linear**: 20 commits on `main`.
- **branchy**: feature branches, merges (including octopus), lightweight and annotated tags, a detached-HEAD variant.
- **worktrees**: `main` plus 3 worktrees (`git worktree add ../wt-a -b a`): one locked, one with its directory deleted (prunable), one dirty.
- **conflicts**: two branches editing the same lines, ready to merge/rebase/cherry-pick into a conflict.
- **odd-paths**: unicode names, spaces, renames, copies, binary files, mode changes.
- **big**: N commits generated with `git fast-import` (100k for performance checks); generated on demand, never committed.

Ready in `tests/Gitunia.Git.Tests/Fixtures/TestRepositories.cs`: `Linear`, `WithMergedFeatureBranch`, `WithWorktree`, `WithManyWorktrees` (locked, prunable, dirty), `Conflicted` (merge), `RebaseConflicted`, `CherryPickConflicted`, `RevertConflicted`, `WithOrigin` (local bare origin, tracking main), `WithTrackingBranch(ahead, behind)`, `WithoutUpstreamBranch`, `WithStash` (tracked + untracked), `OddPaths`, `TestRepository.CreateReftable()` (reftable ref storage, git 2.45+; guard tests with `Assert.SkipUnless(TestRepository.IsReftableSupported, ...)`). `TestRepository.GitIn(dir, ...)` runs isolated git in another worktree or clone. Each shape is built once per process and returned as a directory copy (`TestRepository.FromTemplate`, #1388: a git process is 0.1-0.3 s on macOS CI), so prefer a shape (or `FromTemplate` for a new one) over `Create().Commit(...)` chains when several tests need the same repository.

In C# tests use the fluent fixture builder in `tests/` (create it on first need, keep it small) rather than shell scripts. Parser fixtures are captured real output stored as files next to the tests.

## Running

Report real output, including failures. Benchmarks (BenchmarkDotNet) are not part of the normal run.
