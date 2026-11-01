using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Rebase;

/// <summary>The facts of the root the interactive rebase editor reducer reads.</summary>
/// <param name="Tab">The tab the command is for, with its HEAD, refs, stopped operation and changed files.</param>
/// <param name="IsActive">The tab is the active one.</param>
/// <param name="IsBusy">A rebase operation already runs for the tab (read from the tab's running operations).</param>
/// <param name="IsOverlayOpen">
/// A prompt, dialog, box or overlay other than the editor is open, so a late result must not open the editor over it.
/// </param>
public sealed record RebaseEditorContext(RepositoryState Tab, bool IsActive, bool IsBusy, bool IsOverlayOpen);
