using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Compare;

/// <summary>The result of one comparison command.</summary>
/// <param name="Follow">The follow state of the tab afterwards.</param>
/// <param name="TabCommands">Commands for the tab's own state (the mark, the bottom tab), applied by the root.</param>
/// <param name="Effects">The effects to run.</param>
public sealed record CompareUpdate(
    CompareFollow Follow,
    ImmutableArray<RepositoryCommand> TabCommands,
    ImmutableArray<AppEffect> Effects);
