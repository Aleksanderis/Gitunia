using Gitunia.Git.Stash;

namespace Gitunia.Core.Stash;

/// <summary>What the stash stack did to the stash the detail pane shows.</summary>
/// <param name="Kind">The change.</param>
/// <param name="Current">The shown stash as the stack has it now; null when it left the stack.</param>
public sealed record StashViewChange(StashViewChangeKind Kind, StashEntry? Current);
