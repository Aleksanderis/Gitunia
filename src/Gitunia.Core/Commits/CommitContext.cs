using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Commits;

/// <summary>The facts of the root the commit reducer reads.</summary>
/// <param name="Tab">The tab the command is for, with its draft, HEAD, working tree, view and operation in progress.</param>
/// <param name="IsActive">The tab is the active one.</param>
/// <param name="Mode">The key mode.</param>
/// <param name="IsCommitting">A commit already runs for the tab (the box is read-only meanwhile).</param>
/// <param name="IsBlocked">Another operation that writes the index or the working tree runs for the tab.</param>
public sealed record CommitContext(RepositoryState Tab, bool IsActive, KeyMode Mode, bool IsCommitting, bool IsBlocked);
