---
paths:
  - "src/Gitunia.Git/**"
  - "tests/Gitunia.Git.Tests/**"
---

# Git layer rules

The contract, models and refresh design are in `docs/git-layer.md`; read the section you work in. The rules:

- Every git call is repository-scoped: run with `-C <path>`, never rely on the process working directory, no static "current repository".
- Spawn `git` with `ProcessStartInfo` (no shell), arguments as a list. Use per-call `-c` flags (`-c core.quotepath=off`), never mutate user config.
- Environment: only `LC_ALL=C`, `GIT_TERMINAL_PROMPT=0` for background calls and `GIT_OPTIONAL_LOCKS=0` (through `ReadOnly: true`) for read-only polling. Nothing else. `GitRunnerOptions.EnvironmentOverrides` is for test isolation only.
- Every call has a `CancellationToken`, a timeout, captured stderr and an exit code mapped to a typed error. Long operations stream progress.
- Prefer machine formats (`status --porcelain=v2 -z`, `for-each-ref --format`, `log --format -z`, `worktree list --porcelain -z`, `diff --raw -z`, `cat-file --batch`). Batch: one call with many NUL-separated records instead of one process per item.
- One pure parser per command. Parsers never throw on unknown fields (ignore them and surface a warning) and have fixture tests with captured real output, odd cases included (renames, unicode paths, spaces, detached HEAD, unborn branch, operation in progress).
- The Git layer returns typed results and knows nothing of Core or the UI.
- Read-only code never writes (no `index.lock` fights); respect the user's hooks, credential helpers and worktree layout.
- Tests use real `git` on isolated temp repos (`testing` skill); fake the process runner only for timeout, cancellation and error paths.
