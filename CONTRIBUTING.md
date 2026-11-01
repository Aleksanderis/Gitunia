# Contributing to Gitunia

## Reporting issues

- Use the GitHub issue templates (bug report, feature request).
- Check the [user guide](docs/user-guide.md) and the Features and "Not included" lists in the [README](README.md) first.
- One problem per issue. For a bug, include `gitunia --version`, your OS and terminal, and the steps to reproduce.
- Report security problems privately: see [SECURITY.md](SECURITY.md).

## Pull requests

Open an issue first and wait for a reply that the change fits. Unsolicited large PRs or ones adding features outside the README lists may be closed.

- Branch from `main`, one issue per PR, reference it (`Closes #<n>`).
- Use Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`).
- `dotnet build` and `dotnet test` must pass. Warnings are errors.
- Add tests for behaviour you change or add. Git code is tested against real temporary repos, not mocks. See `docs/testing.md`.
- Code style is enforced by `.editorconfig` during the build, so there is nothing separate to run.
- Keep the PR focused and describe what changed and why. Expect review comments. The maintainer merges.
- Keep Linux, Windows and macOS working, and the binary Native AOT compatible. See `docs/architecture.md` and `docs/testing.md`.

## Development setup

```
dotnet build
dotnet test
```

The .NET SDK version is pinned in `global.json`. Source is in `src/` (`Gitunia.Core`, `Gitunia.Git`, `Gitunia.Platform`, `Gitunia.Tui`) and tests are in `tests/`. Layers are described in `docs/architecture.md`. A Native AOT publish needs a C compiler. See [the user guide](docs/user-guide.md#install).

## Agent-assisted development

Gitunia is developed with AI coding agents under the maintainer's direction. [CLAUDE.md](CLAUDE.md) and `.claude/` hold their instructions. Every change is reviewed and merged by the maintainer. You do not need the agent setup to contribute.

## License

By contributing you agree that your work is licensed under the [MIT License](LICENSE).
