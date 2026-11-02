using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Blame;

/// <summary>What the blame reducer needs to know about the tabs.</summary>
/// <param name="ActiveTab">The active tab, null when none is open.</param>
/// <param name="ViewTab">The tab the open view belongs to, null when the view is closed or that tab is gone.</param>
public sealed record BlameContext(RepositoryState? ActiveTab, RepositoryState? ViewTab);
