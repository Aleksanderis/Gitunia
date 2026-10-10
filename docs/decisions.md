# Decisions

Append-only log of settled choices that cut across docs. Agents follow these without asking. Open questions live in `roadmap.md`. Format: `date, decision. Why.` When a decision changes, add a new entry that supersedes the old one; don't edit history.

- 2026-10-10, C# on .NET with Native AOT (Rust is the runner-up). Why: the owner is a .NET developer doing heavy agentic development, so the code must stay reviewable by them. Revisit only if the M0 spike shows the rendering layer is a time sink.
- 2026-10-10, Git access via the `git` CLI, not libgit2. Why: matches the user's real config, hooks, credentials and new git features such as worktrees.
- 2026-10-10, Standalone multi-repo client (tabs, picker, session restore), not bound to the current directory. Why: retrofitting multi-repo later is expensive, so the Git API is repo-scoped from the start.
- 2026-10-10, Other open-source Git clients are sources of ideas, never of copied code. Why: licence hygiene.
