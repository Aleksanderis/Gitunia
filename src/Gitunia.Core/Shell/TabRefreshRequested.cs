using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Shell;

/// <summary>A tab is to be read again (the refresh key, the end of an external tool).</summary>
/// <param name="TabId">The tab to refresh.</param>
/// <param name="Announce">True for a refresh the user asked for.</param>
public sealed record TabRefreshRequested(TabId TabId, bool Announce) : ShellCommand;
