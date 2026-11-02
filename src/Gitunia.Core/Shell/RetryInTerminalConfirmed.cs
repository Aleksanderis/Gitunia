using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Shell;

/// <summary>
/// The user accepted the credential terminal retry dialog (#512): run the same git command again on the real
/// terminal. The dialog carries it as its accepted command.
/// </summary>
/// <param name="TabId">The tab the command belongs to.</param>
/// <param name="Noun">What failed (such as <c>Tag push</c>), for the status bar.</param>
/// <param name="Arguments">The git arguments of the same command.</param>
public sealed record RetryInTerminalConfirmed(TabId TabId, string Noun, IReadOnlyList<string> Arguments) : ShellCommand;
