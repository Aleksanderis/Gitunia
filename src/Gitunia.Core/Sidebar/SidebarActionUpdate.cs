using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Sidebar;

/// <summary>The result of one pane or sidebar command.</summary>
/// <param name="Session">The session afterwards.</param>
/// <param name="TabCommands">Commands for the tab's own state (focus, cursor, filter), applied by the root.</param>
/// <param name="Effects">The effects to run.</param>
public sealed record SidebarActionUpdate(
    SidebarSession Session,
    ImmutableArray<RepositoryCommand> TabCommands,
    ImmutableArray<AppEffect> Effects);
