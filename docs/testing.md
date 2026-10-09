# Testing

Status: draft v0, 2026-10-10.

## Principles

- Test where the risk is: parsing git output, graph layout, destructive operations, worktree handling, key handling, and rendering regressions. Plain wiring gets no tests.
- Real `git` against real temporary repos for the Git layer. No mocking the process runner except for timeout/cancellation/error-path tests.
- State and keymap logic is pure and tested without a terminal.
- No test touches the user's repos, global git config or network. Fixtures set `GIT_CONFIG_GLOBAL=/dev/null`, `GIT_CONFIG_SYSTEM=/dev/null` and a fixed author/committer and dates.
- A failing test blocks merge. No coverage percentage gate.

## Layers

| Layer | Tools | Covers |
|---|---|---|
| Git parsers | xUnit, captured output fixtures | Every parser incl. odd cases (renames, unicode, detached, unborn, in-progress ops) |
| Git integration | xUnit, temp repos built by a fixture builder | Commands end to end: branches, worktrees, stash, merge/rebase, partial staging |
| Graph layout | xUnit, property tests | Lane assignment invariants, determinism, incremental == full |
| State/keymap | xUnit | Command -> state transitions, modal behaviour, no binding conflicts (test reads the keymap table) |
| Rendering | snapshot tests of frames as text | Layout at several terminal sizes, wide chars, resize |
| AOT smoke | CI script | AOT publish succeeds with no `IL*` warnings; binary starts and renders a frame against a fixture repo |
| Manual | checklist | Real terminals (kitty, alacritty, Windows Terminal, tmux), credential prompts, huge repos |

## Fixtures

Use the `git-fixture` skill's builder pattern: a small fluent helper that creates a temp repo with a scripted history (branches, merges, worktrees, conflicts) and deterministic hashes. Keep a few canned repos (linear, branchy, many-worktrees, big generated one for performance).

## Performance checks

A benchmark (BenchmarkDotNet, not part of the normal test run) for graph load and layout on a generated 100k-commit repo. Record numbers in `docs/architecture.md` when targets are verified.
