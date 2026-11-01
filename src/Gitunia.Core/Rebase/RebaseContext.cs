using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Rebase;

/// <summary>The facts of the root the rebase reducer reads.</summary>
/// <param name="Tab">The tab the command is for, with its HEAD, refs, stopped operation and changed files.</param>
/// <param name="IsActive">The tab is the active one.</param>
/// <param name="IsBusy">A rebase operation already runs for the tab (read from the tab's running operations).</param>
/// <param name="IsOverlayOpen">
/// A prompt, dialog, box or overlay is open, so a late question must not replace it.
/// </param>
public sealed record RebaseContext(RepositoryState Tab, bool IsActive, bool IsBusy, bool IsOverlayOpen);
