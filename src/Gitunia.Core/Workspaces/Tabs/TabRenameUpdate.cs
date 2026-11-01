using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Workspaces.Tabs;

/// <summary>The result of a rename command: the new tab state and the effects to run.</summary>
/// <param name="Tab">The tab after the command.</param>
/// <param name="Effects">Effects for the runner, possibly empty.</param>
public sealed record TabRenameUpdate(RepositoryState Tab, ImmutableArray<AppEffect> Effects);
