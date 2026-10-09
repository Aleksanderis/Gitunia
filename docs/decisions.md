# Decisions

Append-only log of settled choices that cut across docs or would otherwise need digging for. Agents follow these without asking. Open questions live in `roadmap.md`.

Entry format: `date, decision. Why.` When a decision changes, add a new entry that supersedes the old one; don't edit history. Decisions that belong to one area also go into that area's doc.

- 2026-10-10, Language is C# on .NET with Native AOT; Rust is the runner-up, Go is not considered. Why: the owner is a .NET developer and will do heavy agentic development, so the code must stay reviewable and maintainable by them; AOT gives fast startup and a single binary. Rust would be fun but unreviewable in practice. Revisit only if the M0 spike shows the rendering layer is a time sink.
- 2026-10-10, Git access is via the `git` CLI (plumbing/porcelain parsing), not libgit2. Why: matches the user's real config, hooks, credential helpers and new git features such as worktrees; libgit2 bindings lag.  Most established Git clients work this way.
- 2026-10-10, The differentiators are branch and worktree management and graph navigation; vim-style modal, mouseless UX is mandatory, mouse support optional. Why: these are the owner's stated reasons for building it.
- 2026-10-10, Other open-source Git clients are sources of ideas and git command patterns, never of copied code. Why: licence hygiene and a different UI stack.
- 2026-10-10, Gitunia is a standalone multi-repo client (tabs, repo picker, recents, session restore), not a tool bound to the current directory. Why: that is how graphical Git clients work and how the owner wants to use it. Consequence: repo-scoped Git API with explicit paths and per-tab state from the start; retrofitting multi-repo is expensive.
