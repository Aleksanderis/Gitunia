using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Conflicts;

/// <summary>What the conflict reducer needs to know about the tabs and the overlays.</summary>
/// <param name="Tab">The tab the command is about: the one it names, else the one of the open editor, else the active
/// one. Null when none is open.</param>
/// <param name="IsBusy">The tab runs a conflict operation (reading, saving or re-creating).</param>
/// <param name="IsCovered">A dialog, prompt, output box or another overlay is open over the editor.</param>
public sealed record ConflictContext(RepositoryState? Tab, bool IsBusy, bool IsCovered);
