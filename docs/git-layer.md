# Git layer

Status: draft, 2026-10-10. Contract between Gitunia.Git and everything above it.

## Running git

- **Decided:** repo-scoped API. Every operation is invoked through a `Repo` handle bound to an explicit path; git is run with that path as working directory (`-C`), never relying on the app's cwd. Repo discovery (`rev-parse --show-toplevel/--git-common-dir`) validates paths the user opens, including bare repos, worktree paths (resolving to their main repo) and non-repos (clear error).
- **Decided:** spawn `git` with `ProcessStartInfo` (no shell), arguments as a list, `-c core.quotepath=off` and similar per-call `-c` flags rather than mutating user config.
- Environment: `LC_ALL=C` for parsed output, `GIT_TERMINAL_PROMPT=0` for background calls, `GIT_OPTIONAL_LOCKS=0` for read-only status polling (avoids fighting the user's other git processes over `index.lock`). Anything else needs a decision.
- Prefer machine formats: `status --porcelain=v2 -z`, `for-each-ref --format=...`, `log --format=... -z`, `worktree list --porcelain -z`, `rev-list --parents`, `diff --raw -z`, `cat-file --batch` for bulk object reads.
- Every call: `CancellationToken`, timeout, captured stderr, exit code mapped to a typed error. Long operations (fetch, pull, push, clone) stream progress (`--progress`) to the caller.
- Credentials and hooks run in the user's real environment. Interactive prompts (ssh passphrase, credential helper UI) need a defined story: see Open questions.

## Parsers

- One parser per command, pure functions over `ReadOnlySpan<byte>`/string, with fixture-based tests (captured real output including odd cases: renames, copies, unicode paths, spaces, detached HEAD, unborn branch, merge in progress).
- Parsers never throw on unknown fields; they ignore them and surface a warning.

## Models

- Repo: path, git dir, common dir, HEAD state (branch, detached, unborn), operation in progress (merge, rebase, cherry-pick, revert, bisect).
- Worktree: path, HEAD, branch, locked, prunable, bare, per-worktree dirty summary. Worktrees are first-class: every view that shows a branch also shows which worktree has it checked out.
- Branch/ref: local, remote, tag, upstream, ahead/behind, last commit, checked-out-in (worktree).
- Commit graph: hash, parents, author, date, subject, refs; lane assignment computed incrementally from `rev-list --parents` order. Graph layout is a pure function so it can be tested and cached.
- Status: staged/unstaged/untracked/conflicted per path, with hunk/line addressing for partial staging (`git apply --cached` with generated patches).

## Refresh

- One watcher set per open tab, debounced; background tabs mark themselves stale and refresh on activation. Cap total watchers and fall back to polling.
- Watch `.git` (and each worktree's gitdir) with `FileSystemWatcher`, debounced, plus manual refresh. Fall back to polling where watching is unreliable.
- A refresh recomputes only what changed (refs, status, worktrees) and never resets scroll or selection.

## Open questions

- Interactive credential/ssh prompts: suspend the TUI and give git the terminal, or run with a pty and render prompts inline? Recommendation: suspend TUI for fetch/pull/push when git needs input, resume after.
- Large-repo graph layout strategy (incremental lanes vs precomputed via `commit-graph`): decide with spike data.
