using System.Collections.Immutable;

namespace Gitunia.Core.Workspaces;

/// <summary>The result of reducing a command: the new workspace and the effects to run.</summary>
/// <param name="Workspace">The new state.</param>
/// <param name="Effects">Effects for the runner, possibly empty.</param>
public sealed record WorkspaceUpdate(Workspace Workspace, ImmutableArray<WorkspaceEffect> Effects);
