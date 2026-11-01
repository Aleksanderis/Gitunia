# Security policy

## Reporting a vulnerability

Please do not open a public issue for a security problem.

Report it privately through GitHub: open the repository's **Security** tab, choose **Report a vulnerability**, and describe the problem, the affected version (`gitunia --version`) and steps to reproduce.

You can expect an acknowledgement within a week. Gitunia is maintained by one person in spare time, so fixes are best effort; a confirmed issue is fixed in the next release and credited if you wish.

## Scope

Gitunia is a local terminal client that runs the `git` command-line tool on your repositories. It makes no network connections of its own; fetch, pull and push go through `git` and your configured credential helpers. Examples of in-scope reports: command or argument injection through repository content (branch names, paths, commit messages, config), unsafe handling of hooks or credentials, or unexpected data loss from a destructive operation. Problems in `git` itself or in your terminal are out of scope.

## Supported versions

Only the latest release.
