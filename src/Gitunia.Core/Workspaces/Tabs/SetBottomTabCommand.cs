using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Workspaces.Tabs;

/// <summary>Select a tab of the bottom detail pane.</summary>
/// <param name="Tab">The tab to show.</param>
public sealed record SetBottomTabCommand(BottomTab Tab) : RepositoryCommand;
