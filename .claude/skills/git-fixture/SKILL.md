---
name: git-fixture
description: Create throwaway Git repositories (linear, branchy, many worktrees, conflicts, large generated) for Gitunia tests, parser fixtures and manual checks. Use whenever you need a repo to run git commands or the app against; never experiment on the user's real repos.
---

# Git fixtures

## Rules

- Create repos under the scratchpad directory (or a test temp dir), never in the project tree or the user's other repos.
- Make them deterministic and isolated:
  ```sh
  export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
  export GIT_AUTHOR_NAME=Test GIT_AUTHOR_EMAIL=test@example.com
  export GIT_COMMITTER_NAME=Test GIT_COMMITTER_EMAIL=test@example.com
  export GIT_AUTHOR_DATE="2026-01-01T00:00:00Z" GIT_COMMITTER_DATE="$GIT_AUTHOR_DATE"
  git init -q -b main repo
  ```
  Advance the dates per commit so hashes are stable but ordering is meaningful.
- Never run `git push`/`fetch` against real remotes. For remote scenarios create a local bare repo (`git init --bare`) and use it as `origin`.

## Canned shapes

- **linear**: 20 commits on `main`.
- **branchy**: `main` plus feature branches, merges (including octopus), a tag, an annotated tag, a detached HEAD variant.
- **worktrees**: `main` plus 3 worktrees (`git worktree add ../wt-a -b a`), one locked, one with a deleted directory (prunable), one dirty.
- **conflicts**: two branches editing the same lines, ready to merge/rebase/cherry-pick into a conflict.
- **odd-paths**: unicode names, spaces, renames, copies, binary files, file mode changes.
- **big**: N commits generated with `git fast-import` (100k for performance checks); never committed to the repo.

In C# tests, use the fluent fixture builder in `tests/` (create it on first need, keep it small) instead of shell scripts, and capture real output into fixture files for parser tests.
