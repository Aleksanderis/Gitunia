# Contributing to Gitunia

Thanks for your interest. Gitunia is a small project with a fixed goal and scope, so a few ground rules keep things smooth.

## Issues first

- Use GitHub Issues with the templates (bug report, feature request). Blank issues are disabled.
- Before reporting, check the [user guide](docs/user-guide.md) and the [README](README.md), whose "MVP scope" section lists what is in and what is deliberately out until after the MVP.
- One problem per issue. For a bug, include `gitunia --version`, your OS and terminal, and the steps to reproduce.

## Pull requests

Pull requests are welcome after discussion: open an issue first and wait for a reply that the change fits the scope. Unsolicited PRs, especially large ones or ones that add features outside the MVP scope, may be closed without review.

If you do send one:

- Branch from `main`, one issue per PR, and reference it (`Closes #<n>`).
- Commits follow Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`).
- `dotnet build` (analyzers on, warnings are errors) and `dotnet test` must pass on your machine. CI is not run automatically on pull requests, so the maintainer triggers it.
- Read `docs/architecture.md` and `docs/testing.md` first: the Git layer, state layer and rendering layer are strictly separated, git is always run as a child process with an explicit repository path, and the binary must stay Native AOT compatible.
- Linux, Windows and macOS must all keep working; do not add platform-specific code outside the platform abstraction.

## How this repository is built

Gitunia is developed with AI coding agents under the maintainer's direction. That is why the repository contains files you may not expect:

- `CLAUDE.md` and `.claude/` are the standing instructions, rules and skills for those agents. They double as a compact description of the project conventions.
- `docs/` holds the design documents; `docs/decisions.md` is an append-only log of design decisions and `docs/history/` archives moved or superseded doc text, decisions included.
- Issues and pull requests carry labels such as `agent-created` (opened by an agent) and `owner` (the maintainer wrote or commented). Commits made by an agent say so in the author name and an `Agent:` trailer.

Every change is reviewed and merged by the maintainer, whoever (or whatever) wrote it. You do not need to use or understand the agent setup to contribute.

## Security

Do not report vulnerabilities in a public issue. See [SECURITY.md](SECURITY.md).

## License

By contributing you agree that your contribution is licensed under the [MIT License](LICENSE).
