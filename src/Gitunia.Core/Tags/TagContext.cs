using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Tags;

/// <summary>The facts of the root the tag reducer reads: the tab, its refs and whether it is the active tab.</summary>
/// <param name="TabId">The tab the command is for.</param>
/// <param name="Location">Where the tab runs its commands.</param>
/// <param name="Refs">The refs the tab shows.</param>
/// <param name="IsActive">The tab is the active one.</param>
public sealed record TagContext(TabId TabId, RepositoryLocation Location, ImmutableArray<GitRef> Refs, bool IsActive);
