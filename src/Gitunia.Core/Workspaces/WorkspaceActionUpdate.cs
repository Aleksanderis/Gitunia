using System.Collections.Immutable;

namespace Gitunia.Core.Workspaces;

/// <summary>The result of a workspace-level action.</summary>
/// <param name="Session">The new session.</param>
/// <param name="Next">Commands to reduce next on the root state, in order.</param>
public sealed record WorkspaceActionUpdate(WorkspaceActionSession Session, ImmutableArray<AppCommand> Next);
