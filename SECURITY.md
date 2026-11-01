# Security policy

## Reporting a vulnerability

Do not open a public issue. Use the repository's **Security** tab, choose **Report a vulnerability**, and include the version (`gitunia --version`) and steps to reproduce.

You can expect an acknowledgement within a week. Gitunia is maintained by one person in spare time, so fixes are best effort; a confirmed issue is fixed in a following release and credited if you wish.

## Scope

Gitunia is a local client that runs the `git` CLI and makes no network connections of its own. In scope: command or argument injection through repository content, unsafe handling of hooks or credentials, unexpected data loss from a destructive operation. Out of scope: problems in `git` or your terminal.

## Supported versions

The latest release.
