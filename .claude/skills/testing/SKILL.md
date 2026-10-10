---
name: testing
description: Write, run and debug Gitunia tests, and create throwaway Git repos (linear, branchy, worktrees, conflicts, odd paths, large) for tests, parser fixtures and manual checks. Use whenever you add or change code that needs tests, or need a repo to run git commands or the app against. Never experiment on the user's real repos.
---

# Testing

Strategy and layers are in `docs/testing.md` (read it first). This skill is the procedure.

## Pick the layer

| Changing | Write | Notes |
|---|---|---|
| A git output parser | Fixture test: captured real output in, model out | Capture from a fixture repo (below); include odd cases |
| A git operation | Integration test on a temp repo, real `git` | Assert on repo state afterwards, not on the command string |
| Graph layout | Invariant/property tests | Deterministic; incremental result equals full result |
| State, commands, keymap | Pure test, no terminal | Includes the "no binding conflicts" test against `docs/ux.md` |
| Rendering | Snapshot of the frame as text | Several terminal sizes, wide chars, resize |
| Dependencies or serialization | AOT publish + smoke run | No `IL*` warnings |

Tests must fail if the code is wrong; don't write tests that restate the implementation. Destructive operations always get an integration test, including the "user cancels" path.

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

Wait for the frame to settle (poll `capture-pane` until it stops changing) instead of sleeping fixed times. Linux/macOS only; Windows relies on snapshot tests of the frame buffer.

Tips (all implemented in `scripts/measure.sh`, copy from there):
- Use a private server (`tmux -L <name> -f /dev/null`) so the user's tmux and config never interfere; `kill-server` in a trap.
- `set -g remain-on-exit on` keeps the pane after a short-lived binary exits, so its output can still be captured.
- `capture-pane -p -e` keeps escape sequences; use it to detect changes that are only a highlight (selection moves).
- `send-keys -N 200 j` repeats a key; `C-d` is Ctrl-d; send `g g` as two keys.
- Wait for text (`grep -F` on the capture) rather than sleeping.

## Big repo and measurements

- Generate: `scripts/gen-big-repo.sh` (no args: 100k commits into `${GITUNIA_BIG_REPO:-~/.cache/gitunia/big-100000}`; ~5-10 s; idempotent; prints the path). Never inside the project tree.
- Measure any binary the same way: `scripts/measure.sh <binary> <repo>` (contract and metrics: `docs/testing.md`, "Measurement harness").

## Canned shapes

- **linear**: 20 commits on `main`.
- **branchy**: feature branches, merges (including octopus), lightweight and annotated tags, a detached-HEAD variant.
- **worktrees**: `main` plus 3 worktrees (`git worktree add ../wt-a -b a`): one locked, one with its directory deleted (prunable), one dirty.
- **conflicts**: two branches editing the same lines, ready to merge/rebase/cherry-pick into a conflict.
- **odd-paths**: unicode names, spaces, renames, copies, binary files, mode changes.
- **big**: N commits generated with `git fast-import` (100k for performance checks); generated on demand, never committed.

In C# tests use the fluent fixture builder in `tests/` (create it on first need, keep it small) rather than shell scripts. Parser fixtures are captured real output stored as files next to the tests.

## Running

Build, test and benchmark commands live in `CLAUDE.md` once they exist. Report real output, including failures. Benchmarks (BenchmarkDotNet) are not part of the normal run.
