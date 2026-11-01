using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Reflog;

/// <summary>What the reflog reducer needs to know about the tabs.</summary>
/// <param name="ActiveTab">The active tab, null when none is open.</param>
/// <param name="ListTab">The tab the open list belongs to, null when the list is closed or that tab is gone.</param>
public sealed record ReflogContext(RepositoryState? ActiveTab, RepositoryState? ListTab);
