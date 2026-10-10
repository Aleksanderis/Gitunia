# Decisions

Log of settled choices that cut across docs. Agents follow these without asking. Open questions live in `roadmap.md`. Format: `date, decision. Why.` The entries dated 2026-10-10 are the baseline and may be reworded in place. Once the M0 decision (#7) is closed the log is append-only: when a decision changes, add a new entry that supersedes the old one and don't edit history.

- 2026-10-10, C# on .NET with Native AOT (Rust is the runner-up). Why: the owner is a .NET developer doing heavy agentic development, so the code must stay reviewable by them. Revisit only if the M0 spike shows the rendering layer is a time sink.
- 2026-10-10, Git access via the `git` CLI, not libgit2. Why: matches the user's real config, hooks, credentials and new git features such as worktrees.
- 2026-10-10, Standalone multi-repo client (tabs, picker, session restore), not bound to the current directory. Why: retrofitting multi-repo later is expensive, so the Git API is repo-scoped from the start.
- 2026-10-10, Other open-source Git clients are sources of ideas, never of copied code. Why: licence hygiene.
- 2026-10-10, Spikes live on unmerged `spike/` branches and close with findings. Issues labeled `destructive` or `needs-review` are merged by the owner; agents continue with other unblocked issues meanwhile.
- 2026-10-10, Cross-platform from day one (Linux first; Windows and macOS later, macOS lowest priority and droppable). Platform-specific code only behind a platform abstraction.
- 2026-10-10, Minimum git 2.43, `net10.0`, JSON config with source generation, MIT licence, product/binary name `gitunia`, config under the platform config dir `gitunia`. Why: LTS on current distros; AOT-safe; defaults accepted by the owner.
- 2026-10-10, External tools: hand the terminal to `$EDITOR`/difftool/mergetool by suspending and resuming the TUI; clipboard via OSC 52. No built-in editor.
- 2026-10-10, PRs are merged with a merge commit (never squash or rebase) and the branch is deleted afterwards. Why: the owner wants the full branch history and topology visible in the graph.
