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
